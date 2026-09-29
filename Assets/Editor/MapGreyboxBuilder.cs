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

            GameObject road = CreatePrimitive(PrimitiveType.Plane, roadName, roads, roadMat, keepCollider: false);
            road.transform.SetPositionAndRotation(start + dir * (length * 0.5f) + Vector3.up * RoadY, Quaternion.LookRotation(dir));
            road.transform.localScale = new Vector3(RoadWidth / 10f, 1f, length / 10f); // Unity plane is 10x10.
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
