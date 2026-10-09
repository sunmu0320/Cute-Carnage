using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleZombieSpawner : MonoBehaviour
{
    private const float GoldenAngleDegrees = 137.508f;

    private enum WaveState
    {
        InitialDelay,
        Spawning,
        WaitingForClear,
        PostClearDelay,
        InterWaveDelay,
        Complete,
        Stopped
    }

    [Header("Spawn Setup")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private Transform baseCoreTransform;
    [SerializeField] private float minSpawnRadius = 12f;
    [SerializeField] private float maxSpawnRadius = 18f;
    [SerializeField] private float spawnHeightOffset = 0f;
    [SerializeField] private int maxSpawnPositionAttempts = 4;

    [Header("Night Waves")]
    [SerializeField] private float initialWaveStartDelay = 3f;
    [SerializeField] private float interWaveDelay = 3f;
    [SerializeField] private float postClearDelay = 1.5f;
    [SerializeField, Tooltip("Per-night waves; the night is picked from GameManager.CurrentDay.")]
    private NightSchedule nightSchedule;
    [SerializeField, Range(0f, 45f), Tooltip("Random wobble added to the evenly spread main-body angles.")]
    private float mainBodyAngleJitter = 12f;

    public event Action OnNightWaveCompleted;

    private static readonly string[] BaseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };

    private float timer;
    private int spawnedCount;
    private int totalZombiesSpawned;
    private int currentWaveIndex;
    private WaveState waveState;
    private bool hasTriggeredDayTransition;
    private bool warnedMissingPrefab;
    private bool warnedMissingBaseCore;
    private bool warnedInvalidSpawnRadiusOrder;
    private bool warnedMissingWaves;
    private bool isPaused;

    private NightSchedule.Wave[] nightWaves;
    private float waveElapsed;
    private float mainBodyAngleOffset;
    private int[] hordeSpawned;
    private float[] hordeTimers;

    private void Awake()
    {
        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Start()
    {
        LoadTonightsWaves();
        if (nightWaves == null || nightWaves.Length == 0)
        {
            WarnMissingWavesAndStop();
            return;
        }

        waveState = WaveState.InitialDelay;
        timer = initialWaveStartDelay;
        Debug.Log(
            $"[SimpleZombieSpawner] Night wave countdown started ({initialWaveStartDelay:0.##}s).",
            this);
    }

    /// <summary>Freezes wave progression/spawning in place (e.g. GameOver) without resetting any state.</summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    private void Update()
    {
        if (isPaused)
        {
            return;
        }

        switch (waveState)
        {
            case WaveState.InitialDelay:
                UpdateDelayAndStartWave();
                break;
            case WaveState.Spawning:
                UpdateSpawning();
                break;
            case WaveState.WaitingForClear:
                UpdateWaitingForClear();
                break;
            case WaveState.PostClearDelay:
                UpdatePostClearDelay();
                break;
            case WaveState.InterWaveDelay:
                UpdateDelayAndStartWave();
                break;
        }
    }

    /// <summary>Re-arms the spawner for a new night. Without this, the second night never starts:
    /// hasTriggeredDayTransition stays true and waveState stays Complete/Stopped from the previous night.</summary>
    public void ResetForNewNight()
    {
        isPaused = false;
        hasTriggeredDayTransition = false;
        currentWaveIndex = 0;
        totalZombiesSpawned = 0;
        spawnedCount = 0;

        LoadTonightsWaves();
        if (nightWaves == null || nightWaves.Length == 0)
        {
            WarnMissingWavesAndStop();
            return;
        }

        waveState = WaveState.InitialDelay;
        timer = initialWaveStartDelay;
        Debug.Log(
            $"[SimpleZombieSpawner] ResetForNewNight: wave countdown restarted ({initialWaveStartDelay:0.##}s).",
            this);
    }

    private void UpdateDelayAndStartWave()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        StartCurrentWave();
    }

    private void LoadTonightsWaves()
    {
        int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        NightSchedule.Night night = nightSchedule != null ? nightSchedule.GetNight(day) : null;
        nightWaves = night != null ? night.waves : null;
    }

    private void StartCurrentWave()
    {
        spawnedCount = 0;
        timer = 0f;
        waveElapsed = 0f;
        mainBodyAngleOffset = UnityEngine.Random.Range(0f, 360f);
        waveState = WaveState.Spawning;

        NightSchedule.Wave wave = nightWaves[currentWaveIndex];
        hordeSpawned = new int[wave.hordes.Length];
        hordeTimers = new float[wave.hordes.Length];
        Debug.Log(
            $"[SimpleZombieSpawner] Wave {currentWaveIndex + 1}/{nightWaves.Length} started " +
            $"(main={wave.zombieCount}, hordes={wave.hordes.Length}, total={wave.TotalCount}).",
            this);
    }

    /// <summary>Main body: evenly spread all around (golden-angle steps + jitter) at its interval.
    /// Hordes: each starts after its startDelay and spawns inside its compass arc at its own interval.
    /// The wave moves to WaitingForClear only once everything has spawned.</summary>
    private void UpdateSpawning()
    {
        NightSchedule.Wave wave = nightWaves[currentWaveIndex];
        waveElapsed += Time.deltaTime;

        if (spawnedCount < wave.zombieCount)
        {
            timer += Time.deltaTime;
            if (timer >= wave.spawnInterval)
            {
                float angle = mainBodyAngleOffset + spawnedCount * GoldenAngleDegrees
                    + UnityEngine.Random.Range(-mainBodyAngleJitter, mainBodyAngleJitter);
                if (TrySpawnAt(angle))
                {
                    spawnedCount++;
                }

                timer = 0f;
            }
        }

        bool hordesDone = true;
        for (int i = 0; i < wave.hordes.Length; i++)
        {
            NightSchedule.Horde horde = wave.hordes[i];
            if (hordeSpawned[i] >= horde.count)
            {
                continue;
            }

            hordesDone = false;
            if (waveElapsed < horde.startDelay)
            {
                continue;
            }

            hordeTimers[i] += Time.deltaTime;
            if (hordeTimers[i] < horde.spawnInterval && hordeSpawned[i] > 0)
            {
                continue;
            }

            Vector3 dir = NightSchedule.CompassDirection(horde.from);
            float center = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (TrySpawnAt(center + UnityEngine.Random.Range(-horde.arcWidth * 0.5f, horde.arcWidth * 0.5f)))
            {
                if (hordeSpawned[i] == 0)
                {
                    Debug.Log($"[SimpleZombieSpawner] Horde of {horde.count} incoming from the {horde.from}.", this);
                }

                hordeSpawned[i]++;
            }

            hordeTimers[i] = 0f;
        }

        if (spawnedCount >= wave.zombieCount && hordesDone)
        {
            waveState = WaveState.WaitingForClear;
        }
    }

    /// <summary>Spawns one zombie on the spawn ring at the given compass angle (0 = north/+Z, 90 = east).</summary>
    private bool TrySpawnAt(float compassAngleDegrees)
    {
        if (zombiePrefab == null)
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

        Instantiate(zombiePrefab, GetSpawnPosition(resolvedBaseCore.position, compassAngleDegrees), Quaternion.identity);
        warnedMissingBaseCore = false;
        totalZombiesSpawned++;
        return true;
    }

    private void UpdateWaitingForClear()
    {
        if (Zombie.AliveZombieCount > 0)
        {
            return;
        }

        Debug.Log(
            $"[SimpleZombieSpawner] Wave {currentWaveIndex + 1}/{nightWaves.Length} cleared.",
            this);

        timer = postClearDelay;
        waveState = WaveState.PostClearDelay;
        Debug.Log(
            $"[SimpleZombieSpawner] Post-clear countdown started ({postClearDelay:0.##}s).",
            this);
    }

    private void UpdatePostClearDelay()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        if (currentWaveIndex < nightWaves.Length - 1)
        {
            currentWaveIndex++;
            timer = interWaveDelay;
            waveState = WaveState.InterWaveDelay;
            Debug.Log(
                $"[SimpleZombieSpawner] Inter-wave countdown started ({interWaveDelay:0.##}s); Wave {currentWaveIndex + 1} follows.",
                this);
            return;
        }

        CompleteNight();
    }

    private void CompleteNight()
    {
        if (hasTriggeredDayTransition)
        {
            return;
        }

        hasTriggeredDayTransition = true;
        waveState = WaveState.Complete;
        OnNightWaveCompleted?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TransitionToDay();
        }
        else
        {
            Debug.LogWarning("[SimpleZombieSpawner] Night cleared but GameManager.Instance is null.", this);
        }

        Debug.Log(
            $"[SimpleZombieSpawner] Final Night wave complete (totalSpawned={totalZombiesSpawned}). TransitionToDay invoked.",
            this);
    }

    private void WarnMissingWavesAndStop()
    {
        waveState = WaveState.Stopped;
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
        initialWaveStartDelay = Mathf.Max(0f, initialWaveStartDelay);
        interWaveDelay = Mathf.Max(0f, interWaveDelay);
        postClearDelay = Mathf.Max(0f, postClearDelay);
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

        maxSpawnPositionAttempts = Mathf.Max(1, maxSpawnPositionAttempts);
    }

    private Vector3 GetSpawnPosition(Vector3 baseCorePosition, float compassAngleDegrees)
    {
        float distance = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 direction = Quaternion.AngleAxis(compassAngleDegrees, Vector3.up) * Vector3.forward;
        Vector3 spawnPosition = baseCorePosition + direction * distance;
        spawnPosition.y = baseCorePosition.y + spawnHeightOffset;
        return spawnPosition;
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
        center.y += spawnHeightOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, minSpawnRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, maxSpawnRadius);
    }
}
