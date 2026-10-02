using UnityEngine;

// Settings + model slots for the south village fountain plaza. Build/Clear buttons live in FountainPlazaDecorEditor.
// Everything generated goes under the child "Generated" (rebuilt from scratch each time, so nothing duplicates).
// Swap in your own models by dropping prefabs into the slots and pressing Rebuild:
//   - pavingStones: any size — each is scaled to fit one cell (cellSize) by its XZ bounds, pivot at the top face center
//     is not required (the builder aligns the bounds top to the ground).
//   - bench: long axis = local X, seat front = local +Z (set benchYawOffset if your model differs).
// Decorations have no colliders (same as the other environment props) so they never block movement.
public class FountainPlazaDecor : MonoBehaviour
{
    [Header("Target")]
    public Transform fountain;
    public int seed = 7;

    [Header("Paving")]
    public GameObject[] pavingStones;
    [Tooltip("Paving starts just outside the fountain base (m from fountain center).")]
    public float innerRadius = 5.35f;
    [Tooltip("Average outer radius of the paving (base diameter 10.8m x ~1.8).")]
    public float outerRadius = 9.8f;
    [Tooltip("How far the broken outer edge wanders in and out (m).")]
    public float edgeWobble = 1.4f;
    public float cellSize = 1.1f;
    [Range(0f, 0.3f)] public float missingChance = 0.06f;
    [Range(0f, 0.3f)] public float crackedChance = 0.1f;
    [Tooltip("Height of a stone's top face above the terrain (m). Keep > 0.02 to avoid z-fighting.")]
    public float stoneTopHeight = 0.045f;

    [Header("Lanes (world XZ end points; the lane starts at the paving edge)")]
    public Vector2[] laneEnds =
    {
        new Vector2(-19.5f, -80.5f),   // NE: joins the upper road
        new Vector2(-44.0f, -82.0f),   // W: between houses #19 and #20 toward door_20
        new Vector2(-14.0f, -103.0f),  // SE: between houses #33 and #55
    };
    public float laneWidth = 2.6f;

    [Header("Benches")]
    public GameObject bench;
    public float benchScale = 1.3f;
    public float benchYawOffset = 0f;
    [Tooltip("Angles (deg, 0 = +X, 90 = +Z) around the fountain. Kept clear of lanes and door approaches.")]
    public float[] benchAngles = { -128f, 138f };

    [Header("Debris (one edge sector)")]
    public GameObject[] debris;
    public float debrisAngle = -80f;
    public int debrisCount = 7;

    [Header("Weeds (crack / base / edge clumps)")]
    public GameObject weed;
    public Vector2 weedScale = new Vector2(0.45f, 0.75f);
}
