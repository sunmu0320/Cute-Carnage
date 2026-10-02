using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds the fountain plaza from FountainPlazaDecor settings.
// Objects: "Generated" child is destroyed and rebuilt every time (no duplicates).
// Terrain: the plaza square is snapshotted once to Source~/fountain_plaza_terrain_orig.bytes (splat + all detail layers);
// every rebuild starts from that snapshot, so re-running never stacks paint, and Restore Terrain puts it back.
[CustomEditor(typeof(FountainPlazaDecor))]
public class FountainPlazaDecorEditor : Editor
{
    private const string BackupPath = "Assets/Art/MapReference/Source~/fountain_plaza_terrain_orig.bytes";
    private const float Half = 22f; // terrain square half size around the fountain

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var d = (FountainPlazaDecor)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild Plaza")) Build(d);
        if (GUILayout.Button("Clear Objects + Restore Terrain")) Clear(d);
    }

    public static string Build(FountainPlazaDecor d)
    {
        if (d.fountain == null) return "no fountain";
        var t = Terrain.activeTerrain; var td = t.terrainData;
        Vector3 c3 = d.fountain.position; var c = new Vector2(c3.x, c3.z);
        var rng = new System.Random(d.seed);
        float Rand() => (float)rng.NextDouble();

        Undo.RegisterFullObjectHierarchyUndo(d.gameObject, "Rebuild Plaza");
        var old = d.transform.Find("Generated");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("Generated").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Rebuild Plaza");
        root.SetParent(d.transform, false);
        var pave = Child(root, "Paving"); var props = Child(root, "Benches"); var deb = Child(root, "Debris"); var weeds = Child(root, "Weeds");

        // ---- terrain snapshot ------------------------------------------------------------------------------------
        Cells(t, c, out int x0, out int z0, out int n);
        Undo.RegisterCompleteObjectUndo(td, "Rebuild Plaza");
        if (!File.Exists(BackupPath)) Save(td, x0, z0, n);
        Load(td, out _, out _, out _, out float[,,] alpha, out int[][,] details);
        int L3 = Layer(td, "TL_Dirt"), L5 = Layer(td, "TL_LightGrass"), L6 = Layer(td, "TL_Road");

        float EdgeR(float ang) => d.outerRadius + d.edgeWobble * (Fbm1(ang * 1.6f + d.seed) - 0.5f) * 2f;
        float Ang(Vector2 p) => Mathf.Atan2(p.y - c.y, p.x - c.x);
        float RoadAt(Vector2 p) => SampleLayer(t, alpha, x0, z0, n, L6, p);

        // lanes: from paving edge to end point, gently bent
        var lanes = new List<Vector2[]>();
        foreach (var e in d.laneEnds)
        {
            Vector2 dir = (e - c).normalized, s = c + dir * (EdgeR(Mathf.Atan2(dir.y, dir.x)) - 1.2f);
            Vector2 side = new Vector2(-dir.y, dir.x) * ((Rand() - 0.5f) * 2.4f);
            lanes.Add(new[] { s, Vector2.Lerp(s, e, 0.5f) + side, e });
        }

        // ---- paving -----------------------------------------------------------------------------------------------
        var stones = d.pavingStones;
        var stoneCells = new List<Vector2>(); var gaps = new List<Vector2>(); var edgeGaps = new List<Vector2>();
        int rows = Mathf.CeilToInt((d.outerRadius + d.edgeWobble + 1f) / d.cellSize);
        for (int iz = -rows; iz <= rows; iz++)
        for (int ix = -rows; ix <= rows; ix++)
        {
            float off = (iz & 1) == 0 ? 0f : d.cellSize * 0.5f;            // running bond keeps a clear laying direction
            var p = c + new Vector2(ix * d.cellSize + off, iz * d.cellSize);
            float r = Vector2.Distance(p, c), er = EdgeR(Ang(p));
            if (r < d.innerRadius + d.cellSize * 0.35f || r > er) continue;
            if (RoadAt(p) > 0.35f) continue;                                // the road runs into the plaza
            float edge = Mathf.InverseLerp(er - 2f, er, r);
            if (Rand() < edge * 0.75f) { edgeGaps.Add(p); continue; }         // broken, eaten-away edge
            if (Rand() < d.missingChance) { gaps.Add(p); continue; }
            stoneCells.Add(p);
            if (stones == null || stones.Length == 0) continue;
            var prefab = stones[rng.Next(stones.Length)];
            bool cracked = Rand() < d.crackedChance + edge * 0.15f;
            float yaw = (Rand() - 0.5f) * (4f + edge * 8f);
            if (!cracked)
                Place(prefab, pave, t, p + Jit(rng, 0.04f), yaw, d.cellSize * (0.93f + Rand() * 0.05f), d.cellSize * (0.93f + Rand() * 0.05f),
                      d.stoneTopHeight + Rand() * 0.02f, edge * 3f, rng);
            else
            {
                // split in two along a random axis, the pieces drift apart a little
                bool alongX = Rand() < 0.5f; float a = 0.35f + Rand() * 0.3f, s = d.cellSize * 0.92f;
                for (int k = 0; k < 2; k++)
                {
                    float frac = k == 0 ? a : 1f - a, centerOff = (k == 0 ? -1f : 1f) * (s * (1f - frac) * 0.5f + 0.03f);
                    Vector2 o = alongX ? new Vector2(centerOff, 0f) : new Vector2(0f, centerOff);
                    Place(prefab, pave, t, p + o + Jit(rng, 0.05f), yaw + (Rand() - 0.5f) * 10f,
                          alongX ? s * frac - 0.04f : s, alongX ? s : s * frac - 0.04f, d.stoneTopHeight - Rand() * 0.02f, 3f + edge * 3f, rng);
                }
            }
        }

        // ---- terrain: dirt under the paving, worn lanes, no grass through the stones -------------------------------
        for (int zi = 0; zi < n; zi++)
        for (int xi = 0; xi < n; xi++)
        {
            var p = CellWorld(t, x0 + xi, z0 + zi);
            float r = Vector2.Distance(p, c), er = EdgeR(Ang(p));
            float plaza = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(er - 0.3f, er + 1.2f + Fbm1(Ang(p) * 5f) * 1.5f, r));
            float lane = 0f, laneEdge = 0f;
            foreach (var l in lanes)
            {
                float dist = Mathf.Min(SegDist(p, l[0], l[1]), SegDist(p, l[1], l[2])) + (Mathf.PerlinNoise(p.x * 0.4f, p.y * 0.4f) - 0.5f) * 0.8f;
                lane = Mathf.Max(lane, 1f - Mathf.InverseLerp(d.laneWidth * 0.25f, d.laneWidth * 0.5f, dist));
                laneEdge = Mathf.Max(laneEdge, Bump(dist, d.laneWidth * 0.3f, d.laneWidth * 0.55f, d.laneWidth * 0.95f));
            }
            float roadW = alpha[zi, xi, L6];
            float dirt = Mathf.Max(plaza * (0.75f + 0.2f * Mathf.PerlinNoise(p.x * 0.3f + 9f, p.y * 0.3f)), lane * 0.85f) * (1f - roadW);
            float dry = Mathf.Min(laneEdge * 0.6f + Bump(r - er, -0.5f, 0.8f, 2.5f) * 0.35f * Mathf.PerlinNoise(p.x * 0.5f, p.y * 0.5f + 40f), 1f - dirt) * (1f - roadW);
            SetWeight(alpha, zi, xi, L3, dirt);
            SetWeight(alpha, zi, xi, L5, Mathf.Max(alpha[zi, xi, L5], dry));

            // grass: none on paving / lane centers, thinned on lane edges
            float keep = (1f - Mathf.InverseLerp(er + 0.2f, er - 0.6f, r) * (r > d.innerRadius - 1f ? 1f : 0f)) * (1f - lane);
            keep *= 1f - laneEdge * 0.5f;
            foreach (var g in details) g[zi, xi] = Mathf.RoundToInt(g[zi, xi] * Mathf.Clamp01(keep));
        }
        td.SetAlphamaps(x0, z0, alpha);
        for (int L = 0; L < details.Length; L++) td.SetDetailLayer(x0, z0, L, details[L]);
        EditorUtility.SetDirty(td);

        // ---- benches -------------------------------------------------------------------------------------------------
        int benches = 0;
        if (d.bench != null)
            foreach (float a0 in d.benchAngles)
            {
                float a = (a0 + (Rand() - 0.5f) * 6f) * Mathf.Deg2Rad, r = EdgeR(a) - 1.6f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(d.bench, props);
                go.transform.localScale = Vector3.one * d.benchScale;
                Vector2 toC = (c - p).normalized;                              // seat front (+Z) faces the fountain
                float yaw = Mathf.Atan2(toC.x, toC.y) * Mathf.Rad2Deg + d.benchYawOffset + (Rand() - 0.5f) * 8f;
                go.transform.SetPositionAndRotation(new Vector3(p.x, Ground(t, p) + d.stoneTopHeight, p.y), Quaternion.Euler(0f, yaw, 0f));
                StripColliders(go); benches++;
                // weeds behind the bench
                for (int k = 0; k < 4; k++) Weed(d, weeds, t, p - toC * (0.7f + Rand() * 0.6f) + new Vector2(-toC.y, toC.x) * ((Rand() - 0.5f) * 2.6f), rng);
            }

        // ---- debris: a few loose stones / chips in one edge sector ---------------------------------------------------
        if (d.debris != null && d.debris.Length > 0)
            for (int k = 0; k < d.debrisCount; k++)
            {
                float a = (d.debrisAngle + (Rand() - 0.5f) * 40f) * Mathf.Deg2Rad, r = EdgeR(a) + (Rand() - 0.6f) * 1.8f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var prefab = d.debris[rng.Next(d.debris.Length)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, deb);
                go.transform.localScale = Vector3.one * (0.45f + Rand() * 0.35f);
                go.transform.SetPositionAndRotation(new Vector3(p.x, Ground(t, p), p.y), Quaternion.Euler((Rand() - 0.5f) * 14f, Rand() * 360f, (Rand() - 0.5f) * 14f));
                StripColliders(go);
            }

        // ---- weeds: fountain base, some cracks, broken edge ----------------------------------------------------------
        for (int k = 0; k < 9; k++)
        {
            float a = Rand() * Mathf.PI * 2f; var cp = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (d.innerRadius + 0.15f);
            int m = 2 + rng.Next(3);
            for (int j = 0; j < m; j++) Weed(d, weeds, t, cp + Jit(rng, 0.5f), rng);
        }
        foreach (var g in gaps) if (Rand() < 0.7f) for (int j = 0; j < 1 + rng.Next(2); j++) Weed(d, weeds, t, g + Jit(rng, 0.35f), rng);
        foreach (var g in edgeGaps) if (Rand() < 0.22f) for (int j = 0; j < 1 + rng.Next(3); j++) Weed(d, weeds, t, g + Jit(rng, 0.5f), rng);

        string msg = $"[FountainPlaza] stones {pave.childCount} (cells {stoneCells.Count}, gaps {gaps.Count}, edge gaps {edgeGaps.Count}), benches {benches}, debris {deb.childCount}, weeds {weeds.childCount}";
        Debug.Log(msg);
        return msg;
    }

    public static void Clear(FountainPlazaDecor d)
    {
        var old = d.transform.Find("Generated");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        if (!File.Exists(BackupPath)) return;
        var td = Terrain.activeTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Restore Plaza Terrain");
        Load(td, out int x0, out int z0, out int n, out float[,,] a, out int[][,] det);
        td.SetAlphamaps(x0, z0, a);
        for (int L = 0; L < det.Length; L++) td.SetDetailLayer(x0, z0, L, det[L]);
        EditorUtility.SetDirty(td);
    }

    // ---------------------------------------------------------------------------------------------------------------
    private static Transform Child(Transform p, string name) { var t = new GameObject(name).transform; t.SetParent(p, false); return t; }

    private static void Place(GameObject prefab, Transform parent, Terrain t, Vector2 p, float yaw, float sx, float sz, float top, float tilt, System.Random rng)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one; go.transform.position = Vector3.zero;
        var b = Bounds(go);
        go.transform.localScale = new Vector3(sx / Mathf.Max(b.size.x, 0.01f), 1f, sz / Mathf.Max(b.size.z, 0.01f));
        b = Bounds(go);
        float ground = Mathf.Max(Ground(t, p + new Vector2(-0.5f, -0.5f)), Ground(t, p + new Vector2(0.5f, 0.5f)), Ground(t, p));
        go.transform.position = new Vector3(p.x - b.center.x, ground + top - b.max.y, p.y - b.center.z);
        go.transform.RotateAround(new Vector3(p.x, ground, p.y), Vector3.up, yaw);
        if (tilt > 0f)
        {
            float tx = ((float)rng.NextDouble() - 0.5f) * tilt, tz = ((float)rng.NextDouble() - 0.5f) * tilt;
            go.transform.RotateAround(new Vector3(p.x, ground, p.y), Vector3.right, tx);
            go.transform.RotateAround(new Vector3(p.x, ground, p.y), Vector3.forward, tz);
        }
        StripColliders(go);
    }

    private static void Weed(FountainPlazaDecor d, Transform parent, Terrain t, Vector2 p, System.Random rng)
    {
        if (d.weed == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(d.weed, parent);
        float s = Mathf.Lerp(d.weedScale.x, d.weedScale.y, (float)rng.NextDouble());
        go.transform.localScale = new Vector3(s, s * (0.8f + 0.3f * (float)rng.NextDouble()), s);
        go.transform.SetPositionAndRotation(new Vector3(p.x, Ground(t, p) + 0.02f, p.y), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        StripColliders(go);
    }

    private static void StripColliders(GameObject go) { foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col); }

    private static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Vector2 Jit(System.Random rng, float a) => new Vector2(((float)rng.NextDouble() - 0.5f) * 2f * a, ((float)rng.NextDouble() - 0.5f) * 2f * a);
    private static float Ground(Terrain t, Vector2 p) => t.SampleHeight(new Vector3(p.x, 0f, p.y)) + t.transform.position.y;
    private static float Fbm1(float x) => 0.65f * Mathf.PerlinNoise(x, 3.7f) + 0.35f * Mathf.PerlinNoise(x * 2.3f, 11.1f);
    private static float Bump(float x, float a, float b, float c) => x < b ? Mathf.InverseLerp(a, b, x) : 1f - Mathf.InverseLerp(b, c, x);

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * k);
    }

    private static int Layer(TerrainData td, string name)
    {
        for (int i = 0; i < td.terrainLayers.Length; i++) if (td.terrainLayers[i] != null && td.terrainLayers[i].name == name) return i;
        return -1;
    }

    // scales the other layers so the cell still sums to 1
    private static void SetWeight(float[,,] a, int z, int x, int k, float w)
    {
        int L = a.GetLength(2); float rest = 0f;
        for (int i = 0; i < L; i++) if (i != k) rest += a[z, x, i];
        float s = rest > 1e-5f ? (1f - w) / rest : 0f;
        for (int i = 0; i < L; i++) a[z, x, i] = i == k ? w : a[z, x, i] * s;
    }

    private static void Cells(Terrain t, Vector2 c, out int x0, out int z0, out int n)
    {
        var td = t.terrainData; Vector3 o = t.transform.position; int res = td.alphamapResolution;
        x0 = Mathf.FloorToInt((c.x - Half - o.x) / td.size.x * (res - 1));
        z0 = Mathf.FloorToInt((c.y - Half - o.z) / td.size.z * (res - 1));
        n = Mathf.CeilToInt(Half * 2f / td.size.x * (res - 1)) + 1;
    }

    private static Vector2 CellWorld(Terrain t, int x, int z)
    {
        var td = t.terrainData; Vector3 o = t.transform.position; int res = td.alphamapResolution;
        return new Vector2(o.x + x / (float)(res - 1) * td.size.x, o.z + z / (float)(res - 1) * td.size.z);
    }

    private static float SampleLayer(Terrain t, float[,,] a, int x0, int z0, int n, int k, Vector2 p)
    {
        var td = t.terrainData; Vector3 o = t.transform.position; int res = td.alphamapResolution;
        int x = Mathf.RoundToInt((p.x - o.x) / td.size.x * (res - 1)) - x0, z = Mathf.RoundToInt((p.y - o.z) / td.size.z * (res - 1)) - z0;
        return x < 0 || z < 0 || x >= n || z >= n ? 0f : a[z, x, k];
    }

    // backup format: x0 z0 n layers detailLayers, alpha floats, then each detail layer's ints (alpha and detail share the grid)
    private static void Save(TerrainData td, int x0, int z0, int n)
    {
        var a = td.GetAlphamaps(x0, z0, n, n); int L = td.alphamapLayers, D = td.detailPrototypes.Length;
        using var bw = new BinaryWriter(File.Create(BackupPath));
        bw.Write(x0); bw.Write(z0); bw.Write(n); bw.Write(L); bw.Write(D);
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) for (int k = 0; k < L; k++) bw.Write(a[z, x, k]);
        for (int k = 0; k < D; k++) { var g = td.GetDetailLayer(x0, z0, n, n, k); for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) bw.Write(g[z, x]); }
    }

    private static void Load(TerrainData td, out int x0, out int z0, out int n, out float[,,] a, out int[][,] det)
    {
        using var br = new BinaryReader(File.OpenRead(BackupPath));
        x0 = br.ReadInt32(); z0 = br.ReadInt32(); n = br.ReadInt32(); int L = br.ReadInt32(), D = br.ReadInt32();
        a = new float[n, n, L]; det = new int[D][,];
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) for (int k = 0; k < L; k++) a[z, x, k] = br.ReadSingle();
        for (int k = 0; k < D; k++) { det[k] = new int[n, n]; for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) det[k][z, x] = br.ReadInt32(); }
    }
}
