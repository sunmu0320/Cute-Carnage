using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Greybox map builders, one menu per step. Each step skips itself if its output already exists. Ctrl+Z undoes a whole step.
public static class MapGreyboxBuilder
{
    private const string MaterialDir = "Assets/Art/Materials/Graybox/";

    // ---------------------------------------------------------------- Step 1: hub plaza + roads

    private const float PlazaRadius = 26f;   // HomeBase structures reach ~17m; matches existing Road_To_South start (~27m).
    private const float RoadWidth = 7f;      // >= 6m so vehicles (planned) fit later.
    private const float RoadY = 0.02f;       // same height as Road_To_South.
    private const float StopShortOfHit = 1f; // leave a gap before the first building/tree in the way.

    private static readonly (string road, string zone)[] Routes =
    {
        ("Road_To_North", "Zone_North_Forest"),
        ("Road_To_West", "Zone_West_MallStore"),
        ("Road_To_East", "Zone_East_RuinedFactory"),
    };

    [MenuItem("Tools/Map/1. Build Hub Plaza + Roads")]
    private static void BuildHubRoads()
    {
        GameObject homeBase = GameObject.Find("HomeBase");
        Transform blockout = GameObject.Find("Map_Blockout")?.transform;
        Transform roads = blockout != null ? blockout.Find("Roads") : null;
        Material roadMat = LoadMat("Mat_Graybox_Road");
        if (homeBase == null || roads == null || roadMat == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing HomeBase / Map_Blockout/Roads / road material. Nothing built.");
            return;
        }

        Undo.SetCurrentGroupName("Build Hub Plaza + Roads");
        Vector3 center = homeBase.transform.position;
        center.y = 0f;

        if (roads.Find("Hub_Plaza") == null)
        {
            GameObject plaza = CreatePrimitive(PrimitiveType.Cylinder, "Hub_Plaza", roads, roadMat, keepCollider: false);
            plaza.transform.position = new Vector3(center.x, 0.005f, center.z);
            plaza.transform.localScale = new Vector3(PlazaRadius * 2f, 0.005f, PlazaRadius * 2f);
        }

        Physics.SyncTransforms();
        foreach ((string roadName, string zoneName) in Routes)
        {
            Transform zone = blockout.Find(zoneName);
            if (roads.Find(roadName) != null || zone == null)
            {
                Debug.Log($"[MapGreyboxBuilder] {roadName} skipped (exists or {zoneName} missing).");
                continue;
            }

            Vector3 target = zone.position;
            target.y = 0f;
            Vector3 dir = (target - center).normalized;
            Vector3 start = center + dir * PlazaRadius;
            float length = Vector3.Distance(start, target);

            // Stop at the first solid collider on the centerline (e.g. Mall facade, first trees) instead of running through it.
            if (Physics.Raycast(start + Vector3.up, dir, out RaycastHit hit, length, ~0, QueryTriggerInteraction.Ignore))
            {
                length = Mathf.Max(hit.distance - StopShortOfHit, 1f);
                Debug.Log($"[MapGreyboxBuilder] {roadName} stops at '{hit.collider.name}'.");
            }

            Strip(roadName, roads, roadMat, start, start + dir * length, RoadWidth, RoadY);
            Debug.Log($"[MapGreyboxBuilder] {roadName}: {length:0.#}m, width {RoadWidth}m.");
        }
    }

    // ---------------------------------------------------------------- Step 2: West mall (1F, enterable, no roof yet)

    // World-space layout (x, z). Road_To_West ends at x≈-97 on z=28.
    // Building footprint x -185..-125, z 5.5..50.5. Entrance faces east toward the road.
    private const float MallWallHeight = 5f;
    private const float WallThickness = 0.5f;

    [MenuItem("Tools/Map/2. Build West Mall (1F)")]
    private static void BuildWestMall()
    {
        Transform zone = GameObject.Find("Map_Blockout")?.transform.Find("Zone_West_MallStore");
        Material mallMat = LoadMat("Mat_Graybox_Mall");
        Material roadMat = LoadMat("Mat_Graybox_Road");
        Material propMat = LoadMat("Mat_Graybox_Boundary");
        if (zone == null || mallMat == null || roadMat == null || propMat == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout/Zone_West_MallStore or graybox materials. Nothing built.");
            return;
        }
        if (zone.Find("Mall_1F") != null)
        {
            Debug.Log("[MapGreyboxBuilder] Mall_1F already exists, skipped.");
            return;
        }

        Undo.SetCurrentGroupName("Build West Mall (1F)");

        // The old solid placeholder block is replaced by the enterable 1F.
        Transform oldMall = zone.Find("Mall");
        if (oldMall != null)
        {
            Undo.DestroyObjectImmediate(oldMall.gameObject);
        }

        Transform mall = CreateGroup("Mall_1F", zone);
        Transform walls = CreateGroup("Walls", mall);
        Transform interior = CreateGroup("Interior", mall);
        Transform outside = CreateGroup("Outside", zone);
        Transform cars = CreateGroup("Cars", outside);

        Flat("Floor", mall, roadMat, -155f, 28f, 60f, 45f, 0.03f);

        // Outer walls. East = entrance (10m gap at z 23..33), West = loading door (5m gap at z 37.5..42.5).
        Box("Wall_North", walls, mallMat, -155f, 50.5f, 60f, WallThickness, MallWallHeight);
        Box("Wall_South", walls, mallMat, -155f, 5.5f, 60f, WallThickness, MallWallHeight);
        Box("Wall_East_A", walls, mallMat, -125f, 14.25f, WallThickness, 17.5f, MallWallHeight);
        Box("Wall_East_B", walls, mallMat, -125f, 41.75f, WallThickness, 17.5f, MallWallHeight);
        Box("Wall_West_A", walls, mallMat, -185f, 21.5f, WallThickness, 32f, MallWallHeight);
        Box("Wall_West_B", walls, mallMat, -185f, 46.5f, WallThickness, 8f, MallWallHeight);

        // Back storage room (x -185..-172), 4m door aligned with the loading door.
        Box("Storage_Wall_A", walls, mallMat, -172f, 21f, WallThickness, 31f, MallWallHeight);
        Box("Storage_Wall_B", walls, mallMat, -172f, 46.25f, WallThickness, 8.5f, MallWallHeight);

        // Checkout counters just inside the entrance, main aisle kept clear on z=28.
        float[] counterZ = { 12f, 18f, 38f, 44f };
        for (int i = 0; i < counterZ.Length; i++)
        {
            Box($"Checkout_{i}", interior, propMat, -132f, counterZ[i], 3f, 1f, 1f);
        }

        // Shelf rows running east-west; 8m main aisle between z 24 and 32.
        float[] shelfZ = { 10f, 16f, 22f, 34f, 40f, 46f };
        for (int i = 0; i < shelfZ.Length; i++)
        {
            Box($"Shelf_{i}", interior, propMat, -155f, shelfZ[i], 24f, 1.2f, 2f);
        }

        // Outside: parking lot joined to the road end, loading dock, warehouse, water tower landmark.
        Flat("ParkingLot", outside, roadMat, -111f, 28f, 28f, 50f, 0.02f);
        Box("LoadingDock", outside, propMat, -190f, 40f, 10f, 8f, 1f);
        Box("Warehouse", outside, mallMat, -193f, 14f, 12f, 14f, 6f);
        Cylinder("WaterTower", outside, propMat, -190f, 62f, 6f, 12f);

        // Abandoned cars (x, z, yaw). Cover now, Scrap/Food loot spots later.
        (float x, float z, float yaw)[] carSpots =
        {
            (-118f, 10f, 0f), (-114f, 10f, 5f), (-106f, 12f, 80f), (-118f, 18f, -8f),
            (-104f, 20f, 0f), (-118f, 38f, 3f), (-110f, 40f, 95f), (-104f, 44f, -10f),
            (-114f, 47f, 0f), (-100f, 36f, 30f),
        };
        for (int i = 0; i < carSpots.Length; i++)
        {
            Box($"Car_{i}", cars, propMat, carSpots[i].x, carSpots[i].z, 2f, 4.5f, 1.5f, carSpots[i].yaw);
        }

        Debug.Log("[MapGreyboxBuilder] West mall 1F built. Roof/floors/interior art are later steps.");
    }

    // ---------------------------------------------------------------- Step 3: East factory compound

    // World-space layout (x, z). Road_To_East runs along z=28 up to x=150, straight into the hall's west door.
    // Fenced compound x 104..192, z -30..46: sits south of the east river and north of the south branch
    // (>= 5.9m clear of the reference water, see MapTerrainMask). Gate on the west side where the road enters.
    // Main hall is enterable (no roof yet), same as Mall_1F; other buildings are solid for now.
    // Rerunning replaces the whole zone.
    private const float FenceHeight = 2.5f;
    private const float FenceThickness = 0.3f;
    private const float HallWallHeight = 6f;

    [MenuItem("Tools/Map/3. Build East Factory")]
    private static void BuildEastFactory()
    {
        Transform zone = GameObject.Find("Map_Blockout")?.transform.Find("Zone_East_RuinedFactory");
        Material factoryMat = LoadMat("Mat_Graybox_Factory");
        Material roadMat = LoadMat("Mat_Graybox_Road");
        Material propMat = LoadMat("Mat_Graybox_Boundary");
        if (zone == null || factoryMat == null || roadMat == null || propMat == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout/Zone_East_RuinedFactory or graybox materials. Nothing built.");
            return;
        }

        Undo.SetCurrentGroupName("Build East Factory");
        ClearChildren(zone);

        Transform compound = CreateGroup("Factory_Compound", zone);
        Transform fence = CreateGroup("Fence", compound);
        Transform hall = CreateGroup("MainHall_1F", compound);
        Transform hallWalls = CreateGroup("Walls", hall);
        Transform hallInterior = CreateGroup("Interior", hall);
        Transform buildings = CreateGroup("Buildings", compound);
        Transform props = CreateGroup("Props", compound);

        Flat("Yard", compound, roadMat, 148f, 8f, 88f, 76f, 0.015f);

        // Perimeter fence: gate z 21..35 on the west (road), breaches on the west (z 0..6) and south (x 150..158).
        Box("Fence_West_A", fence, propMat, 104f, -15f, FenceThickness, 30f, FenceHeight);
        Box("Fence_West_B", fence, propMat, 104f, 13.5f, FenceThickness, 15f, FenceHeight);
        Box("Fence_West_C", fence, propMat, 104f, 40.5f, FenceThickness, 11f, FenceHeight);
        Box("Fence_North", fence, propMat, 148f, 46f, 88f, FenceThickness, FenceHeight);
        Box("Fence_East", fence, propMat, 192f, 8f, FenceThickness, 76f, FenceHeight);
        Box("Fence_South_A", fence, propMat, 127f, -30f, 46f, FenceThickness, FenceHeight);
        Box("Fence_South_B", fence, propMat, 175f, -30f, 34f, FenceThickness, FenceHeight);

        // Main hall x 150..190, z 8..42. West door (z 23..33) meets the road end, south door (x 165..175) faces the yard.
        Flat("Floor", hall, roadMat, 170f, 25f, 40f, 34f, 0.03f);
        Box("Wall_West_A", hallWalls, factoryMat, 150f, 15.5f, WallThickness, 15f, HallWallHeight);
        Box("Wall_West_B", hallWalls, factoryMat, 150f, 37.5f, WallThickness, 9f, HallWallHeight);
        Box("Wall_East", hallWalls, factoryMat, 190f, 25f, WallThickness, 34f, HallWallHeight);
        Box("Wall_South_A", hallWalls, factoryMat, 157.5f, 8f, 15f, WallThickness, HallWallHeight);
        Box("Wall_South_B", hallWalls, factoryMat, 182.5f, 8f, 15f, WallThickness, HallWallHeight);
        Box("Wall_North", hallWalls, factoryMat, 170f, 42f, 40f, WallThickness, HallWallHeight);

        // Machines in the corners, a press in the middle (cover), conveyor along the north wall; both door paths stay open.
        Box("Press", hallInterior, factoryMat, 172f, 25f, 6f, 6f, 4f);
        Box("Conveyor_0", hallInterior, propMat, 172f, 38f, 20f, 2f, 1.2f);
        (float x, float z)[] machines = { (156f, 13f), (186f, 13f), (186f, 34f), (156f, 38f) };
        for (int i = 0; i < machines.Length; i++)
        {
            Box($"Machine_{i}", hallInterior, factoryMat, machines[i].x, machines[i].z, 4f, 4f, 3f);
        }

        // Warehouses along the south fence (solid for now; one can become the collapsed-roof building later).
        Box("Warehouse_0", buildings, factoryMat, 116f, -20f, 18f, 14f, 7f);
        Box("Warehouse_1", buildings, factoryMat, 139f, -21f, 20f, 16f, 8f);
        Box("Warehouse_2", buildings, factoryMat, 175f, -19f, 22f, 14f, 6f);

        // Landmarks from the reference image: two chimneys, long boiler tube, storage tanks.
        // Chimneys and boiler sit at the north fence so the south-facing camera rarely has them in front of the player.
        Cylinder("Chimney_0", buildings, factoryMat, 145f, 37.5f, 3.5f, 30f);
        Cylinder("Chimney_1", buildings, factoryMat, 145f, 43.5f, 3.5f, 30f);
        GameObject boiler = Cylinder("BoilerTube", buildings, factoryMat, 125f, 41f, 6f, 30f);
        boiler.transform.SetPositionAndRotation(new Vector3(125f, 3f, 41f), Quaternion.Euler(0f, 0f, 90f)); // lying along x, 30m long.
        Cylinder("Tank_0", buildings, propMat, 116f, 8f, 8f, 10f);
        Cylinder("Tank_1", buildings, propMat, 128f, 8f, 8f, 10f);

        // Shipping containers: cover now, Scrap loot spots later.
        (float x, float z, float yaw)[] containers =
        {
            (108f, 0f, 0f), (160f, -5f, 90f), (140f, 12f, 90f), (186f, 2f, 0f), (132f, -6f, 0f),
        };
        for (int i = 0; i < containers.Length; i++)
        {
            Box($"Container_{i}", props, propMat, containers[i].x, containers[i].z, 2.5f, 6f, 2.6f, containers[i].yaw);
        }

        Debug.Log("[MapGreyboxBuilder] East factory built. Roof/interior art/collapsed warehouse are later steps.");
    }

    // ---------------------------------------------------------------- Step 4: North forest

    // World-space layout (x, z). Road_To_North ends near (-2, 150).
    // Trees fill the north band but skip reference water (+ margin), the outer rim, trails and clearings.
    // Trees are solid trunks (dense, blocks movement). Rerunning replaces the whole zone.
    private const float ForestMinX = -200f, ForestMaxX = 196f, ForestMinZ = 95f, ForestMaxZ = 228f;
    private const float TreeSpacing = 9f;   // lower = denser + heavier scene.
    private const float TreeWaterMargin = 3f;
    private const float TrailWidth = 4f;
    private const int ForestSeed = 1234;    // fixed seed: rerunning gives the same forest.

    private static readonly Vector3 TrailHub = new Vector3(-2f, 0f, 158f);     // road terminus clearing
    private static readonly Vector3 LumberCamp = new Vector3(150f, 0f, 165f);  // NE forest corner, as in the reference image
    private static readonly Vector3 Cabin = new Vector3(-80f, 0f, 178f);       // lone cabin (reference image, top centre-left)

    private static readonly (Vector3 center, float radius)[] Clearings =
    {
        (TrailHub, 14f), (LumberCamp, 22f), (Cabin, 15f),
    };

    [MenuItem("Tools/Map/4. Build North Forest")]
    private static void BuildNorthForest()
    {
        Transform zone = GameObject.Find("Map_Blockout")?.transform.Find("Zone_North_Forest");
        Material forestMat = LoadMat("Mat_Graybox_Forest");
        Material roadMat = LoadMat("Mat_Graybox_Road");
        Material propMat = LoadMat("Mat_Graybox_Boundary");
        Material villageMat = LoadMat("Mat_Graybox_Village");
        TerrainMask mask = TerrainMask.Load();
        if (zone == null || forestMat == null || roadMat == null || propMat == null || villageMat == null || mask == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout/Zone_North_Forest, graybox materials or terrain mask. Nothing built.");
            return;
        }

        Undo.SetCurrentGroupName("Build North Forest");
        ClearChildren(zone);

        Transform forest = CreateGroup("Forest", zone);
        Transform trails = CreateGroup("Trails", forest);
        Transform trees = CreateGroup("Trees", forest);
        Transform camp = CreateGroup("LumberCamp", forest);
        Transform cabin = CreateGroup("Cabin", forest);
        Transform rocks = CreateGroup("Rocks", forest);

        // Trails branch from the road terminus to the camp (crosses the NE river: step 5 adds a footbridge) and the cabin.
        (Vector3 from, Vector3 to)[] trailSegments =
        {
            (new Vector3(-2f, 0f, 145f), TrailHub),
            (TrailHub, LumberCamp),
            (TrailHub, Cabin),
        };
        for (int i = 0; i < trailSegments.Length; i++)
        {
            Strip($"Trail_{i}", trails, roadMat, trailSegments[i].from, trailSegments[i].to, TrailWidth, 0.02f);
        }

        // Lumber camp: sawmill shed + log piles (future Wood loot spots).
        Box("Sawmill", camp, villageMat, LumberCamp.x + 10f, LumberCamp.z + 10f, 12f, 8f, 5f);
        Box("LogPile_0", camp, propMat, LumberCamp.x - 10f, LumberCamp.z - 8f, 8f, 1.5f, 1.2f);
        Box("LogPile_1", camp, propMat, LumberCamp.x - 10f, LumberCamp.z - 4f, 8f, 1.5f, 1.2f);
        Box("LogPile_2", camp, propMat, LumberCamp.x + 2f, LumberCamp.z - 12f, 8f, 1.5f, 1.2f, 20f);
        Box("Cabin_House", cabin, villageMat, Cabin.x, Cabin.z + 3f, 8f, 10f, 4f);

        // Boulders at clearing edges for cover/landmarks.
        (float x, float z, float size, float yaw)[] boulders =
        {
            (12f, 166f, 3f, 20f), (-14f, 150f, 2.5f, 45f), (168f, 155f, 3.5f, 10f),
            (40f, 170f, 2.5f, 60f), (-92f, 168f, 3f, 30f), (-66f, 188f, 2.5f, 75f),
        };
        for (int i = 0; i < boulders.Length; i++)
        {
            Box($"Rock_{i}", rocks, propMat, boulders[i].x, boulders[i].z, boulders[i].size, boulders[i].size, boulders[i].size * 0.7f, boulders[i].yaw);
        }

        // Scatter trunks with a minimum spacing, skipping clearings, trails and the Road_To_North corridor.
        var keepClear = new List<(Vector3 from, Vector3 to)>(trailSegments)
        {
            (new Vector3(-2f, 0f, ForestMinZ - 10f), new Vector3(-2f, 0f, 150f)),
        };
        // ponytail: O(n^2) dart throwing, fine for a few hundred trees; switch to a grid lookup if spacing drops a lot.
        var rng = new System.Random(ForestSeed);
        var placed = new List<Vector3>();
        for (int attempt = 0; attempt < 10000; attempt++)
        {
            var p = new Vector3(
                Mathf.Lerp(ForestMinX, ForestMaxX, (float)rng.NextDouble()), 0f,
                Mathf.Lerp(ForestMinZ, ForestMaxZ, (float)rng.NextDouble()));
            if (mask.Outside(p) || mask.DistanceToWater(p) < TreeWaterMargin || IsForestBlocked(p, keepClear)
                || placed.Exists(q => (q - p).sqrMagnitude < TreeSpacing * TreeSpacing))
            {
                continue;
            }

            placed.Add(p);
            float diameter = Mathf.Lerp(2f, 3.5f, (float)rng.NextDouble());
            float height = Mathf.Lerp(5f, 8f, (float)rng.NextDouble());
            Cylinder($"Tree_{placed.Count - 1}", trees, forestMat, p.x, p.z, diameter, height);
        }

        Debug.Log($"[MapGreyboxBuilder] North forest built: {placed.Count} trees. Run step 5 next for water/bridges.");
    }

    private static bool IsForestBlocked(Vector3 p, List<(Vector3 from, Vector3 to)> keepClear)
    {
        foreach ((Vector3 center, float radius) in Clearings)
        {
            if ((p - center).sqrMagnitude < radius * radius)
            {
                return true;
            }
        }

        // Keep trees clear of road/trails (road half-width + a trunk's width of margin).
        float clearance = RoadWidth * 0.5f + 3f;
        return keepClear.Exists(s => DistanceToSegment(p, s.from, s.to) < clearance);
    }

    // ---------------------------------------------------------------- Step 5: Terrain from the reference image

    // Water, waterfall and outer rim traced from the reference map image into MapTerrainMask.png
    // (red = water, green = outside the playable rim; world-aligned, see TerrainMask). Source + script in MapReference/Source~.
    // - Terrain: water dug RiverBedDepth down with a sloped bank, rim raised RimHeight up as a cliff plateau.
    // - Water: one plane at WaterY; the terrain hides it everywhere except the dug channels.
    // - Impassable water: invisible blocker boxes merged from a 2m grid; gaps only under auto-placed bridges.
    // - Bridges: wherever a road (Map_Blockout/Roads) or trail (Forest/Trails) crosses water.
    // - Rim rock ring + bank rocks (greybox boxes, prefab swap later).
    // Rerunning replaces Terrain_Features and resets the heightmap. Run steps 3 and 4 first.
    private const float RiverBedDepth = 1f;
    private const float BankSlopeIn = 2f;     // metres from the water edge to full depth
    private const float RimHeight = 8f;
    private const float RimSlope = 6f;        // metres from the rim edge to full height
    private const float WaterY = -0.35f;
    private const float BlockerCell = 2f;
    private const float BlockerMinDepthIn = 0.75f; // shoreline strip that stays walkable
    private const float BridgeOverhang = 3f;       // deck extends this far onto each bank
    private const int TerrainSeed = 4321;

    private static readonly Vector3 WaterfallTop = new Vector3(-116f, 0f, 206f);

    [MenuItem("Tools/Map/5. Build Terrain From Reference (water, bridges, rim)")]
    private static void BuildTerrainFromReference()
    {
        Transform blockout = GameObject.Find("Map_Blockout")?.transform;
        Transform boundary = blockout != null ? blockout.Find("Boundary") : null;
        Terrain terrain = GameObject.Find("Terrain")?.GetComponent<Terrain>();
        Material roadMat = LoadMat("Mat_Graybox_Road");
        Material propMat = LoadMat("Mat_Graybox_Boundary");
        Material villageMat = LoadMat("Mat_Graybox_Village");
        TerrainMask mask = TerrainMask.Load();
        if (boundary == null || terrain == null || terrain.terrainData == null || roadMat == null || propMat == null || villageMat == null || mask == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout/Boundary, 'Terrain', graybox materials or terrain mask. Nothing built.");
            return;
        }

        Transform old = blockout.Find("Terrain_Features");
        if (old != null && !EditorUtility.DisplayDialog("Rebuild terrain?",
                "Terrain was already built. Rebuilding resets the whole heightmap, including any hand sculpting.", "Rebuild", "Cancel"))
        {
            return;
        }

        Undo.SetCurrentGroupName("Build Terrain From Reference");
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        Transform features = CreateGroup("Terrain_Features", blockout);
        Transform bridges = CreateGroup("Bridges", features);
        Transform blockers = CreateGroup("WaterBlockers", features);
        Transform rimRocks = CreateGroup("RimRocks", features);
        Transform bankRocks = CreateGroup("BankRocks", features);

        ShapeTerrain(terrain, mask);
        RemoveTreesInWater(blockout, mask);

        Material waterMat = GetOrCreateWaterMaterial(roadMat);
        Flat("Water", features, waterMat, (TerrainMask.MinX + TerrainMask.MaxX) * 0.5f, (TerrainMask.MinZ + TerrainMask.MaxZ) * 0.5f,
            TerrainMask.MaxX - TerrainMask.MinX, TerrainMask.MaxZ - TerrainMask.MinZ, WaterY);

        GameObject fall = CreatePrimitive(PrimitiveType.Cube, "WaterfallFace", features, waterMat, keepCollider: false);
        fall.transform.position = new Vector3(WaterfallTop.x, (RimHeight + WaterY) * 0.5f, WaterfallTop.z);
        fall.transform.localScale = new Vector3(12f, RimHeight - WaterY, 0.3f);

        List<(Vector3 center, Vector3 forward, float length, float width)> decks = BuildBridges(bridges, blockout, mask, villageMat, propMat);
        BuildWaterBlockers(blockers, mask, decks);

        Physics.SyncTransforms();
        BuildRimRocks(rimRocks, boundary, mask, propMat);
        BuildBankRocks(bankRocks, boundary, blockout, mask, decks, propMat);

        Debug.Log($"[MapGreyboxBuilder] Terrain from reference built: {decks.Count} bridges. Save the scene AND the project (terrain data is an asset).");
    }

    // ---------------------------------------------------------------- Step 6: Paint terrain layers

    // Rule-based splat painting over the playable map; later rules paint over earlier ones.
    // Texels the user already hand-painted (base layer < 95%) are kept as they were — which also means a second run
    // keeps everything from the first run (effectively a no-op). To repaint, restore the pre-paint alphamap first.
    // Greybox ground planes (roads, plaza, trails, factory yard, parking) are deactivated, not deleted:
    // bridge detection in step 5 still reads their transforms.
    private const int PaintResolution = 1024; // ~1m per texel on the 1000m terrain
    private const string LayerDir = "Assets/Texture/";

    [MenuItem("Tools/Map/6. Paint Terrain")]
    private static void PaintTerrain()
    {
        Transform blockout = GameObject.Find("Map_Blockout")?.transform;
        Terrain terrain = GameObject.Find("Terrain")?.GetComponent<Terrain>();
        TerrainMask mask = TerrainMask.Load();
        if (blockout == null || terrain == null || mask == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout, 'Terrain' or terrain mask. Nothing painted.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int grass = LayerIndex(data, "TL_Grass"), light = LayerIndex(data, "TL_LightGrass"), dirt = LayerIndex(data, "TL_Dirt"),
            gravel = LayerIndex(data, "TL_Gravel"), road = LayerIndex(data, "TL_Road"), wet = LayerIndex(data, "TL_WetDirt");
        if (Mathf.Min(grass, Mathf.Min(light, Mathf.Min(dirt, Mathf.Min(gravel, Mathf.Min(road, wet))))) < 0)
        {
            Debug.LogError("[MapGreyboxBuilder] Terrain is missing one of the TL_* layers from Assets/Texture. Nothing painted.");
            return;
        }

        Undo.SetCurrentGroupName("Paint Terrain");
        Undo.RegisterCompleteObjectUndo(data, "Paint Terrain");

        int oldRes = data.alphamapResolution;
        float[,,] old = data.GetAlphamaps(0, 0, oldRes, oldRes);
        if (data.alphamapResolution != PaintResolution)
        {
            data.alphamapResolution = PaintResolution;
        }
        int res = data.alphamapResolution, layers = data.alphamapLayers;

        var strips = FindPathStrips(blockout);
        Transform plaza = blockout.Find("Roads/Hub_Plaza");
        Vector3 plazaCenter = plaza != null ? plaza.position : Vector3.zero;
        float plazaRadius = plaza != null ? plaza.lossyScale.x * 0.5f : 0f;

        Vector3 origin = terrain.transform.position, size = data.size;
        var map = new float[res, res, layers];
        var w = new float[layers];
        int kept = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = z / (float)(res - 1);
                var p = new Vector3(origin.x + u * size.x, 0f, origin.z + v * size.z);

                // Keep hand painting (bilinear from the old map, so an upscale doesn't go blocky).
                float fx = u * (oldRes - 1), fz = v * (oldRes - 1);
                int x0 = Mathf.Min((int)fx, oldRes - 2), z0 = Mathf.Min((int)fz, oldRes - 2);
                float tx = fx - x0, tz = fz - z0;
                float OldAt(int k) => Mathf.Lerp(Mathf.Lerp(old[z0, x0, k], old[z0, x0 + 1, k], tx), Mathf.Lerp(old[z0 + 1, x0, k], old[z0 + 1, x0 + 1, k], tx), tz);
                if (OldAt(0) < 0.95f)
                {
                    for (int k = 0; k < layers; k++) map[z, x, k] = OldAt(k);
                    kept++;
                    continue;
                }

                System.Array.Clear(w, 0, layers);
                w[0] = 1f;
                float n = Mathf.PerlinNoise(p.x * 0.035f + 100f, p.z * 0.035f + 100f);
                Blend(w, light, Mathf.SmoothStep(0f, 0.6f, (n - 0.45f) * 2.5f));

                if (mask.Outside(p))
                {
                    Blend(w, grass, 0.8f);
                    Blend(w, dirt, 0.3f * n);
                }
                else
                {
                    // Forest floor fades in over ~25m around z≈95 with a noisy edge (no straight seam).
                    float forest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 110f, p.z + (n - 0.5f) * 24f));
                    Blend(w, grass, 0.6f * forest);
                    Blend(w, dirt, Mathf.Clamp01((n - 0.3f) * 1.2f) * forest);
                }

                float steep = data.GetSteepness(u, v);
                Blend(w, gravel, Mathf.InverseLerp(20f, 40f, steep));

                Blend(w, gravel, RectFade(p, 104f, -30f, 192f, 46f, 2f));      // factory yard
                Blend(w, road, RectFade(p, -125f, 3f, -97f, 53f, 1.5f));       // mall parking
                Blend(w, road, RectFade(p, -185f, 5.5f, -125f, 50.5f, 0.5f));  // under the mall

                if (plaza != null)
                {
                    float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(plazaCenter.x, 0f, plazaCenter.z));
                    Blend(w, dirt, 1f - Mathf.InverseLerp(plazaRadius - 2f, plazaRadius + 1f, d));
                }

                foreach ((string name, Vector3 c, Vector3 f, float length, float width) in strips)
                {
                    float d = DistanceToSegment(p, c - f * (length * 0.5f), c + f * (length * 0.5f));
                    bool isRoad = name.StartsWith("Road_To_");
                    float half = width * 0.5f;
                    if (isRoad)
                    {
                        Blend(w, dirt, 1f - Mathf.InverseLerp(half, half + 1.5f, d));  // shoulder
                        Blend(w, road, 1f - Mathf.InverseLerp(half - 0.5f, half, d));
                    }
                    else
                    {
                        Blend(w, dirt, 1f - Mathf.InverseLerp(half - 0.5f, half + 1f, d));
                    }
                }

                float toWater = mask.Water(p) ? 0f : mask.DistanceToWater(p);
                Blend(w, wet, 1f - Mathf.InverseLerp(1f, 3f, toWater));

                for (int k = 0; k < layers; k++) map[z, x, k] = w[k];
            }
        }
        data.SetAlphamaps(0, 0, map);
        EditorUtility.SetDirty(data);

        HideGreyboxGround(blockout);
        Debug.Log($"[MapGreyboxBuilder] Terrain painted at {res}x{res} ({size.x / res:0.##}m/texel); {kept} hand-painted texels kept.");
    }

    // Lerps all weights toward `layer` by `amount` (keeps the sum at 1).
    private static void Blend(float[] w, int layer, float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (amount <= 0f) return;
        for (int k = 0; k < w.Length; k++) w[k] *= 1f - amount;
        w[layer] += amount;
    }

    // 1 inside the rectangle, fading to 0 over `fade` metres outside it.
    private static float RectFade(Vector3 p, float x0, float z0, float x1, float z1, float fade)
    {
        float dx = Mathf.Max(x0 - p.x, 0f, p.x - x1), dz = Mathf.Max(z0 - p.z, 0f, p.z - z1);
        return 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / fade);
    }

    private static int LayerIndex(TerrainData data, string layerName)
    {
        TerrainLayer[] all = data.terrainLayers;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && AssetDatabase.GetAssetPath(all[i]) == LayerDir + layerName + ".terrainlayer") return i;
        }
        return -1;
    }

    private static void HideGreyboxGround(Transform blockout)
    {
        var planes = new List<GameObject>();
        foreach (Transform parent in new[] { blockout.Find("Roads"), blockout.Find("Zone_North_Forest/Forest/Trails") })
        {
            if (parent == null) continue;
            foreach (Transform s in parent) planes.Add(s.gameObject);
        }
        planes.Add(blockout.Find("Zone_East_RuinedFactory/Factory_Compound/Yard")?.gameObject);
        planes.Add(blockout.Find("Zone_West_MallStore/Outside/ParkingLot")?.gameObject);
        foreach (GameObject go in planes)
        {
            if (go == null || !go.activeSelf) continue;
            Undo.RecordObject(go, "Paint Terrain");
            go.SetActive(false);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }
    }

    // Resets the heightmap to flat ground at y=0, then digs water and raises the rim.
    private static void ShapeTerrain(Terrain terrain, TerrainMask mask)
    {
        TerrainData data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Build Terrain From Reference");
        Undo.RecordObject(terrain.transform, "Build Terrain From Reference");

        // At least ~1m per heightmap sample so narrow river parts keep their shape.
        Vector3 size = data.size;
        int res = data.heightmapResolution;
        if (size.x / (res - 1) > 1.01f)
        {
            int wanted = Mathf.Min(4097, Mathf.NextPowerOfTwo(Mathf.CeilToInt(size.x)) + 1);
            data.heightmapResolution = wanted;
            data.size = size; // resolution change can rescale the terrain
            res = data.heightmapResolution;
        }
        if (size.y < RimHeight + RiverBedDepth + 1f)
        {
            Debug.LogWarning($"[MapGreyboxBuilder] Terrain height {size.y}m is too small for the rim; rim will be clipped.");
        }

        // Surface y=0 sits RiverBedDepth above the terrain's own base so the river can be dug below it.
        Vector3 pos = terrain.transform.position;
        terrain.transform.position = new Vector3(pos.x, -RiverBedDepth, pos.z);
        Vector3 origin = terrain.transform.position;

        var heights = new float[res, res];
        float step = size.x / (res - 1);
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                var p = new Vector3(origin.x + x * step, 0f, origin.z + z * size.z / (res - 1));
                float h = RiverBedDepth;
                if (mask.Water(p))
                {
                    h -= RiverBedDepth * Mathf.SmoothStep(0f, 1f, mask.DistanceToLand(p) / BankSlopeIn);
                }
                else if (mask.Outside(p))
                {
                    h += RimHeight * Mathf.SmoothStep(0f, 1f, mask.DistanceToInside(p) / RimSlope);
                }
                heights[z, x] = Mathf.Clamp01(h / size.y);
            }
        }
        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        Debug.Log($"[MapGreyboxBuilder] Terrain reshaped: res {res}, {step:0.##}m per sample, water -{RiverBedDepth}m, rim +{RimHeight}m.");
    }

    private static void RemoveTreesInWater(Transform blockout, TerrainMask mask)
    {
        Transform trees = blockout.Find("Zone_North_Forest/Forest/Trees");
        if (trees == null)
        {
            return;
        }

        int removed = 0;
        for (int i = trees.childCount - 1; i >= 0; i--)
        {
            Vector3 p = trees.GetChild(i).position;
            if (mask.Outside(p) || mask.DistanceToWater(p) < 2.5f)
            {
                Undo.DestroyObjectImmediate(trees.GetChild(i).gameObject);
                removed++;
            }
        }
        if (removed > 0)
        {
            Debug.Log($"[MapGreyboxBuilder] Removed {removed} trees standing in water or on the rim.");
        }
    }

    // A deck (walkable collider) + railings wherever a road/trail strip crosses water.
    private static List<(Vector3 center, Vector3 forward, float length, float width)> BuildBridges(
        Transform parent, Transform blockout, TerrainMask mask, Material deckMat, Material railMat)
    {
        var decks = new List<(Vector3 center, Vector3 forward, float length, float width)>();
        foreach ((string name, Vector3 center, Vector3 forward, float length, float width) in FindPathStrips(blockout))
        {
            // Walk the strip centreline and collect water runs.
            float runStart = float.NaN;
            for (float t = -length * 0.5f; t <= length * 0.5f + 0.25f; t += 0.5f)
            {
                bool wet = t <= length * 0.5f && mask.Water(center + forward * t);
                if (wet && float.IsNaN(runStart))
                {
                    runStart = t;
                }
                else if (!wet && !float.IsNaN(runStart))
                {
                    float a = runStart - BridgeOverhang, b = t + BridgeOverhang;
                    var deckCenter = center + forward * ((a + b) * 0.5f);
                    decks.Add((deckCenter, forward, b - a, width + 1f));
                    runStart = float.NaN;
                }
            }
        }

        for (int i = 0; i < decks.Count; i++)
        {
            (Vector3 c, Vector3 f, float length, float width) = decks[i];
            Transform bridge = CreateGroup($"Bridge_{i}", parent);
            Vector3 half = f * (length * 0.5f);
            Strip("Deck", bridge, deckMat, c - half, c + half, width, 0.06f);
            BoxCollider col = Undo.AddComponent<BoxCollider>(bridge.Find("Deck").gameObject);
            col.size = new Vector3(10f, 0.2f, 10f); // plane mesh is 10x10 before scale
            col.center = new Vector3(0f, -0.1f, 0f);

            Vector3 side = Vector3.Cross(Vector3.up, f) * (width * 0.5f);
            float yaw = Quaternion.LookRotation(f).eulerAngles.y;
            Vector3 l = c - side, r = c + side;
            Box("Railing_L", bridge, railMat, l.x, l.z, 0.3f, length, 1f, yaw);
            Box("Railing_R", bridge, railMat, r.x, r.z, 0.3f, length, 1f, yaw);
        }
        return decks;
    }

    // Roads and trails are flat planes: position = centre, local z = direction, scale x/z = width/length / 10.
    private static List<(string name, Vector3 center, Vector3 forward, float length, float width)> FindPathStrips(Transform blockout)
    {
        var strips = new List<(string, Vector3, Vector3, float, float)>();
        foreach (Transform parent in new[] { blockout.Find("Roads"), blockout.Find("Zone_North_Forest/Forest/Trails") })
        {
            if (parent == null) continue;
            foreach (Transform s in parent)
            {
                if (!s.name.StartsWith("Road_To_") && !s.name.StartsWith("Trail_")) continue;
                Vector3 c = s.position;
                c.y = 0f;
                strips.Add((s.name, c, s.forward, s.lossyScale.z * 10f, s.lossyScale.x * 10f));
            }
        }
        return strips;
    }

    // Invisible boxes over deep-enough water, merged from a 2m grid into rectangles; cells under a bridge deck stay open.
    private static void BuildWaterBlockers(Transform parent, TerrainMask mask, List<(Vector3 center, Vector3 forward, float length, float width)> decks)
    {
        int nx = Mathf.CeilToInt((TerrainMask.MaxX - TerrainMask.MinX) / BlockerCell);
        int nz = Mathf.CeilToInt((TerrainMask.MaxZ - TerrainMask.MinZ) / BlockerCell);
        var blocked = new bool[nz, nx];
        for (int z = 0; z < nz; z++)
        {
            for (int x = 0; x < nx; x++)
            {
                var p = new Vector3(TerrainMask.MinX + (x + 0.5f) * BlockerCell, 0f, TerrainMask.MinZ + (z + 0.5f) * BlockerCell);
                blocked[z, x] = mask.Water(p) && mask.DistanceToLand(p) >= BlockerMinDepthIn && !decks.Exists(d => OnDeck(p, d));
            }
        }

        // Greedy merge: horizontal runs, extended upward while the next row has the identical run.
        int count = 0;
        for (int z = 0; z < nz; z++)
        {
            for (int x = 0; x < nx; x++)
            {
                if (!blocked[z, x]) continue;
                int x1 = x;
                while (x1 + 1 < nx && blocked[z, x1 + 1]) x1++;
                int z1 = z;
                while (z1 + 1 < nz && RowFilled(blocked, z1 + 1, x, x1)) z1++;
                for (int zz = z; zz <= z1; zz++)
                    for (int xx = x; xx <= x1; xx++)
                        blocked[zz, xx] = false;

                float cx = TerrainMask.MinX + (x + x1 + 1) * 0.5f * BlockerCell;
                float cz = TerrainMask.MinZ + (z + z1 + 1) * 0.5f * BlockerCell;
                GameObject go = CreatePrimitive(PrimitiveType.Cube, $"Blocker_{count++}", parent, null, keepCollider: true);
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                go.transform.position = new Vector3(cx, 1f, cz); // spans y -1..3 (river bed to above head height)
                go.transform.localScale = new Vector3((x1 - x + 1) * BlockerCell, 4f, (z1 - z + 1) * BlockerCell);
            }
        }
        Debug.Log($"[MapGreyboxBuilder] Water blockers: {count} boxes.");
    }

    private static bool RowFilled(bool[,] grid, int z, int x0, int x1)
    {
        for (int x = x0; x <= x1; x++)
            if (!grid[z, x]) return false;
        return true;
    }

    private static bool OnDeck(Vector3 p, (Vector3 center, Vector3 forward, float length, float width) d)
    {
        Vector3 rel = p - d.center;
        Vector3 side = Vector3.Cross(Vector3.up, d.forward);
        return Mathf.Abs(Vector3.Dot(rel, d.forward)) <= d.length * 0.5f + BlockerCell
            && Mathf.Abs(Vector3.Dot(rel, side)) <= d.width * 0.5f + BlockerCell * 0.5f;
    }

    // Rock ring on the rim edge (the terrain slope alone may be climbable). South edge stays low for the camera.
    private static void BuildRimRocks(Transform parent, Transform boundary, TerrainMask mask, Material mat)
    {
        var rng = new System.Random(TerrainSeed);
        float Rand(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

        int placed = 0, skipped = 0;
        foreach (Vector3 p in mask.RimEdgePoints(6f))
        {
            float size = Rand(6f, 10f);
            float height = p.z < -100f ? Rand(2f, 3.5f) : Rand(5f, 9f);
            float yaw = Rand(0f, 90f);
            if (p.x < -202f || p.x > 198f || p.z < -172f || p.z > 229.6f || mask.DistanceToWater(p) < size * 0.5f
                || OverlapsBuilt(p, size, height, yaw, boundary))
            {
                skipped++;
                continue;
            }
            Box($"RimRock_{placed++}", parent, mat, p.x, p.z, size, size, height, yaw);
        }
        Debug.Log($"[MapGreyboxBuilder] Rim rocks: {placed} placed, {skipped} skipped (off-map, water or existing structures).");
    }

    // Grey rocks along river banks like the reference image; kept off bridges, roads and trails.
    private static void BuildBankRocks(Transform parent, Transform boundary, Transform blockout, TerrainMask mask,
        List<(Vector3 center, Vector3 forward, float length, float width)> decks, Material mat)
    {
        var rng = new System.Random(TerrainSeed + 1);
        float Rand(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());
        var strips = FindPathStrips(blockout);
        var placed = new List<Vector3>();

        List<Vector3> candidates = mask.BankPoints(1f, 2f);
        for (int i = candidates.Count - 1; i > 0; i--) // shuffle (seeded) so spacing isn't biased to one corner
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (Vector3 p in candidates)
        {
            if (placed.Exists(q => (q - p).sqrMagnitude < 49f)) continue;                     // ~7m apart
            if (decks.Exists(d => (d.center - p).magnitude < d.length * 0.5f + 5f)) continue; // bridge approaches
            if (strips.Exists(s => DistanceToSegment(p, s.center - s.forward * (s.length * 0.5f), s.center + s.forward * (s.length * 0.5f)) < s.width * 0.5f + 2f)) continue;

            float size = Rand(1.5f, 3.5f);
            float height = Rand(0.8f, 2.2f);
            float yaw = Rand(0f, 90f);
            if (OverlapsBuilt(p, size, height, yaw, boundary)) continue;

            Box($"BankRock_{placed.Count}", parent, mat, p.x, p.z, size, size, height, yaw);
            placed.Add(p);
        }
        Debug.Log($"[MapGreyboxBuilder] Bank rocks: {placed.Count}.");
    }

    // True if a box here would intersect anything already built (ignores boundary walls, trees and the terrain itself).
    private static bool OverlapsBuilt(Vector3 p, float size, float height, float yaw, Transform boundary)
    {
        // Lifted a little off y=0 so the terrain collider doesn't count as an overlap.
        var center = new Vector3(p.x, height * 0.5f + 0.2f, p.z);
        var half = new Vector3(size * 0.5f, Mathf.Max(0.05f, height * 0.5f - 0.2f), size * 0.5f);
        foreach (Collider hit in Physics.OverlapBox(center, half, Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit is TerrainCollider || hit.transform.IsChildOf(boundary) || hit.transform.parent?.name == "Trees")
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private static Material GetOrCreateWaterMaterial(Material template)
    {
        const string path = MaterialDir + "Mat_Graybox_Water.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(template) { color = new Color(0.2f, 0.45f, 0.75f) }; // same shader as the other graybox mats.
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static float DistanceToSegment(Vector3 p, Vector3 from, Vector3 to)
    {
        p.y = from.y = to.y = 0f;
        Vector3 seg = to - from;
        float t = Mathf.Clamp01(Vector3.Dot(p - from, seg) / seg.sqrMagnitude);
        return (p - (from + seg * t)).magnitude;
    }

    // World-aligned mask baked from the reference image (0.5m per pixel). Red = water, green = outside the rim.
    // Distances are chamfer approximations, good to a few %.
    private sealed class TerrainMask
    {
        public const string Path = "Assets/Art/MapReference/MapTerrainMask.png";
        public const float MinX = -205f, MaxX = 200f, MinZ = -175f, MaxZ = 232f, Cell = 0.5f;

        private int nx, nz;
        private bool[] water, outside;
        private float[] toLand, toWater, toInside;

        public static TerrainMask Load()
        {
            if (!System.IO.File.Exists(Path))
            {
                Debug.LogError($"[MapGreyboxBuilder] {Path} not found.");
                return null;
            }
            var tex = new Texture2D(2, 2);
            tex.LoadImage(System.IO.File.ReadAllBytes(Path)); // raw PNG read: import settings don't matter
            var m = new TerrainMask { nx = tex.width, nz = tex.height };
            Color32[] px = tex.GetPixels32(); // row 0 = south (PNG was written north-up)
            Object.DestroyImmediate(tex);
            m.water = new bool[px.Length];
            m.outside = new bool[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                m.water[i] = px[i].r > 127;
                m.outside[i] = px[i].g > 127;
            }
            m.toLand = Chamfer(m.water, m.nx, m.nz, invert: true);
            m.toWater = Chamfer(m.water, m.nx, m.nz, invert: false);
            m.toInside = Chamfer(m.outside, m.nx, m.nz, invert: true);
            return m;
        }

        // Off-mask points count as outside the rim.
        private int Index(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - MinX) / Cell), z = Mathf.FloorToInt((p.z - MinZ) / Cell);
            return x < 0 || z < 0 || x >= nx || z >= nz ? -1 : z * nx + x;
        }

        public bool Water(Vector3 p) { int i = Index(p); return i >= 0 && water[i]; }
        public bool Outside(Vector3 p) { int i = Index(p); return i < 0 || outside[i]; }
        public float DistanceToLand(Vector3 p) { int i = Index(p); return i < 0 ? 0f : toLand[i]; }
        public float DistanceToWater(Vector3 p) { int i = Index(p); return i < 0 ? float.MaxValue : toWater[i]; }
        public float DistanceToInside(Vector3 p) { int i = Index(p); return i < 0 ? RimSlope : toInside[i]; }

        // Points just inside the rim edge, roughly `spacing` apart along it.
        public IEnumerable<Vector3> RimEdgePoints(float spacing)
        {
            var taken = new List<Vector3>();
            for (int z = 0; z < nz; z += 2)
            {
                for (int x = 0; x < nx; x += 2)
                {
                    int i = z * nx + x;
                    if (outside[i] || !NextToOutside(x, z)) continue;
                    var p = new Vector3(MinX + (x + 0.5f) * Cell, 0f, MinZ + (z + 0.5f) * Cell);
                    if (taken.Exists(q => (q - p).sqrMagnitude < spacing * spacing)) continue;
                    taken.Add(p);
                    yield return p;
                }
            }
        }

        // Land points between minDist and maxDist from water (river banks).
        public List<Vector3> BankPoints(float minDist, float maxDist)
        {
            var list = new List<Vector3>();
            for (int z = 0; z < nz; z += 2)
            {
                for (int x = 0; x < nx; x += 2)
                {
                    int i = z * nx + x;
                    if (!water[i] && !outside[i] && toWater[i] >= minDist && toWater[i] <= maxDist)
                    {
                        list.Add(new Vector3(MinX + (x + 0.5f) * Cell, 0f, MinZ + (z + 0.5f) * Cell));
                    }
                }
            }
            return list;
        }

        private bool NextToOutside(int x, int z)
        {
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int xx = x + dx, zz = z + dz;
                    if (xx < 0 || zz < 0 || xx >= nx || zz >= nz || outside[zz * nx + xx]) return true;
                }
            return false;
        }

        // Two-pass chamfer distance in metres. invert=false: distance to the nearest set cell.
        // invert=true: distance from each set cell to the nearest unset cell (0 on unset cells).
        private static float[] Chamfer(bool[] set, int nx, int nz, bool invert)
        {
            const float big = 1e6f, d1 = Cell, d2 = Cell * 1.41421356f;
            var d = new float[set.Length];
            for (int i = 0; i < d.Length; i++)
            {
                bool seed = invert ? !set[i] : set[i];
                d[i] = seed ? 0f : big;
            }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * nx + x;
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i - 1] + d1);
                    if (z > 0) d[i] = Mathf.Min(d[i], d[i - nx] + d1);
                    if (x > 0 && z > 0) d[i] = Mathf.Min(d[i], d[i - nx - 1] + d2);
                    if (x < nx - 1 && z > 0) d[i] = Mathf.Min(d[i], d[i - nx + 1] + d2);
                }
            for (int z = nz - 1; z >= 0; z--)
                for (int x = nx - 1; x >= 0; x--)
                {
                    int i = z * nx + x;
                    if (x < nx - 1) d[i] = Mathf.Min(d[i], d[i + 1] + d1);
                    if (z < nz - 1) d[i] = Mathf.Min(d[i], d[i + nx] + d1);
                    if (x < nx - 1 && z < nz - 1) d[i] = Mathf.Min(d[i], d[i + nx + 1] + d2);
                    if (x > 0 && z < nz - 1) d[i] = Mathf.Min(d[i], d[i + nx - 1] + d2);
                }
            return d;
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");

    private static Transform CreateGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, name);
        return go.transform;
    }

    // Solid box standing on the ground: size (x, z) footprint, height up from y=0.
    private static GameObject Box(string name, Transform parent, Material mat, float x, float z, float sizeX, float sizeZ, float height, float yaw = 0f)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Cube, name, parent, mat, keepCollider: true);
        go.transform.SetPositionAndRotation(new Vector3(x, height * 0.5f, z), Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = new Vector3(sizeX, height, sizeZ);
        return go;
    }

    // Walkable ground strip (road/trail) between two points (no collider).
    private static void Strip(string name, Transform parent, Material mat, Vector3 from, Vector3 to, float width, float y)
    {
        from.y = to.y = 0f;
        Vector3 dir = to - from;
        GameObject go = CreatePrimitive(PrimitiveType.Plane, name, parent, mat, keepCollider: false);
        go.transform.SetPositionAndRotation((from + to) * 0.5f + Vector3.up * y, Quaternion.LookRotation(dir));
        go.transform.localScale = new Vector3(width / 10f, 1f, dir.magnitude / 10f); // Unity plane is 10x10.
    }

    // Upright cylinder standing on the ground (Unity cylinder is 2 units tall, so scale.y = height / 2).
    private static GameObject Cylinder(string name, Transform parent, Material mat, float x, float z, float diameter, float height)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Cylinder, name, parent, mat, keepCollider: true);
        go.transform.position = new Vector3(x, height * 0.5f, z);
        go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        return go;
    }

    // Walkable ground decal (no collider).
    private static void Flat(string name, Transform parent, Material mat, float x, float z, float sizeX, float sizeZ, float y)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Plane, name, parent, mat, keepCollider: false);
        go.transform.position = new Vector3(x, y, z);
        go.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
    }

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        if (!keepCollider)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        go.name = name;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.transform.SetParent(parent, true);
        Undo.RegisterCreatedObjectUndo(go, name);
        return go;
    }
}
