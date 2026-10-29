using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runs the night on one clock (seconds since nightfall). A continuous stream spawns all around on the
/// near ring for the whole night; each wave point starts at its scheduled time (never waiting for kills) and
/// spawns its groups on the far ring. Killing most of a wave gives a short breather (thinner stream). The night
/// ends only when every wave group has fully spawned and every zombie is dead — this spawner is the only thing
/// that asks GameManager for the Day transition.</summary>
public class SimpleZombieSpawner : MonoBehaviour
{
    // ponytail: per-frame spawn budget so a backlog (alive cap just freed) drains over a few frames, not one.
    private const int MaxSpawnsPerFrame = 8;

    [Header("Spawn Setup")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private Transform baseCoreTransform;
    [SerializeField, Tooltip("Stream ring (inner edge): just outside the fences.")] private float minSpawnRadius = 12f;
    [SerializeField, Tooltip("Stream ring (outer edge): off-screen.")] private float maxSpawnRadius = 18f;
    [SerializeField, Tooltip("Wave ring (inner edge): far, off-screen.")] private float waveMinSpawnRadius = 45f;
    [SerializeField, Tooltip("Wave ring (outer edge).")] private float waveMaxSpawnRadius = 60f;
    [SerializeField] private int maxSpawnPositionAttempts = 4;

    [Header("Night")]
    [SerializeField, Tooltip("Per-night stream and waves; the night is picked from GameManager.CurrentDay.")]
    private NightSchedule nightSchedule;
    [SerializeField, Min(1), Tooltip("Spawning pauses while this many zombies are alive; skipped spawns come later.")]
    private int maxAlive = 300;
    [SerializeField, Range(0f, 1f), Tooltip("Killing this fraction of a wave starts a breather.")]
    private float restKillFraction = 0.8f;
    [SerializeField, Min(0f)] private float restDuration = 10f;
    [SerializeField, Range(0.05f, 1f), Tooltip("Stream rate during a breather (0.3 = 30% of normal).")]
    private float restStreamFactor = 0.3f;

    /// <summary>Fires once per whole second of night time, on each wave start, and on kills after the last wave
    /// has spawned (remaining count).</summary>
    public event Action OnNightProgressChanged;

    public event Action<int> OnWaveStarted;

    public int WaveCount => waves != null ? waves.Length : 0;
    public int WavesStarted => wavesStarted;
    public float NightElapsed => elapsed;
    public float NightDuration => WaveCount > 0 ? WaveTimeAt(WaveCount - 1) : 0f;
    public float NightProgress => NightDuration > 0f ? Mathf.Clamp01(elapsed / NightDuration) : (AllSpawned ? 1f : 0f);
    public bool AllSpawned => running && wavesStarted == WaveCount && runs != null && AllRunsSpawned();
    public bool IsResting => elapsed < restUntil;

    /// <summary>Kept for the current HUD text until the dial switches to time-based display.</summary>
    public int CurrentWaveNumber => Mathf.Clamp(wavesStarted, 1, Mathf.Max(1, WaveCount));

    public float SecondsToNextWave => wavesStarted < WaveCount ? Mathf.Max(0f, WaveTimeAt(wavesStarted) - elapsed) : -1f;

    public float WaveTimeAt(int index) => NightSchedule.WaveTime(waves, index);

    private class WaveRun
    {
        public int[] spawned;
        public float[] timers;
        public int total;
        public int killed;
        public bool restTriggered;
    }

    private static readonly string[] BaseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };

    private NightSchedule.Stream stream;
    private NightSchedule.WavePoint[] waves;
    private WaveRun[] runs;
    private float elapsed;
    private int lastWholeSecond;
    private int wavesStarted;
    private float streamTimer;
    private float restUntil;
    private float hpMultiplier = 1f;
    private int totalZombiesSpawned;
    private bool running;
    private bool hasTriggeredDayTransition;
    private bool isPaused;
    private bool warnedMissingPrefab;
    private bool warnedMissingBaseCore;
    private bool warnedInvalidSpawnRadiusOrder;
    private bool warnedMissingWaves;
    private bool warnedNoSpawnPoint;
    private int spawnBudget;
    private readonly Collider[] overlapBuffer = new Collider[16];

    private void Awake()
    {
        ClampSettings();
    }

    private void OnEnable()
    {
        Zombie.Died += HandleZombieDied;
    }

    private void OnDisable()
    {
        Zombie.Died -= HandleZombieDied;
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Start()
    {
        // GameManager calls ResetForNewNight right after activating nightRoot; only arm here if it didn't.
        if (!running && !hasTriggeredDayTransition)
        {
            ResetForNewNight();
        }
    }

    /// <summary>Freezes the night clock and spawning in place (e.g. GameOver) without resetting any state.</summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    /// <summary>Re-arms the spawner for a new night: clock, waves, kill counts, breather and backlog all reset.</summary>
    public void ResetForNewNight()
    {
        isPaused = false;
        hasTriggeredDayTransition = false;
        running = false;
        elapsed = 0f;
        lastWholeSecond = 0;
        wavesStarted = 0;
        restUntil = -1f;
        totalZombiesSpawned = 0;

        int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        NightSchedule.Night night = nightSchedule != null ? nightSchedule.GetNight(day) : null;
        stream = night != null ? night.stream : null;
        waves = night != null ? night.waves : null;
        if (waves == null || waves.Length == 0)
        {
            WarnMissingWavesAndStop();
            return;
        }

        hpMultiplier = NightSchedule.HpMultiplier(nightSchedule.HpGrowthPerDay, day);
        runs = new WaveRun[waves.Length];
        for (int i = 0; i < waves.Length; i++)
        {
            NightSchedule.Group[] groups = waves[i].groups ?? new NightSchedule.Group[0];
            runs[i] = new WaveRun
            {
                spawned = new int[groups.Length],
                // First spawn of each group happens as soon as its startDelay passes.
                timers = new float[groups.Length],
                total = waves[i].TotalCount,
            };
            for (int g = 0; g < groups.Length; g++)
            {
                runs[i].timers[g] = groups[g].spawnInterval;
            }
        }

        streamTimer = stream != null ? NightSchedule.StreamInterval(stream, 0f) : 0f;
        running = true;
        OnNightProgressChanged?.Invoke();
        Debug.Log(
            $"[SimpleZombieSpawner] Night {day} started: {waves.Length} waves, last at {NightDuration:0.#}s, HP x{hpMultiplier:0.##}.",
            this);
    }

    private void Update()
    {
        if (isPaused || !running || hasTriggeredDayTransition)
        {
            return;
        }

        elapsed += Time.deltaTime;
        spawnBudget = MaxSpawnsPerFrame;

        while (wavesStarted < waves.Length && elapsed >= WaveTimeAt(wavesStarted))
        {
            int started = wavesStarted++;
            Debug.Log($"[SimpleZombieSpawner] Wave {started + 1}/{waves.Length} started at {elapsed:0.0}s ({runs[started].total} zombies).", this);
            OnWaveStarted?.Invoke(started);
            OnNightProgressChanged?.Invoke();
        }

        for (int i = 0; i < wavesStarted; i++)
        {
            UpdateWaveGroups(i);
        }

        bool allSpawned = AllSpawned;
        if (!allSpawned)
        {
            UpdateStream();
        }

        int whole = (int)elapsed;
        if (whole != lastWholeSecond)
        {
            lastWholeSecond = whole;
            OnNightProgressChanged?.Invoke();
        }

        if (allSpawned && Zombie.AliveZombieCount == 0)
        {
            CompleteNight();
        }
    }

    private void UpdateWaveGroups(int waveIndex)
    {
        NightSchedule.Group[] groups = waves[waveIndex].groups;
        WaveRun run = runs[waveIndex];
        float waveElapsed = elapsed - WaveTimeAt(waveIndex);
        for (int g = 0; g < groups.Length; g++)
        {
            NightSchedule.Group group = groups[g];
            if (run.spawned[g] >= group.count || waveElapsed < group.startDelay)
            {
                continue;
            }

            run.timers[g] += Time.deltaTime;
            while (run.timers[g] >= group.spawnInterval && run.spawned[g] < group.count)
            {
                float angle;
                if (group.allAround)
                {
                    angle = UnityEngine.Random.Range(0f, 360f);
                }
                else
                {
                    Vector3 dir = NightSchedule.CompassDirection(group.from);
                    angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg
                        + UnityEngine.Random.Range(-group.arcWidth * 0.5f, group.arcWidth * 0.5f);
                }

                // Failed (alive cap, no free ground, budget): keep the timer so it catches up later.
                if (!TrySpawn(group.zombie, angle, waveMinSpawnRadius, waveMaxSpawnRadius, waveIndex))
                {
                    break;
                }

                run.spawned[g]++;
                run.timers[g] -= group.spawnInterval;
            }
        }
    }

    private void UpdateStream()
    {
        if (stream == null)
        {
            return;
        }

        float interval = NightSchedule.StreamInterval(stream, NightProgress);
        if (IsResting)
        {
            interval /= restStreamFactor;
        }

        streamTimer += Time.deltaTime;
        while (streamTimer >= interval)
        {
            ZombieData data = NightSchedule.PickStreamZombie(stream, NightProgress, UnityEngine.Random.value);
            if (!TrySpawn(data, UnityEngine.Random.Range(0f, 360f), minSpawnRadius, maxSpawnRadius, -1))
            {
                // Stream spawns are not owed: drop the backlog instead of bursting later.
                streamTimer = Mathf.Min(streamTimer, interval);
                break;
            }

            streamTimer -= interval;
        }
    }

    private bool AllRunsSpawned()
    {
        for (int i = 0; i < runs.Length; i++)
        {
            NightSchedule.Group[] groups = waves[i].groups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (runs[i].spawned[g] < groups[g].count)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void HandleZombieDied(Zombie zombie)
    {
        if (!running || hasTriggeredDayTransition || runs == null)
        {
            return;
        }

        int w = zombie.WaveIndex;
        if (w >= 0 && w < runs.Length)
        {
            WaveRun run = runs[w];
            run.killed++;
            if (!run.restTriggered && run.total > 0 && run.killed >= Mathf.CeilToInt(run.total * restKillFraction))
            {
                run.restTriggered = true;
                restUntil = elapsed + restDuration;
                Debug.Log($"[SimpleZombieSpawner] Wave {w + 1} {restKillFraction:P0} cleared: breather for {restDuration:0.#}s.", this);
            }
        }

        if (AllSpawned)
        {
            OnNightProgressChanged?.Invoke();
        }
    }

    /// <summary>Spawns one zombie on the given ring at a compass angle (0 = north/+Z, 90 = east).</summary>
    private bool TrySpawn(ZombieData data, float compassAngleDegrees, float minR, float maxR, int waveIndex)
    {
        if (spawnBudget <= 0 || Zombie.AliveZombieCount >= maxAlive)
        {
            return false;
        }

        GameObject prefab = data != null && data.prefab != null ? data.prefab : zombiePrefab;
        if (prefab == null)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("[SimpleZombieSpawner] zombiePrefab is not assigned.", this);
                warnedMissingPrefab = true;
            }

            return false;
        }

        Transform resolvedBaseCore = ResolveBaseCoreTransform();
        if (resolvedBaseCore == null)
        {
            if (!warnedMissingBaseCore)
            {
                Debug.LogWarning("[SimpleZombieSpawner] baseCoreTransform is not assigned and no BaseCore/HomeBase object was found.", this);
                warnedMissingBaseCore = true;
            }

            return false;
        }

        warnedMissingBaseCore = false;
        if (!TryFindSpawnPoint(resolvedBaseCore.position, compassAngleDegrees, minR, maxR, out Vector3 point))
        {
            if (!warnedNoSpawnPoint)
            {
                Debug.LogWarning($"[SimpleZombieSpawner] No free ground found on the {minR:0.#}-{maxR:0.#}m ring at {compassAngleDegrees:0}°; retrying next frame.", this);
                warnedNoSpawnPoint = true;
            }

            return false;
        }

        Vector3 toBase = resolvedBaseCore.position - point;
        toBase.y = 0f;
        Quaternion facing = toBase.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toBase) : Quaternion.identity;
        GameObject spawned = Instantiate(prefab, point, facing);
        Zombie zombie = spawned.GetComponent<Zombie>();
        if (zombie != null)
        {
            zombie.Initialize(data, hpMultiplier, waveIndex, false);
        }

        spawnBudget--;
        totalZombiesSpawned++;
        return true;
    }

    /// <summary>Random point on the ring that lies on terrain (not on a rock, building, fence or prop) and has
    /// no non-terrain collider where the zombie would stand.</summary>
    private bool TryFindSpawnPoint(Vector3 center, float compassAngleDegrees, float minR, float maxR, out Vector3 point)
    {
        int mask = ~LayerMask.GetMask("Zombie", "VFXVolume", "Ignore Raycast", "UI");
        Vector3 direction = Quaternion.AngleAxis(compassAngleDegrees, Vector3.up) * Vector3.forward;
        for (int attempt = 0; attempt < maxSpawnPositionAttempts; attempt++)
        {
            Vector3 probe = center + direction * UnityEngine.Random.Range(minR, maxR);
            probe.y = center.y + 50f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore)
                || !(hit.collider is TerrainCollider))
            {
                continue;
            }

            Vector3 ground = hit.point;
            int count = Physics.OverlapCapsuleNonAlloc(
                ground + Vector3.up * 0.5f, ground + Vector3.up * 1.8f, 0.45f,
                overlapBuffer, mask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                if (!(overlapBuffer[i] is TerrainCollider))
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
            {
                continue;
            }

            point = ground;
            return true;
        }

        point = default;
        return false;
    }

    private void CompleteNight()
    {
        if (hasTriggeredDayTransition)
        {
            return;
        }

        hasTriggeredDayTransition = true;
        running = false;
        OnNightProgressChanged?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TransitionToDay();
        }
        else
        {
            Debug.LogWarning("[SimpleZombieSpawner] Night cleared but GameManager.Instance is null.", this);
        }

        Debug.Log(
            $"[SimpleZombieSpawner] Night complete at {elapsed:0.0}s (totalSpawned={totalZombiesSpawned}). TransitionToDay invoked.",
            this);
    }

    private void WarnMissingWavesAndStop()
    {
        running = false;
        if (warnedMissingWaves)
        {
            return;
        }

        warnedMissingWaves = true;
        Debug.LogWarning(
            "[SimpleZombieSpawner] nightSchedule is missing or tonight has no waves. Night spawning has stopped; Day transition was not triggered.",
            this);
    }

    private void ClampSettings()
    {
        minSpawnRadius = Mathf.Max(0f, minSpawnRadius);
        maxSpawnRadius = Mathf.Max(0f, maxSpawnRadius);
        if (maxSpawnRadius < minSpawnRadius)
        {
            maxSpawnRadius = minSpawnRadius;
            if (!warnedInvalidSpawnRadiusOrder)
            {
                Debug.LogWarning("[SimpleZombieSpawner] maxSpawnRadius was below minSpawnRadius, clamped to minSpawnRadius.", this);
                warnedInvalidSpawnRadiusOrder = true;
            }
        }
        else
        {
            warnedInvalidSpawnRadiusOrder = false;
        }

        waveMinSpawnRadius = Mathf.Max(0f, waveMinSpawnRadius);
        waveMaxSpawnRadius = Mathf.Max(waveMinSpawnRadius, waveMaxSpawnRadius);
        maxSpawnPositionAttempts = Mathf.Max(1, maxSpawnPositionAttempts);
    }

    private Transform ResolveBaseCoreTransform()
    {
        if (baseCoreTransform != null)
        {
            return baseCoreTransform;
        }

        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "HomeBase")
            {
                continue;
            }

            PersistentId rootPersistentId = root.GetComponent<PersistentId>();
            if (rootPersistentId != null)
            {
                baseCoreTransform = root.transform;
                return baseCoreTransform;
            }

            PersistentId[] childPersistentIds = root.GetComponentsInChildren<PersistentId>(true);
            if (childPersistentIds.Length > 0 && childPersistentIds[0] != null)
            {
                baseCoreTransform = childPersistentIds[0].transform;
                return baseCoreTransform;
            }
        }

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindBaseObjectByNameRecursive(roots[i] != null ? roots[i].transform : null);
            if (found != null)
            {
                baseCoreTransform = found.transform;
                return baseCoreTransform;
            }
        }

        return null;
    }

    private static GameObject FindBaseObjectByNameRecursive(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < BaseCoreNameCandidates.Length; i++)
        {
            if (root.name == BaseCoreNameCandidates[i])
            {
                return root.gameObject;
            }
        }

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindBaseObjectByNameRecursive(root.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Transform gizmoBase = baseCoreTransform != null ? baseCoreTransform : ResolveBaseCoreTransform();
        if (gizmoBase == null)
        {
            return;
        }

        Vector3 center = gizmoBase.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, minSpawnRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, maxSpawnRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, waveMinSpawnRadius);
        Gizmos.DrawWireSphere(center, waveMaxSpawnRadius);
    }
}
