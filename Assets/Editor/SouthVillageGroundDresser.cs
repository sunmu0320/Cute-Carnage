using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// South village ground + grass dressing (Zone_South_RuinedVillage only).
// - Ground: inside the village the natural-ground weights (TL_GroundGreen, TL_Grass, TL_Dirt, TL_LightGrass) are
//   redistributed into muted olive/green base + worn dirt (doors, path ends, yards) + dead grass (TL_LightGrass) rims.
//   Road / gravel / wet dirt / Ground_Layer_01 weights are never touched, so roads, river banks and slopes keep their paint.
// - Grass: CC_GrassClump (shared with the outer nature) is cleared inside the village and replaced by the
//   village-only prototype CC_GrassClump_Village (muted material copy), placed as big clusters / small clusters / bare ground.
// The original region is saved once to Source~/south_village_ground_orig.bytes; every run rebuilds from it,
// so re-running gives the same result (no stacking), and Restore puts the original back.
public static class SouthVillageGroundDresser
{
    private const string BackupPath = "Assets/Art/MapReference/Source~/south_village_ground_orig.bytes";
    private const string RoadsJson = "Assets/Art/MapReference/Source~/village_roads.json";
    private const string VillagePrefab = "Assets/Art/Nature/TerrainDetails/CC_GrassClump_Village.prefab";
    private const float MinX = -175f, MaxX = 140f, MinZ = -185f, MaxZ = 2f;

    private struct Building { public Rect r; public Vector2 c; public Vector2 door; }

    [MenuItem("Tools/Map/7. Dress South Village Ground + Grass")]
    public static void ApplyAll()
    {
        // Rebuilds from the first-run snapshot: any hand painting done in the village since then is lost.
        if (EditorUtility.DisplayDialog("Dress South Village", "This rebuilds the village ground and grass from the original snapshot and overwrites hand-painted changes made since. Continue?", "Overwrite", "Cancel"))
            Apply(null);
    }

    [MenuItem("Tools/Map/7b. Restore South Village Ground (original)")]
    public static void Restore()
    {
        var t = Terrain.activeTerrain; var td = t.terrainData;
        if (!File.Exists(BackupPath)) { Debug.LogError("[SouthVillage] No backup found."); return; }
        Load(td, out int x0, out int z0, out int w, out int h, out float[,,] alpha, out int[,] grass);
        Undo.RegisterCompleteObjectUndo(td, "Restore South Village");
        td.SetAlphamaps(x0, z0, alpha);
        td.SetDetailLayer(x0, z0, 1, grass);
        int v = VillageLayer(td, false);
        if (v >= 0) td.SetDetailLayer(x0, z0, v, new int[h, w]);
        EditorUtility.SetDirty(td);
        Debug.Log("[SouthVillage] Original ground and grass restored.");
    }

    // limit: optional world XZ rect (test area); outside it nothing changes.
    public static string Apply(Rect? limit)
    {
        var t = Terrain.activeTerrain; var td = t.terrainData;
        Vector3 o = t.transform.position, size = td.size;
        int res = td.alphamapResolution;
        if (td.detailWidth != res) return "detail/alpha resolution mismatch";
        int L0 = Layer(td, "TL_GroundGreen"), L2 = Layer(td, "TL_Grass"), L3 = Layer(td, "TL_Dirt"), L5 = Layer(td, "TL_LightGrass");
        if (Mathf.Min(L0, Mathf.Min(L2, Mathf.Min(L3, L5))) < 0) return "missing terrain layer";

        int x0 = Mathf.FloorToInt((MinX - o.x) / size.x * (res - 1)), x1 = Mathf.CeilToInt((MaxX - o.x) / size.x * (res - 1));
        int z0 = Mathf.FloorToInt((MinZ - o.z) / size.z * (res - 1)), z1 = Mathf.CeilToInt((MaxZ - o.z) / size.z * (res - 1));
        int w = x1 - x0 + 1, h = z1 - z0 + 1;

        Undo.RegisterCompleteObjectUndo(td, "Dress South Village");
        if (!File.Exists(BackupPath)) Save(td, x0, z0, w, h);
        Load(td, out _, out _, out _, out _, out float[,,] orig, out int[,] origGrass);
        int V = VillageLayer(td, true);

        var buildings = Buildings();
        ReadNetwork(out var roads, out var paths, out var doors, out var juncs);
        for (int i = 0; i < buildings.Count; i++)   // front = direction to the nearest door
        {
            var b = buildings[i]; Vector2 best = b.c + Vector2.down; float bd = float.MaxValue;
            foreach (var d in doors) { float dd = Vector2.Distance(d, b.c); if (dd < bd) { bd = dd; best = d; } }
            b.door = (best - b.c).normalized; buildings[i] = b;
        }

        float[,,] alpha = td.GetAlphamaps(x0, z0, w, h);
        int[,] g1 = td.GetDetailLayer(x0, z0, w, h, 1), gv = td.GetDetailLayer(x0, z0, w, h, V);
        int layers = td.alphamapLayers;
        var mask = ~0;

        for (int zi = 0; zi < h; zi++)
        for (int xi = 0; xi < w; xi++)
        {
            float u = (x0 + xi) / (float)(res - 1), v = (z0 + zi) / (float)(res - 1);
            var p = new Vector2(o.x + u * size.x, o.z + v * size.z);
            if (limit.HasValue && !limit.Value.Contains(p)) continue;

            // ---- region weight -------------------------------------------------------------------------------
            float dB = float.MaxValue; Building nb = default;
            foreach (var b in buildings) { float d = RectDist(b.r, p); if (d < dB) { dB = d; nb = b; } }
            float R = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 36f, dB));
            R *= Mathf.InverseLerp(-2f, -10f, p.y);                                           // keep off the hub/plaza road
            R *= Mathf.InverseLerp(32f, 42f, Vector2.Distance(p, new Vector2(-2f, 27f)));       // HomeBase plaza
            R *= 1f - Mathf.InverseLerp(-44f, -36f, p.y) * Mathf.InverseLerp(92f, 100f, p.x);  // east factory
            R *= 1f - Mathf.InverseLerp(-14f, -6f, p.y) * Mathf.InverseLerp(-90f, -98f, p.x);  // west mall
            if (R <= 0f)
            {
                // outside: original state
                for (int k = 0; k < layers; k++) alpha[zi, xi, k] = orig[zi, xi, k];
                g1[zi, xi] = origGrass[zi, xi]; gv[zi, xi] = 0;
                continue;
            }

            // ---- distance fields -----------------------------------------------------------------------------
            float dRoad = 99f, roadHalf = 3f;
            foreach (var r in roads) { float d = PolyDist(r.pts, p) - r.w * 0.5f; if (d < dRoad) { dRoad = d; roadHalf = r.w * 0.5f; } }
            float dPath = 99f;
            foreach (var r in paths) dPath = Mathf.Min(dPath, PolyDist(r.pts, p) - r.w * 0.5f);
            float dDoor = 99f; int doorId = 0;
            for (int i = 0; i < doors.Count; i++) { float d = Vector2.Distance(doors[i], p); if (d < dDoor) { dDoor = d; doorId = i; } }
            float dJ = 99f; int jId = 0;
            for (int i = 0; i < juncs.Count; i++) { float d = Vector2.Distance(juncs[i], p); if (d < dJ) { dJ = d; jId = i; } }

            float warp = Fbm(p * 0.06f, 2) - 0.5f;                      // edge wobble (m-scale offsets below)
            float edgeSoft = Mathf.Lerp(0.4f, 3.5f, Fbm(p * 0.045f + new Vector2(31f, 7f), 2)); // sharp vs gentle borders

            // ---- worn dirt -----------------------------------------------------------------------------------
            float dirt = 0f;
            float rDoor = 1.2f + 2.4f * Hash(doorId);
            dirt = Mathf.Max(dirt, (0.55f + 0.3f * Hash(doorId + 77)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rDoor, rDoor + edgeSoft, dDoor + warp * 5f))));
            float rJ = 1.5f + 2.5f * Hash(jId + 500);
            dirt = Mathf.Max(dirt, 0.7f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rJ, rJ + edgeSoft, dJ + warp * 4f))));
            dirt = Mathf.Max(dirt, (0.3f + 0.35f * Fbm(p * 0.15f, 2)) * (1f - Mathf.InverseLerp(-0.6f, 0.8f, dPath + warp * 1.5f)));
            float yard = Fbm(p * 0.035f + new Vector2(-40f, 12f) + Vector2.one * warp * 0.8f, 3);
            float nearHouse = 1f - Mathf.InverseLerp(4f, 12f, dB);
            dirt = Mathf.Max(dirt, nearHouse * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.68f + 0.02f * edgeSoft + 0.02f, yard)) * 0.8f);
            dirt = Mathf.Max(dirt, 0.45f * Mathf.InverseLerp(0.45f, 0.65f, Fbm(p * 0.12f + new Vector2(3f, 9f), 2)) * (1f - Mathf.InverseLerp(0.2f, 1.8f, dRoad)));  // road shoulder
            dirt = Mathf.Clamp(dirt, 0f, 0.85f);

            // ---- grass cluster potential (low traffic: building backs, outskirts) -----------------------------
            Vector2 toP = (p - nb.c).normalized;
            float behind = Mathf.Clamp01(Vector2.Dot(toP, -nb.door) * 1.4f) * (1f - Mathf.InverseLerp(2f, 9f, dB));
            float traffic = Mathf.Max(1f - Mathf.InverseLerp(3f, 9f, dDoor), Mathf.Max(1f - Mathf.InverseLerp(3f, 8f, dJ),
                            Mathf.Max(1f - Mathf.InverseLerp(0f, 2.5f, dPath), 0.55f * (1f - Mathf.InverseLerp(0.5f, 3.5f, dRoad)))));
            float big = Fbm(p * 0.033f + new Vector2(17f, -5f) + Vector2.one * warp * 1.2f, 3)
                        + 0.18f * behind + 0.14f * Mathf.InverseLerp(8f, 24f, dB) - 0.45f * traffic - 0.6f * dirt;
            float bigMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.56f, big));
            float core = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.66f, big));

            // ---- dead grass ----------------------------------------------------------------------------------
            float n2 = Fbm(p * 0.09f + new Vector2(70f, 70f), 2);
            float dead = Bump(dirt, 0.05f, 0.25f, 0.55f) * Mathf.InverseLerp(0.3f, 0.65f, n2) * 0.85f;   // between dirt and grass
            dead = Mathf.Max(dead, Bump(big, 0.34f, 0.42f, 0.5f) * Mathf.InverseLerp(0.55f, 0.75f, n2) * 0.6f); // some cluster rims
            float neglect = Fbm(p * 0.028f + new Vector2(-90f, 44f) + Vector2.one * warp, 3);
            dead = Mathf.Max(dead, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.72f + 0.015f * edgeSoft + 0.01f, neglect)) * (1f - traffic) * 0.6f);
            dead = Mathf.Min(dead, 1f - dirt);

            // ---- ground ------------------------------------------------------------------------------------------
            float rem = orig[zi, xi, L0] + orig[zi, xi, L2] + orig[zi, xi, L3] + orig[zi, xi, L5];
            float green = 1f - dirt - dead;
            float g0 = Mathf.Clamp01(0.12f + 0.2f * Fbm(p * 0.05f + new Vector2(5f, 55f), 2) + 0.3f * bigMask); // deep green share under clusters
            var nw = new float[layers];
            nw[L3] = dirt; nw[L5] = dead; nw[L0] = green * g0; nw[L2] = green * (1f - g0);
            for (int k = 0; k < layers; k++)
            {
                bool natural = k == L0 || k == L2 || k == L3 || k == L5;
                alpha[zi, xi, k] = natural ? Mathf.Lerp(orig[zi, xi, k], nw[k] * rem, R) : orig[zi, xi, k];
            }

            // ---- grass -------------------------------------------------------------------------------------------
            float small = Fbm(p * 0.14f + new Vector2(-13f, 61f), 2);
            float yardEdge = Bump(dB, 2.5f, 5f, 9f);
            float smallMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.64f, 0.74f, small))
                              * Mathf.Max(Mathf.InverseLerp(0.3f, 0.42f, big), yardEdge) * (1f - traffic);
            float holes = Mathf.InverseLerp(0.25f, 0.42f, Mathf.PerlinNoise(p.x * 0.45f + 300f, p.y * 0.45f + 300f));
            float roadBreak = 1f - (1f - Mathf.InverseLerp(0.38f, 0.6f, Mathf.PerlinNoise(p.x * 0.11f + 50f, p.y * 0.11f - 20f)))
                                  * (1f - Mathf.InverseLerp(1f, 5f, dRoad));
            float G = Mathf.Max(bigMask * (0.6f + 0.4f * core), smallMask * 0.55f) * holes * roadBreak;
            G *= 1f - Mathf.InverseLerp(0.15f, 0.4f, dirt);
            G *= 1f - 0.7f * dead;
            float hard = 1f - rem;                                                     // road / gravel / wet dirt
            if (hard > 0.08f || dDoor < 3.5f || dPath < 0.5f || dB < 0.3f) G = 0f;
            if (G > 0f && td.GetSteepness(u, v) > 28f) G = 0f;
            if (G > 0f && Physics.Raycast(new Vector3(p.x, 80f, p.y), Vector3.down, out var hit, 200f, mask, QueryTriggerInteraction.Ignore)
                && !(hit.collider is TerrainCollider)) G = 0f;

            int cov = Mathf.RoundToInt(255f * G * R);
            gv[zi, xi] = cov < 12 ? 0 : cov;
            g1[zi, xi] = Mathf.RoundToInt(origGrass[zi, xi] * (1f - R));
        }

        td.SetAlphamaps(x0, z0, alpha);
        td.SetDetailLayer(x0, z0, 1, g1);
        td.SetDetailLayer(x0, z0, V, gv);
        EditorUtility.SetDirty(td);
        string msg = $"[SouthVillage] dressed {w}x{h} cells, {buildings.Count} buildings, {doors.Count} doors, {juncs.Count} junctions";
        Debug.Log(msg);
        return msg;
    }

    // ---------------------------------------------------------------------------------------------------------------
    private static int Layer(TerrainData td, string name)
    {
        for (int i = 0; i < td.terrainLayers.Length; i++) if (td.terrainLayers[i] != null && td.terrainLayers[i].name == name) return i;
        return -1;
    }

    private static int VillageLayer(TerrainData td, bool create)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillagePrefab);
        var protos = td.detailPrototypes;
        for (int i = 0; i < protos.Length; i++) if (prefab != null && protos[i].prototype == prefab) return i;
        if (!create || prefab == null) return -1;
        var src = protos[1];
        var p = new DetailPrototype
        {
            prototype = prefab, usePrototypeMesh = true, renderMode = src.renderMode, useInstancing = src.useInstancing,
            minWidth = src.minWidth, maxWidth = src.maxWidth, minHeight = src.minHeight, maxHeight = src.maxHeight,
            noiseSpread = 0.35f, density = src.density, alignToGround = src.alignToGround,
            healthyColor = new Color(1f, 1f, 0.97f), dryColor = new Color(0.9f, 0.9f, 0.8f),
            holeEdgePadding = src.holeEdgePadding, positionJitter = src.positionJitter, targetCoverage = src.targetCoverage,
            useDensityScaling = src.useDensityScaling,
        };
        var list = new List<DetailPrototype>(protos) { p };
        td.detailPrototypes = list.ToArray();
        return list.Count - 1;
    }

    // Short, sparse grass hugging house walls, beside doors and along both road sides.
    // Only writes its own detail layer (short copy of the village clump) from the CURRENT terrain paint, so hand-painted
    // ground/roads are read as-is and never modified; re-running rewrites the layer (no stacking).
    [MenuItem("Tools/Map/7c. South Village Edge Grass (short, sparse)")]
    public static string EdgeGrass()
    {
        var t = Terrain.activeTerrain; var td = t.terrainData;
        Vector3 o = t.transform.position, size = td.size;
        int res = td.alphamapResolution;
        int L2 = Layer(td, "TL_Grass"), L3 = Layer(td, "TL_Dirt"), L6 = Layer(td, "TL_Road");
        int main = VillageLayer(td, false); if (main < 0) return "run 7 first";
        int S = ShortLayer(td, main);

        int x0 = Mathf.FloorToInt((MinX - o.x) / size.x * (res - 1)), x1 = Mathf.CeilToInt((MaxX - o.x) / size.x * (res - 1));
        int z0 = Mathf.FloorToInt((MinZ - o.z) / size.z * (res - 1)), z1 = Mathf.CeilToInt((MaxZ - o.z) / size.z * (res - 1));
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        float cell = size.x / (res - 1);

        Undo.RegisterCompleteObjectUndo(td, "South Village Edge Grass");
        float[,,] a = td.GetAlphamaps(x0, z0, w, h);
        int[,] gm = td.GetDetailLayer(x0, z0, w, h, main);
        var gs = new int[h, w];

        // distance (m) to painted road, two-pass chamfer over the grid
        var dr = new float[h, w];
        for (int z = 0; z < h; z++) for (int x = 0; x < w; x++) dr[z, x] = a[z, x, L6] > 0.5f ? 0f : 1e6f;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < w * h; i++)
            {
                int idx = pass == 0 ? i : w * h - 1 - i, z = idx / w, x = idx % w, s = pass == 0 ? -1 : 1;
                float best = dr[z, x];
                if (x + s >= 0 && x + s < w) best = Mathf.Min(best, dr[z, x + s] + cell);
                if (z + s >= 0 && z + s < h)
                {
                    best = Mathf.Min(best, dr[z + s, x] + cell);
                    if (x + s >= 0 && x + s < w) best = Mathf.Min(best, dr[z + s, x + s] + cell * 1.414f);
                    if (x - s >= 0 && x - s < w) best = Mathf.Min(best, dr[z + s, x - s] + cell * 1.414f);
                }
                dr[z, x] = best;
            }

        var houses = new List<Rect>();
        var zone = GameObject.Find("Map_Blockout/Zone_South_RuinedVillage").transform;
        foreach (var group in new[] { "Houses", "Landmarks" })
        {
            var g = zone.Find(group); if (g == null) continue;
            foreach (Transform c in g)
            {
                var rs = c.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                houses.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
            }
        }
        ReadNetwork(out _, out _, out var doors, out _);
        var plaza = GameObject.Find("WaterFountain");
        Vector2 fc = plaza != null ? new Vector2(plaza.transform.position.x, plaza.transform.position.z) : new Vector2(1e6f, 1e6f);

        int placed = 0;
        for (int zi = 0; zi < h; zi++)
        for (int xi = 0; xi < w; xi++)
        {
            float u = (x0 + xi) / (float)(res - 1), v = (z0 + zi) / (float)(res - 1);
            var p = new Vector2(o.x + u * size.x, o.z + v * size.z);
            if (p.y > -8f || Vector2.Distance(p, fc) < 12.5f) continue;
            if (p.x > 92f && p.y > -40f) continue;   // east factory
            if (p.x < -92f && p.y > -14f) continue;  // west mall

            float dB = 99f; foreach (var r in houses) dB = Mathf.Min(dB, RectDist(r, p));
            float dDoor = 99f; foreach (var d in doors) dDoor = Mathf.Min(dDoor, Vector2.Distance(d, p));
            float dRoad = dr[zi, xi];
            if (dB > 30f) continue;

            float wall = Bump(dB, 0.1f, 1.2f, 4.2f);
            float entrance = Bump(dDoor, 1.2f, 2.4f, 4.5f);
            float roadside = dRoad > 0f ? Bump(dRoad, 0.2f, 1.3f, 3.8f) : 0f;
            float band = Mathf.Max(wall, Mathf.Max(entrance * 0.9f, roadside));
            if (band <= 0f) continue;

            // patchy: roughly half of the band stays bare, no continuous strip
            float patch = Mathf.InverseLerp(0.4f, 0.6f, Fbm(p * 0.22f + new Vector2(211f, -37f), 2));
            float speck = Mathf.PerlinNoise(p.x * 0.9f + 500f, p.y * 0.9f + 500f) > 0.35f ? 1f : 0f;
            float G = band * Mathf.Max(patch, Mathf.Max(wall, entrance) * 0.55f) * speck;   // walls/doors get a light fringe even off-patch

            float hard = a[zi, xi, L6] + a[zi, xi, L3] * 0.8f;   // road, worn dirt
            G *= 1f - Mathf.InverseLerp(0.15f, 0.55f, hard);
            if (dDoor < 1.2f || gm[zi, xi] > 40 || G < 0.05f) continue;
            if (td.GetSteepness(u, v) > 28f) continue;
            if (Physics.Raycast(new Vector3(p.x, 80f, p.y), Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                && !(hit.collider is TerrainCollider)) continue;

            gs[zi, xi] = Mathf.RoundToInt(Mathf.Lerp(70f, 190f, G));
            placed++;
        }
        td.SetDetailLayer(x0, z0, S, gs);
        EditorUtility.SetDirty(td);
        string msg = $"[SouthVillage] edge grass cells {placed} on detail layer {S}";
        Debug.Log(msg);
        return msg;
    }

    // short copy of the village clump (same prefab/material, lower and narrower)
    private static int ShortLayer(TerrainData td, int main)
    {
        var protos = td.detailPrototypes; var prefab = protos[main].prototype;
        for (int i = 0; i < protos.Length; i++) if (i != main && protos[i].prototype == prefab) return i;
        var src = protos[main];
        var p = new DetailPrototype
        {
            prototype = prefab, usePrototypeMesh = true, renderMode = src.renderMode, useInstancing = src.useInstancing,
            minWidth = 0.35f, maxWidth = 0.55f, minHeight = 0.16f, maxHeight = 0.3f,
            noiseSpread = src.noiseSpread, density = src.density, alignToGround = src.alignToGround,
            healthyColor = src.healthyColor, dryColor = src.dryColor,
            holeEdgePadding = src.holeEdgePadding, positionJitter = src.positionJitter, targetCoverage = src.targetCoverage,
            useDensityScaling = src.useDensityScaling,
        };
        var list = new List<DetailPrototype>(protos) { p };
        td.detailPrototypes = list.ToArray();
        return list.Count - 1;
    }

    private static List<Building> Buildings()
    {
        var list = new List<Building>();
        var zone = GameObject.Find("Map_Blockout/Zone_South_RuinedVillage").transform;
        foreach (var group in new[] { "Houses", "Landmarks", "Farms" })
        {
            var g = zone.Find(group); if (g == null) continue;
            foreach (Transform c in g)
            {
                var rs = c.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                list.Add(new Building { r = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z), c = new Vector2(b.center.x, b.center.z) });
            }
        }
        return list;
    }

    private struct Line { public List<Vector2> pts; public float w; }

    private static void ReadNetwork(out List<Line> roads, out List<Line> paths, out List<Vector2> doors, out List<Vector2> juncs)
    {
        roads = new List<Line>(); paths = new List<Line>(); doors = new List<Vector2>(); juncs = new List<Vector2>();
        string json = File.ReadAllText(RoadsJson);
        foreach (Match m in Regex.Matches(json, "\"kind\":\\s*\"(\\w+)\",\\s*\"width\":\\s*([\\d.]+),\\s*\"pts\":\\s*\\[(.*?)\\]\\s*\\}", RegexOptions.Singleline))
        {
            var pts = new List<Vector2>();
            foreach (Match q in Regex.Matches(m.Groups[3].Value, "\\[\\s*(-?[\\d.]+),\\s*(-?[\\d.]+)\\s*\\]"))
                pts.Add(new Vector2(float.Parse(q.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(q.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
            if (pts.Count < 2) continue;
            var line = new Line { pts = pts, w = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) };
            if (m.Groups[1].Value == "road") roads.Add(line); else paths.Add(line);
        }
        foreach (var p in paths)
        {
            Vector2 a = p.pts[0], b = p.pts[p.pts.Count - 1];
            float da = 99f, db = 99f;
            foreach (var r in roads) { da = Mathf.Min(da, PolyDist(r.pts, a)); db = Mathf.Min(db, PolyDist(r.pts, b)); }
            doors.Add(da > db ? a : b); juncs.Add(da > db ? b : a);
        }
        // road endpoints that meet another road = crossings
        foreach (var r in roads)
            foreach (var e in new[] { r.pts[0], r.pts[r.pts.Count - 1] })
                foreach (var o in roads) if (!ReferenceEquals(o.pts, r.pts) && PolyDist(o.pts, e) < 4f) { juncs.Add(e); break; }
    }

    private static float PolyDist(List<Vector2> pts, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i], ab = pts[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            best = Mathf.Min(best, (a + ab * t - p).sqrMagnitude);
        }
        return Mathf.Sqrt(best);
    }

    private static float RectDist(Rect r, Vector2 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float Fbm(Vector2 p, int oct)
    {
        float s = 0f, a = 0.5f, n = 0f;
        for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(p.x + 137.1f * i + 1000f, p.y + 71.3f * i + 1000f); n += a; p *= 2.03f; a *= 0.5f; }
        return s / n;
    }

    private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 78.233f) * 43758.5453f, 1f);

    // 0 below a, ramps to 1 at b, back to 0 at c
    private static float Bump(float x, float a, float b, float c) => x < b ? Mathf.InverseLerp(a, b, x) : 1f - Mathf.InverseLerp(b, c, x);

    // backup format: x0 z0 w h layers, alpha floats, then CC_GrassClump (detail layer 1) ints
    private static void Save(TerrainData td, int x0, int z0, int w, int h)
    {
        var a = td.GetAlphamaps(x0, z0, w, h); var g = td.GetDetailLayer(x0, z0, w, h, 1); int L = td.alphamapLayers;
        using var bw = new BinaryWriter(File.Create(BackupPath));
        bw.Write(x0); bw.Write(z0); bw.Write(w); bw.Write(h); bw.Write(L);
        for (int z = 0; z < h; z++) for (int x = 0; x < w; x++) for (int k = 0; k < L; k++) bw.Write(a[z, x, k]);
        for (int z = 0; z < h; z++) for (int x = 0; x < w; x++) bw.Write(g[z, x]);
    }

    private static void Load(TerrainData td, out int x0, out int z0, out int w, out int h, out float[,,] a, out int[,] g)
    {
        using var br = new BinaryReader(File.OpenRead(BackupPath));
        x0 = br.ReadInt32(); z0 = br.ReadInt32(); w = br.ReadInt32(); h = br.ReadInt32(); int L = br.ReadInt32();
        a = new float[h, w, L]; g = new int[h, w];
        for (int z = 0; z < h; z++) for (int x = 0; x < w; x++) for (int k = 0; k < L; k++) a[z, x, k] = br.ReadSingle();
        for (int z = 0; z < h; z++) for (int x = 0; x < w; x++) g[z, x] = br.ReadInt32();
    }
}
