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

    // World-space layout (x, z). Road_To_East runs along z=28 up to x=150.
    // Fenced compound x 95..192, z -25..95 (boundary wall at x≈198). Gate on the west side where the road enters.
    // Main hall is enterable (no roof yet), same as Mall_1F; other buildings are solid for now.
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
        if (zone.Find("Factory_Compound") != null)
        {
            Debug.Log("[MapGreyboxBuilder] Factory_Compound already exists, skipped.");
            return;
        }

        Undo.SetCurrentGroupName("Build East Factory");

        // Old placeholder blocks/silos (incl. the three that sat outside the zone, north-east of base) are replaced.
        for (int i = zone.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(zone.GetChild(i).gameObject);
        }

        Transform compound = CreateGroup("Factory_Compound", zone);
        Transform fence = CreateGroup("Fence", compound);
        Transform hall = CreateGroup("MainHall_1F", compound);
        Transform hallWalls = CreateGroup("Walls", hall);
        Transform hallInterior = CreateGroup("Interior", hall);
        Transform buildings = CreateGroup("Buildings", compound);
        Transform props = CreateGroup("Props", compound);

        Flat("Yard", compound, roadMat, 143.5f, 35f, 97f, 120f, 0.015f);

        // Perimeter fence: gate z 21..35 on the west (road), plus three breaches (west, north, south) as alternate entries.
        Box("Fence_West_A", fence, propMat, 95f, -2f, FenceThickness, 46f, FenceHeight);
        Box("Fence_West_B", fence, propMat, 95f, 47.5f, FenceThickness, 25f, FenceHeight);
        Box("Fence_West_C", fence, propMat, 95f, 80.5f, FenceThickness, 29f, FenceHeight);
        Box("Fence_North_A", fence, propMat, 117.5f, 95f, 45f, FenceThickness, FenceHeight);
        Box("Fence_North_B", fence, propMat, 169f, 95f, 46f, FenceThickness, FenceHeight);
        Box("Fence_East", fence, propMat, 192f, 35f, FenceThickness, 120f, FenceHeight);
        Box("Fence_South_A", fence, propMat, 107.5f, -25f, 25f, FenceThickness, FenceHeight);
        Box("Fence_South_B", fence, propMat, 161f, -25f, 62f, FenceThickness, FenceHeight);

        // Main hall x 130..175, z 40..80. Big door south (x 145..155) facing the yard, side door west (z 58..62).
        Flat("Floor", hall, roadMat, 152.5f, 60f, 45f, 40f, 0.03f);
        Box("Wall_South_A", hallWalls, factoryMat, 137.5f, 40f, 15f, WallThickness, HallWallHeight);
        Box("Wall_South_B", hallWalls, factoryMat, 165f, 40f, 20f, WallThickness, HallWallHeight);
        Box("Wall_North", hallWalls, factoryMat, 152.5f, 80f, 45f, WallThickness, HallWallHeight);
        Box("Wall_West_A", hallWalls, factoryMat, 130f, 49f, WallThickness, 18f, HallWallHeight);
        Box("Wall_West_B", hallWalls, factoryMat, 130f, 71f, WallThickness, 18f, HallWallHeight);
        Box("Wall_East", hallWalls, factoryMat, 175f, 60f, WallThickness, 40f, HallWallHeight);

        // Two conveyor lines with a row of machines between them; 7m aisles either side of the machines.
        Box("Conveyor_0", hallInterior, propMat, 150f, 50f, 30f, 2f, 1.2f);
        Box("Conveyor_1", hallInterior, propMat, 150f, 70f, 30f, 2f, 1.2f);
        float[] machineX = { 138f, 146f, 158f, 166f };
        for (int i = 0; i < machineX.Length; i++)
        {
            Box($"Machine_{i}", hallInterior, factoryMat, machineX[i], 60f, 4f, 4f, 3f);
        }

        // Warehouses south of the road (solid for now; one can become the collapsed-roof building later).
        Box("Warehouse_0", buildings, factoryMat, 110f, 5f, 20f, 15f, 7f);
        Box("Warehouse_1", buildings, factoryMat, 140f, 0f, 25f, 18f, 8f);
        Box("Warehouse_2", buildings, factoryMat, 172f, 5f, 15f, 12f, 6f);

        // Landmarks from the reference image: two tall chimneys behind the hall, long boiler tube, storage tanks.
        // Chimneys sit on the north side so the south-facing camera never has them between it and the player.
        Cylinder("Chimney_0", buildings, factoryMat, 165f, 88f, 4f, 30f);
        Cylinder("Chimney_1", buildings, factoryMat, 173f, 88f, 4f, 30f);
        GameObject boiler = Cylinder("BoilerTube", buildings, factoryMat, 112f, 62f, 6f, 30f);
        boiler.transform.SetPositionAndRotation(new Vector3(112f, 3f, 62f), Quaternion.Euler(90f, 0f, 0f)); // lying along z, 30m long.
        Cylinder("Tank_0", buildings, propMat, 186f, 36f, 8f, 10f);
        Cylinder("Tank_1", buildings, propMat, 186f, 50f, 8f, 10f);

        // Shipping containers: cover now, Scrap loot spots later.
        (float x, float z, float yaw)[] containers =
        {
            (104f, -12f, 90f), (111f, -12f, 90f), (160f, -15f, 0f),
            (185f, 10f, 90f), (122f, 88f, 0f), (128f, 88f, 0f),
        };
        for (int i = 0; i < containers.Length; i++)
        {
            Box($"Container_{i}", props, propMat, containers[i].x, containers[i].z, 2.5f, 6f, 2.6f, containers[i].yaw);
        }

        Debug.Log("[MapGreyboxBuilder] East factory built. Roof/interior art/collapsed warehouse are later steps.");
    }

    // ---------------------------------------------------------------- Step 4: North forest

    // World-space layout (x, z). Road_To_North ends near (-2, 150).
    // Forest band x -185..185, z 100..222 (north wall z≈229; factory fence reaches z 95). Open meadow between base and forest.
    // Trees are solid trunks (dense, blocks movement); trails and clearings are kept free.
    private const float ForestMinX = -185f, ForestMaxX = 185f, ForestMinZ = 100f, ForestMaxZ = 222f;
    private const float TreeSpacing = 9f;   // ~400 trees. Lower = denser + heavier scene.
    private const float TrailWidth = 4f;
    private const int ForestSeed = 1234;    // fixed seed: rerunning after Ctrl+Z gives the same forest.

    private static readonly Vector3 TrailHub = new Vector3(-2f, 0f, 158f);     // road terminus clearing
    private static readonly Vector3 LumberCamp = new Vector3(70f, 0f, 150f);   // sawmill + log piles (reference image, top right)
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
        if (zone == null || forestMat == null || roadMat == null || propMat == null || villageMat == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout/Zone_North_Forest or graybox materials. Nothing built.");
            return;
        }
        if (zone.Find("Forest") != null)
        {
            Debug.Log("[MapGreyboxBuilder] Forest already exists, skipped.");
            return;
        }

        Undo.SetCurrentGroupName("Build North Forest");

        // Old placeholder tree cubes are replaced.
        for (int i = zone.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(zone.GetChild(i).gameObject);
        }

        Transform forest = CreateGroup("Forest", zone);
        Transform trails = CreateGroup("Trails", forest);
        Transform trees = CreateGroup("Trees", forest);
        Transform camp = CreateGroup("LumberCamp", forest);
        Transform cabin = CreateGroup("Cabin", forest);
        Transform rocks = CreateGroup("Rocks", forest);

        // Trails branch from the road terminus to the camp and the cabin.
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
        Box("Sawmill", camp, villageMat, 80f, 160f, 12f, 8f, 5f);
        Box("LogPile_0", camp, propMat, 60f, 142f, 8f, 1.5f, 1.2f);
        Box("LogPile_1", camp, propMat, 60f, 146f, 8f, 1.5f, 1.2f);
        Box("LogPile_2", camp, propMat, 72f, 138f, 8f, 1.5f, 1.2f, 20f);
        Box("Cabin_House", cabin, villageMat, Cabin.x, Cabin.z + 3f, 8f, 10f, 4f);

        // Boulders at clearing edges for cover/landmarks.
        (float x, float z, float size, float yaw)[] boulders =
        {
            (12f, 166f, 3f, 20f), (-14f, 150f, 2.5f, 45f), (88f, 140f, 3.5f, 10f),
            (52f, 162f, 2.5f, 60f), (-92f, 168f, 3f, 30f), (-66f, 188f, 2.5f, 75f),
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
        for (int attempt = 0; attempt < 8000; attempt++)
        {
            var p = new Vector3(
                Mathf.Lerp(ForestMinX, ForestMaxX, (float)rng.NextDouble()), 0f,
                Mathf.Lerp(ForestMinZ, ForestMaxZ, (float)rng.NextDouble()));
            if (IsForestBlocked(p, keepClear) || placed.Exists(q => (q - p).sqrMagnitude < TreeSpacing * TreeSpacing))
            {
                continue;
            }

            placed.Add(p);
            float diameter = Mathf.Lerp(2f, 3.5f, (float)rng.NextDouble());
            float height = Mathf.Lerp(5f, 8f, (float)rng.NextDouble());
            Cylinder($"Tree_{placed.Count - 1}", trees, forestMat, p.x, p.z, diameter, height);
        }

        Debug.Log($"[MapGreyboxBuilder] North forest built: {placed.Count} trees. Wood nodes/river/waterfall are later steps.");
    }

    // ---------------------------------------------------------------- Step 5: River, waterfall, bridges, cliff rim

    // River is impassable (invisible blockers); only the two bridges cross it. Terrain stays flat for now.
    // Route (world x, z): NW waterfall pool -> west edge of forest -> between meadow and forest (z≈85)
    // -> between base and factory (x≈75) -> SE lake. The south village is not touched.
    private const float RiverWidth = 12f;
    private const float WaterY = 0.04f;
    private const float BlockerHeight = 3f;
    private const float BlockerChunk = 4f;      // blockers are laid in chunks so bridges can leave a gap.
    private const float BridgeGapHalf = 5.5f;   // chunks within this distance of a bridge centre are skipped.
    private const float BridgeLength = 18f;     // river width + 3m of bank each side.
    private const float BridgeWidth = 8f;       // road is 7m.
    private const int CliffSeed = 4321;

    private static readonly Vector2[] RiverPath =
    {
        new Vector2(-170f, 214f), new Vector2(-150f, 180f), new Vector2(-128f, 140f), new Vector2(-100f, 100f),
        new Vector2(-60f, 86f), new Vector2(0f, 85f), new Vector2(50f, 85f), new Vector2(72f, 68f),
        new Vector2(75f, 0f), new Vector2(75f, -50f), new Vector2(105f, -95f), new Vector2(145f, -135f),
    };
    private static readonly Vector3 WaterfallPool = new Vector3(-170f, 0f, 214f);
    private const float PoolDiameter = 16f;
    private static readonly Vector3 Lake = new Vector3(145f, 0f, -135f);
    private const float LakeDiameter = 52f;

    // Where Road_To_North (x=-2) and Road_To_East (z=28) cross the river; forward = road direction.
    private static readonly (Vector3 center, Vector3 forward)[] Bridges =
    {
        (new Vector3(-2f, 0f, 85f), Vector3.forward),
        (new Vector3(73.8f, 0f, 28f), Vector3.right),
    };

    [MenuItem("Tools/Map/5. Build River + Waterfall + Cliffs")]
    private static void BuildRiverAndCliffs()
    {
        Transform blockout = GameObject.Find("Map_Blockout")?.transform;
        Transform boundary = blockout != null ? blockout.Find("Boundary") : null;
        Material roadMat = LoadMat("Mat_Graybox_Road");
        Material propMat = LoadMat("Mat_Graybox_Boundary");
        Material villageMat = LoadMat("Mat_Graybox_Village");
        if (boundary == null || roadMat == null || propMat == null || villageMat == null)
        {
            Debug.LogError("[MapGreyboxBuilder] Missing Map_Blockout/Boundary or graybox materials. Nothing built.");
            return;
        }
        if (blockout.Find("Terrain_Features") != null)
        {
            Debug.Log("[MapGreyboxBuilder] Terrain_Features already exists, skipped.");
            return;
        }

        Material waterMat = GetOrCreateWaterMaterial(roadMat);
        Undo.SetCurrentGroupName("Build River + Waterfall + Cliffs");

        Transform features = CreateGroup("Terrain_Features", blockout);
        Transform river = CreateGroup("River", features);
        Transform water = CreateGroup("Water", river);
        Transform blockers = CreateGroup("Blockers", river);
        Transform bridges = CreateGroup("Bridges", features);
        Transform waterfall = CreateGroup("Waterfall", features);
        Transform cliffs = CreateGroup("Cliffs", features);

        RemoveTreesInRiver(blockout);

        // River surface: strips per segment + discs at bends so corners have no gaps.
        for (int i = 0; i < RiverPath.Length - 1; i++)
        {
            Vector3 a = ToWorld(RiverPath[i]);
            Vector3 b = ToWorld(RiverPath[i + 1]);
            Strip($"River_{i}", water, waterMat, a, b, RiverWidth, WaterY);
            Disc($"Bend_{i}", water, waterMat, a, RiverWidth, WaterY);
            LayBlockers(blockers, i, a, b);
        }
        Disc("Lake", water, waterMat, Lake, LakeDiameter, WaterY);
        Disc("WaterfallPool", water, waterMat, WaterfallPool, PoolDiameter, WaterY);
        RoundBlocker("Lake_Blocker", blockers, Lake, LakeDiameter);
        RoundBlocker("WaterfallPool_Blocker", blockers, WaterfallPool, PoolDiameter);

        // Bridges: walkable deck above the water + railings (the gap in the blockers is what lets you cross).
        for (int i = 0; i < Bridges.Length; i++)
        {
            (Vector3 center, Vector3 forward) = Bridges[i];
            Transform bridge = CreateGroup($"Bridge_{i}", bridges);
            Vector3 half = forward * (BridgeLength * 0.5f);
            Strip("Deck", bridge, villageMat, center - half, center + half, BridgeWidth, 0.06f);
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (BridgeWidth * 0.5f);
            float yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            foreach (int sign in new[] { -1, 1 })
            {
                Vector3 p = center + side * sign;
                Box(sign < 0 ? "Railing_L" : "Railing_R", bridge, propMat, p.x, p.z, 0.3f, BridgeLength, 1f, yaw);
            }
        }

        // Waterfall: cliff block in the NW corner, a vertical water face, pool below (already added above).
        Box("WaterfallCliff", waterfall, propMat, -172f, 227f, 26f, 6f, 14f);
        GameObject fall = CreatePrimitive(PrimitiveType.Cube, "WaterfallFace", waterfall, waterMat, keepCollider: false);
        fall.transform.position = new Vector3(-170f, 7f, 223.8f);
        fall.transform.localScale = new Vector3(8f, 14f, 0.3f);

        BuildCliffRim(cliffs, boundary, propMat);

        Debug.Log("[MapGreyboxBuilder] River, waterfall, 2 bridges and cliff rim built. Height sculpting/water shader are polish steps.");
    }

    private static Vector3 ToWorld(Vector2 p) => new Vector3(p.x, 0f, p.y);

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

    private static void RemoveTreesInRiver(Transform blockout)
    {
        Transform trees = blockout.Find("Zone_North_Forest/Forest/Trees");
        if (trees == null)
        {
            return;
        }

        float riverClear = RiverWidth * 0.5f + 2.5f;
        int removed = 0;
        for (int i = trees.childCount - 1; i >= 0; i--)
        {
            Transform tree = trees.GetChild(i);
            Vector3 p = tree.position;
            p.y = 0f;
            bool inRiver = (p - WaterfallPool).magnitude < PoolDiameter * 0.5f + 2.5f;
            for (int s = 0; s < RiverPath.Length - 1 && !inRiver; s++)
            {
                inRiver = DistanceToSegment(p, ToWorld(RiverPath[s]), ToWorld(RiverPath[s + 1])) < riverClear;
            }
            if (inRiver)
            {
                Undo.DestroyObjectImmediate(tree.gameObject);
                removed++;
            }
        }
        Debug.Log($"[MapGreyboxBuilder] Removed {removed} forest trees inside the river corridor.");
    }

    // Invisible walls along one river segment, skipping chunks at bridges.
    private static void LayBlockers(Transform parent, int segment, Vector3 a, Vector3 b)
    {
        Vector3 dir = b - a;
        int chunks = Mathf.Max(1, Mathf.CeilToInt(dir.magnitude / BlockerChunk));
        float chunkLength = dir.magnitude / chunks;
        Quaternion rot = Quaternion.LookRotation(dir);
        for (int c = 0; c < chunks; c++)
        {
            Vector3 mid = a + dir * ((c + 0.5f) / chunks);
            bool atBridge = System.Array.Exists(Bridges, br => (br.center - mid).magnitude < BridgeGapHalf);
            if (atBridge)
            {
                continue;
            }

            GameObject go = CreatePrimitive(PrimitiveType.Cube, $"Blocker_{segment}_{c}", parent, null, keepCollider: true);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            go.transform.SetPositionAndRotation(mid + Vector3.up * (BlockerHeight * 0.5f), rot);
            go.transform.localScale = new Vector3(RiverWidth, BlockerHeight, chunkLength + 0.5f); // slight overlap between chunks.
        }
    }

    private static void RoundBlocker(string name, Transform parent, Vector3 center, float diameter)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Cylinder, name, parent, null, keepCollider: false);
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        go.AddComponent<MeshCollider>().convex = true; // capsule collider would turn a flat wide cylinder into a sphere.
        go.transform.position = center + Vector3.up * (BlockerHeight * 0.5f);
        go.transform.localScale = new Vector3(diameter, BlockerHeight * 0.5f, diameter);
    }

    // Flat walkable disc (no collider).
    private static void Disc(string name, Transform parent, Material mat, Vector3 center, float diameter, float y)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Cylinder, name, parent, mat, keepCollider: false);
        go.transform.position = new Vector3(center.x, y, center.z);
        go.transform.localScale = new Vector3(diameter, 0.005f, diameter);
    }

    // Rock blocks just inside the boundary walls. South edge stays low so it never hides the player from the camera.
    // A rock is skipped if it would overlap anything already built (mall warehouse, factory tanks, waterfall, lake...).
    private static void BuildCliffRim(Transform parent, Transform boundary, Material mat)
    {
        Physics.SyncTransforms();
        var rng = new System.Random(CliffSeed);
        float Rand(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

        const float minX = -202f, maxX = 198f, minZ = -172f, maxZ = 229.6f, inset = 5f, step = 9f;
        var edges = new List<(Vector3 from, Vector3 to, float minH, float maxH)>
        {
            (new Vector3(minX + inset, 0f, minZ), new Vector3(minX + inset, 0f, maxZ), 6f, 12f),   // west
            (new Vector3(maxX - inset, 0f, minZ), new Vector3(maxX - inset, 0f, maxZ), 6f, 12f),   // east
            (new Vector3(minX, 0f, maxZ - inset), new Vector3(maxX, 0f, maxZ - inset), 8f, 14f),   // north
            (new Vector3(minX, 0f, minZ + inset), new Vector3(maxX, 0f, minZ + inset), 2f, 3.5f),  // south (low)
        };

        int placed = 0, skipped = 0;
        foreach ((Vector3 from, Vector3 to, float minH, float maxH) in edges)
        {
            int count = Mathf.CeilToInt((to - from).magnitude / step);
            for (int i = 0; i <= count; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)count);
                float size = Rand(7f, 12f);
                float height = Rand(minH, maxH);
                float yaw = Rand(0f, 90f);

                // Lifted a little off y=0 so the flat terrain collider doesn't count as an overlap.
                Vector3 checkCenter = new Vector3(p.x, height * 0.5f + 0.2f, p.z);
                Vector3 checkHalf = new Vector3(size * 0.5f, height * 0.5f - 0.2f, size * 0.5f);
                bool blocked = false;
                foreach (Collider hit in Physics.OverlapBox(checkCenter, checkHalf, Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(boundary) && hit.transform.parent?.name != "Trees")
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                {
                    skipped++;
                    continue;
                }

                Box($"Cliff_{placed}", parent, mat, p.x, p.z, size, size, height, yaw);
                placed++;
            }
        }
        Debug.Log($"[MapGreyboxBuilder] Cliff rim: {placed} rocks, {skipped} skipped to avoid overlapping existing structures.");
    }

    private static float DistanceToSegment(Vector3 p, Vector3 from, Vector3 to)
    {
        Vector3 seg = to - from;
        float t = Mathf.Clamp01(Vector3.Dot(p - from, seg) / seg.sqrMagnitude);
        return (p - (from + seg * t)).magnitude;
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
