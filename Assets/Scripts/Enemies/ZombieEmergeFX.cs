using UnityEngine;

/// <summary>Ground-burst burst for emerging zombies: two shared world-space ParticleSystems (dirt chunks + dust)
/// that only Emit at a position, so no per-zombie objects are created.</summary>
public class ZombieEmergeFX : MonoBehaviour
{
    [SerializeField] private ParticleSystem debris;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private Vector2Int debrisCount = new Vector2Int(8, 12);
    [SerializeField] private Vector2Int dustCount = new Vector2Int(6, 10);

    public void Play(Vector3 position, float scale)
    {
        Emit(debris, position, scale, debrisCount);
        Emit(dust, position, scale, dustCount);
    }

    private static void Emit(ParticleSystem system, Vector3 position, float scale, Vector2Int count)
    {
        if (system == null)
            return;

        var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
        system.Emit(p, Mathf.RoundToInt(Random.Range(count.x, count.y + 1) * Mathf.Max(0.1f, scale)));
    }
}
