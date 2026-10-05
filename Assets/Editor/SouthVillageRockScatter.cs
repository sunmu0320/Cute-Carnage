using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Rock dressing for the south village (Stone1 = blocky hero cluster, Stone2 = low cluster with a mossy skirt).
// Owns only Zone_South_RuinedVillage/SouthVillage_Rocks: every run deletes that object and rebuilds it with a fixed seed,
// so re-running never duplicates and never touches anything else. Delete SouthVillage_Rocks to revert.
// Prefab assets are not modified; small rocks drop their collider as an instance override.
public static class SouthVillageRockScatter
{
    private const string Stone1 = "Assets/Prefabs/Props/Stone1.prefab";
    private const string Stone2 = "Assets/Prefabs/Props/Stone2.prefab";
    private const string RoadsJson = "Assets/Art/MapReference/Source~/village_roads.json";
    private const float ColliderMinScale = 0.45f;   // below this the rock is knee-high decoration: no collider
    private const int Seed = 11;

    private class House { public Transform t; public Vector3 c, half; public Vector2 door; public bool landmark; }
    private struct Disc { public Vector2 p; public float r; }

    private static GameObject s1, s2;
    private static Terrain terrain;
    private static List<House> houses;
    private static List<Vector2> doors, juncs, existing;
    private static List<Disc> placed;
    private static int roadLayer;
    private static System.Random rng;
    private static float Rand() => (float)rng.NextDouble();

    [MenuItem("Tools/Map/8. Scatter South Village Rocks")]
    public static void MenuBuild() => Debug.Log(Build(null));

    // only: null = all zones, otherwise e.g. "Outskirts,Houses" (test passes)
    public static string Build(string only)
    {
        s1 = AssetDatabase.LoadAssetAtPath<GameObject>(Stone1); s2 = AssetDatabase.LoadAssetAtPath<GameObject>(Stone2);
        if (s1 == null || s2 == null) return "Stone1/Stone2 prefab missing";
        terrain = Terrain.activeTerrain; rng = new System.Random(Seed);
        var zone = GameObject.Find("Map_Blockout/Zone_South_RuinedVillage").transform;
        var old = zone.Find("SouthVillage_Rocks");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("SouthVillage_Rocks").transform; root.SetParent(zone, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Scatter South Village Rocks");

        Collect(zone);
        var counts = new Dictionary<string, int>();
        bool Want(string z) => only == null || only.Contains(z);
        if (Want("Outskirts")) counts["Outskirts"] = Outskirts(Group(root, "Outskirts"));
        if (Want("Church")) counts["Church"] = Church(Group(root, "Church"));
        if (Want("Houses")) counts["Houses"] = Houses(Group(root, "Houses"));
        if (Want("Farms")) counts["Farms"] = Farms(zone, Group(root, "Farms"));
        if (Want("Roadside")) counts["Roadside"] = Roadside(Group(root, "Roadside"));
        if (Want("Plaza")) counts["Plaza"] = Plaza(Group(root, "Roadside"));
        string msg = "[SouthVillageRocks] "; foreach (var kv in counts) msg += kv.Key + " " + kv.Value + "  ";
        return msg;
    }

    // ---- zones ---------------------------------------------------------------------------------------------------
    private static int Outskirts(Transform g)
    {
        // cluster sites: 12-32m from the nearest building, off roads/water/existing rocks, picked where low-frequency noise is high
        var sites = new List<Vector2>();
        for (int i = 0; i < 4000 && sites.Count < 13; i++)
        {
            var p = new Vector2(Mathf.Lerp(-172f, 138f, Rand()), Mathf.Lerp(-182f, -18f, Rand()));
            float dB = HouseDist(p);
            if (dB < 12f || dB > 32f || Excluded(p) || !Open(p, 4f, 9f)) continue;
            if (Mathf.PerlinNoise(p.x * 0.02f + 40f, p.y * 0.02f + 40f) < 0.5f) continue;
            bool far = true; foreach (var s in sites) if (Vector2.Distance(s, p) < 22f) far = false;
            if (far) sites.Add(p);
        }
        int n = 0;
        foreach (var s in sites)
        {
            int size = 2 + rng.Next(5) + (Rand() < 0.3f ? 2 : 0);   // 2..8, uneven
            n += Cluster(g, s, size, 0.55f, 0.9f, 3.5f);
        }
        // a few lone rocks between sites
        for (int i = 0, k = 0; i < 600 && k < 6; i++)
        {
            var p = new Vector2(Mathf.Lerp(-172f, 138f, Rand()), Mathf.Lerp(-182f, -18f, Rand()));
            float dB = HouseDist(p);
            if (dB < 10f || dB > 30f || Excluded(p) || !Open(p, 4f, 8f)) continue;
            if (Place(g, Rand() < 0.5f ? s1 : s2, p, Mathf.Lerp(0.35f, 0.6f, Rand()))) { n++; k++; }
        }
        return n;
    }

    private static int Church(Transform g)
    {
        int n = 0;
        foreach (var h in houses)
        {
            if (h.t == null || !h.t.name.StartsWith("Church")) continue;
            // back corners + one side, low density; the door side stays open
            var spots = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1.15f, -0.1f) };
            foreach (var sp in spots)
                if (Rand() < 0.8f) n += Cluster(g, AroundHouse(h, sp, 2.2f), 1 + rng.Next(3), 0.3f, 0.55f, 1.8f);
        }
        return n;
    }

    private static int Houses(Transform g)
    {
        int n = 0;
        foreach (var h in houses)
        {
            if (h.landmark || Rand() > 0.27f) continue;          // most houses stay clean
            // back corner or the back end of a side wall; try another spot if the first is blocked
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var spot = Rand() < 0.6f ? new Vector2(Rand() < 0.5f ? -1f : 1f, -1f) : new Vector2(Rand() < 0.5f ? -1.1f : 1.1f, -0.45f);
                var p = AroundHouse(h, spot, 1.6f);
                if (NarrowGap(p)) continue;
                int got = Cluster(g, p, 1 + rng.Next(3), 0.35f, 0.6f, 1.4f);
                n += got; if (got > 0) break;
            }
        }
        return n;
    }

    private static int Farms(Transform zone, Transform g)
    {
        var farms = new List<Transform>(); foreach (Transform f in zone.Find("Farms")) farms.Add(f);
        for (int i = farms.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (farms[i], farms[j]) = (farms[j], farms[i]); }   // visit in random order, stop at 4
        int n = 0, used = 0;
        foreach (var f in farms)
        {
            if (used >= 4) break;
            var rs = f.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var c = new Vector2(b.center.x, b.center.z);
            // the most open outer corner (farthest from other buildings/plots), nudged outside: stones thrown off the field
            Vector2 corner = Vector2.zero, outDir = Vector2.zero; float bestOpen = -1f;
            for (int k = 0; k < 4; k++)
            {
                var sgn = new Vector2((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f);
                var q = c + new Vector2(sgn.x * b.extents.x, sgn.y * b.extents.z) + sgn.normalized * 1.6f;
                float open = 1e6f;
                foreach (var h in houses) if (Vector2.Distance(new Vector2(h.c.x, h.c.z), c) > 1f) open = Mathf.Min(open, BoxDist(h, q));
                if (open > bestOpen && !Excluded(q) && juncDist(q) > 6f && RoadDist(q) > 2.5f) { bestOpen = open; corner = q; outDir = sgn.normalized; }
            }
            if (bestOpen < 1.5f) continue;
            int pile = 3 + rng.Next(3);
            int got = 0;
            for (int i = 0; i < pile * 4 && got < pile; i++)
            {
                var p = corner + Jit(1.1f) + outDir * Rand() * 0.8f;
                if (b.Contains(new Vector3(p.x, b.center.y, p.y))) continue;     // never inside the field
                if (Place(g, Rand() < 0.65f ? s2 : s1, p, Mathf.Lerp(0.18f, 0.34f, Rand()), 0.55f)) got++;
            }
            if (got > 0) { used++; n += got; }
        }
        return n;
    }

    private static int Roadside(Transform g)
    {
        int n = 0;
        for (int i = 0; i < 3000 && n < 9; i++)
        {
            var p = new Vector2(Mathf.Lerp(-150f, 120f, Rand()), Mathf.Lerp(-160f, -15f, Rand()));
            float dr = RoadDist(p);
            if (dr < 2.5f || dr > 6f || juncDist(p) < 12f || HouseDist(p) < 4f || Excluded(p) || !Open(p, 3f, 10f)) continue;
            n += Cluster(g, p, 1 + (Rand() < 0.35f ? 1 : 0), 0.25f, 0.45f, 1.2f);
        }
        return n;
    }

    private static int Plaza(Transform g)
    {
        var f = GameObject.Find("WaterFountain"); if (f == null) return 0;
        var c = new Vector2(f.transform.position.x, f.transform.position.z);
        int n = 0;
        // two natural stones on the outer dirt/grass rim, in the sectors without lanes/benches (S-SW and E-SE)
        foreach (float a0 in new[] { -100f, -20f })
        {
            float a = (a0 + (Rand() - 0.5f) * 10f) * Mathf.Deg2Rad;
            var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12.5f;
            if (Place(g, s2, p, Mathf.Lerp(0.2f, 0.28f, Rand()))) n++;
        }
        return n;
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------
    // hero rock (Stone1, larger) + a few supporting Stone2 / small Stone1 around it, uneven spacing
    private static int Cluster(Transform g, Vector2 center, int count, float minS, float maxS, float spread)
    {
        int n = 0;
        float hero = Mathf.Lerp(minS, maxS, 0.6f + 0.4f * Rand());
        if (Place(g, Rand() < 0.75f ? s1 : s2, center, hero)) n++;
        for (int i = 0, tries = 0; i < count - 1 && tries < 24; tries++)
        {
            // supports lean against the hero (or the previous support), so the group reads as one mound
            float s = hero * Mathf.Lerp(0.4f, 0.75f, Rand());
            float a = Rand() * Mathf.PI * 2f, d = 1.75f * hero * (0.5f + 0.35f * Rand()) + 1.75f * s * 0.45f + (spread > 3f && Rand() < 0.25f ? 1.75f * hero : 0f);
            var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            if (Place(g, Rand() < 0.7f ? s2 : s1, p, Mathf.Max(minS * 0.6f, s), 0.55f)) { n++; i++; }
        }
        return n;
    }

    private static bool Place(Transform g, GameObject prefab, Vector2 p, float scale, float overlap = 0.25f)
    {
        float r = 1.75f * scale;
        if (Excluded(p) || HouseDist(p) < r * 0.6f + 0.4f) return false;
        foreach (var d in doors) if (Vector2.Distance(d, p) < 4.5f + r) return false;
        foreach (var e in existing) if (Vector2.Distance(e, p) < 6f) return false;
        foreach (var l in lanes) if (Seg(p, l[0], l[1]) < 2.2f + r) return false;          // fountain plaza lanes stay walkable
        foreach (var q in placed) if (Vector2.Distance(q.p, p) < (q.r + r) * (1f - overlap)) return false;
        if (RoadDist(p) < r + 0.8f) return false;
        // nothing solid (buildings, props, fountain) where the rock goes
        foreach (var col in Physics.OverlapSphere(new Vector3(p.x, Ground(p) + 0.8f, p.y), r * 0.8f, ~0, QueryTriggerInteraction.Ignore))
            if (!(col is TerrainCollider) && !col.transform.IsChildOf(g.parent)) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
        go.transform.localScale = Vector3.one * scale;                       // uniform only
        go.transform.rotation = Quaternion.Euler((Rand() - 0.5f) * 5f, Rand() * 360f, (Rand() - 0.5f) * 5f);
        go.transform.position = Vector3.zero;
        var b = Bounds(go);
        // sit the real mesh bottom on the lowest ground under the footprint, sunk a little (more on slopes)
        float gMin = float.MaxValue, gMax = float.MinValue;
        for (int i = 0; i < 9; i++)
        {
            var q = p + new Vector2((i % 3 - 1) * b.extents.x * 0.8f, (i / 3 - 1) * b.extents.z * 0.8f);
            float h = Ground(q); gMin = Mathf.Min(gMin, h); gMax = Mathf.Max(gMax, h);
        }
        float sink = (prefab == s1 ? 0.06f + 0.1f * scale : 0.03f) + (gMax - gMin);
        go.transform.position = new Vector3(p.x - b.center.x, gMin - sink - b.min.y, p.y - b.center.z);
        if (scale < ColliderMinScale)
            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);   // instance override only
        placed.Add(new Disc { p = p, r = r });
        return true;
    }

    private static void Collect(Transform zone)
    {
        houses = new List<House>(); placed = new List<Disc>(); existing = new List<Vector2>();
        foreach (var group in new[] { "Houses", "Landmarks" })
            foreach (Transform t in zone.Find(group))
            {
                // oriented box in the house's own frame
                Vector3 mn = Vector3.one * 1e6f, mx = -mn;
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    var lb = r.localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                        var l = t.InverseTransformPoint(r.transform.TransformPoint(corner));
                        mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l);
                    }
                }
                var sc = t.lossyScale;
                houses.Add(new House { t = t, c = t.TransformPoint((mn + mx) * 0.5f), half = Vector3.Scale((mx - mn) * 0.5f, sc), landmark = group == "Landmarks" });
            }
        // farms count as blocking boxes too
        foreach (Transform t in zone.Find("Farms"))
        {
            var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            houses.Add(new House { t = null, c = b.center, half = b.extents, landmark = true });
        }
        doors = new List<Vector2>(); juncs = new List<Vector2>(); roads = new List<List<Vector2>>(); roadW = new List<float>();
        string json = System.IO.File.ReadAllText(RoadsJson);
        var paths = new List<List<Vector2>>();
        foreach (Match m in Regex.Matches(json, "\"kind\":\\s*\"(\\w+)\",\\s*\"width\":\\s*([\\d.]+),\\s*\"pts\":\\s*\\[(.*?)\\]\\s*\\}", RegexOptions.Singleline))
        {
            var pts = new List<Vector2>();
            foreach (Match q in Regex.Matches(m.Groups[3].Value, "\\[\\s*(-?[\\d.]+),\\s*(-?[\\d.]+)\\s*\\]"))
                pts.Add(new Vector2(float.Parse(q.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(q.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
            if (pts.Count < 2) continue;
            if (m.Groups[1].Value == "road") { roads.Add(pts); roadW.Add(float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)); }
            else paths.Add(pts);
        }
        foreach (var pth in paths)
        {
            Vector2 a = pth[0], b = pth[pth.Count - 1];
            if (RoadDist(a) > RoadDist(b)) { doors.Add(a); juncs.Add(b); } else { doors.Add(b); juncs.Add(a); }
        }
        foreach (var r in roads) foreach (var e in new[] { r[0], r[r.Count - 1] }) juncs.Add(e);
        foreach (var h in houses)
        {
            if (h.t == null) continue;
            var c = new Vector2(h.c.x, h.c.z); Vector2 best = c + Vector2.down; float bd = 1e6f;
            foreach (var d in doors) { float dd = Vector2.Distance(d, c); if (dd < bd) { bd = dd; best = d; } }
            h.door = (best - c).normalized;
        }
        lanes = new List<Vector2[]>();
        var plaza = Object.FindFirstObjectByType<FountainPlazaDecor>();
        if (plaza != null && plaza.fountain != null)
            foreach (var e in plaza.laneEnds) lanes.Add(new[] { new Vector2(plaza.fountain.position.x, plaza.fountain.position.z), e });
        var feat = GameObject.Find("Map_Blockout/Terrain_Features");
        foreach (var n in new[] { "RimRocks", "DecorRocks", "BankCliffRocks" })
        {
            var g = feat.transform.Find(n); if (g == null) continue;
            foreach (Transform t in g) existing.Add(new Vector2(t.position.x, t.position.z));
        }
        var td = terrain.terrainData; roadLayer = -1;
        for (int i = 0; i < td.terrainLayers.Length; i++) if (td.terrainLayers[i] != null && td.terrainLayers[i].name == "TL_Road") roadLayer = i;
    }

    private static List<List<Vector2>> roads; private static List<float> roadW; private static List<Vector2[]> lanes;

    private static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    // point next to a house, in its frame: x = -1..1 across the width, y = -1 (back) .. 1 (door side)
    private static Vector2 AroundHouse(House h, Vector2 local, float gap)
    {
        Vector2 fwd = h.door, right = new Vector2(fwd.y, -fwd.x);
        // half sizes along the house frame, projected from the oriented box
        Vector3 rx = h.t.right * h.half.x, rz = h.t.forward * h.half.z;
        float alongR = Mathf.Abs(Vector2.Dot(new Vector2(rx.x, rx.z), right)) + Mathf.Abs(Vector2.Dot(new Vector2(rz.x, rz.z), right));
        float alongF = Mathf.Abs(Vector2.Dot(new Vector2(rx.x, rx.z), fwd)) + Mathf.Abs(Vector2.Dot(new Vector2(rz.x, rz.z), fwd));
        var c = new Vector2(h.c.x, h.c.z);
        return c + right * (local.x * (alongR + gap * Mathf.Abs(Mathf.Sign(local.x)))) + fwd * (local.y * (alongF + gap)) + Jit(0.5f);
    }

    private static float HouseDist(Vector2 p)
    {
        float best = 1e6f;
        foreach (var h in houses) best = Mathf.Min(best, BoxDist(h, p));
        return best;
    }

    private static float BoxDist(House h, Vector2 p)
    {
        Vector3 l = h.t != null ? h.t.InverseTransformPoint(new Vector3(p.x, h.c.y, p.y)) - h.t.InverseTransformPoint(h.c) : new Vector3(p.x - h.c.x, 0f, p.y - h.c.z);
        if (h.t != null) l = Vector3.Scale(l, h.t.lossyScale);
        float dx = Mathf.Max(Mathf.Abs(l.x) - h.half.x, 0f), dz = Mathf.Max(Mathf.Abs(l.z) - h.half.z, 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // an alley: two different buildings both close to the point
    private static bool NarrowGap(Vector2 p)
    {
        int near = 0;
        foreach (var h in houses)
        {
            var c = new Vector2(h.c.x, h.c.z);
            if (Vector2.Distance(c, p) < Mathf.Max(h.half.x, h.half.z) + 4.5f) near++;
        }
        return near >= 2;
    }

    private static float RoadDist(Vector2 p)
    {
        float best = 1e6f;
        for (int k = 0; k < roads.Count; k++)
        {
            var pts = roads[k];
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t) - roadW[k] * 0.5f);
            }
        }
        // hand-painted roads may differ from the json: also respect the painted road layer
        if (roadLayer >= 0 && RoadPaint(p) > 0.3f) best = Mathf.Min(best, 0f);
        return best;
    }

    private static float RoadPaint(Vector2 p)
    {
        var td = terrain.terrainData; Vector3 o = terrain.transform.position; int res = td.alphamapResolution;
        int x = Mathf.Clamp(Mathf.RoundToInt((p.x - o.x) / td.size.x * (res - 1)), 0, res - 1), z = Mathf.Clamp(Mathf.RoundToInt((p.y - o.z) / td.size.z * (res - 1)), 0, res - 1);
        return td.GetAlphamaps(x, z, 1, 1)[0, 0, roadLayer];
    }

    private static float juncDist(Vector2 p) { float b = 1e6f; foreach (var j in juncs) b = Mathf.Min(b, Vector2.Distance(j, p)); return b; }

    // open ground: no painted road within `road` m and no existing border rock within `rock` m
    private static bool Open(Vector2 p, float road, float rock)
    {
        if (RoadDist(p) < road) return false;
        foreach (var e in existing) if (Vector2.Distance(e, p) < rock) return false;
        var td = terrain.terrainData; Vector3 o = terrain.transform.position;
        return td.GetSteepness((p.x - o.x) / td.size.x, (p.y - o.z) / td.size.z) < 18f;
    }

    private static bool Excluded(Vector2 p)
    {
        if (p.y > -12f) return true;                                   // hub road / HomeBase side
        if (p.x > 92f && p.y > -42f) return true;                      // east factory
        if (p.x < -92f && p.y > -16f) return true;                     // west mall
        var f = GameObject.Find("WaterFountain");
        if (f != null && Vector2.Distance(p, new Vector2(f.transform.position.x, f.transform.position.z)) < 11.5f) return true;
        return Ground(p) < -0.6f;                                      // river bed / banks
    }

    private static Transform Group(Transform root, string name)
    {
        var t = root.Find(name); if (t != null) return t;
        t = new GameObject(name).transform; t.SetParent(root, false); return t;
    }

    private static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Vector2 Jit(float a) => new Vector2((Rand() - 0.5f) * 2f * a, (Rand() - 0.5f) * 2f * a);
    private static float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
}
