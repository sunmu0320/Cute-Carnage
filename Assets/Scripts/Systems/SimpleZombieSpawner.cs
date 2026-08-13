using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleZombieSpawner : MonoBehaviour
{
    [Serializable]
    private class WaveDefinition
    {
        [Min(1)] public int zombieCount = 10;
        [Min(0.01f)] public float spawnInterval = 1f;
    }

    private enum SpawnMode
    {
        FullCircle,
        DirectionalArc
    }

    private enum WaveState
    {
        InitialDelay,
        Spawning,
        WaitingForClear,
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
    [SerializeField] private WaveDefinition[] nightWaves;

    [Header("Spawn Direction")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.FullCircle;
    [SerializeField, Range(1f, 360f)] private float arcAngle = 45f;

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

    private void Update()
    {
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
            case WaveState.InterWaveDelay:
                UpdateDelayAndStartWave();
                break;
        }
    }

    /// <summary>Re-arms the spawner for a new night. Without this, the second night never starts:
    /// hasTriggeredDayTransition stays true and waveState stays Complete/Stopped from the previous night.</summary>
    public void ResetForNewNight()
    {
        hasTriggeredDayTransition = false;
        currentWaveIndex = 0;
        totalZombiesSpawned = 0;
        spawnedCount = 0;

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

    private void StartCurrentWave()
    {
        spawnedCount = 0;
        timer = 0f;
        waveState = WaveState.Spawning;

        WaveDefinition wave = nightWaves[currentWaveIndex];
        Debug.Log(
            $"[SimpleZombieSpawner] Wave {currentWaveIndex + 1}/{nightWaves.Length} started (zombies={wave.zombieCount}).",
            this);
    }

    private void UpdateSpawning()
    {
        WaveDefinition wave = nightWaves[currentWaveIndex];
        timer += Time.deltaTime;
        if (timer < wave.spawnInterval)
        {
            return;
        }

        if (zombiePrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("[SimpleZombieSpawner] zombiePrefab is not assigned.", this);
                warnedMissingPrefab = true;
            }

            timer = 0f;
            return;
        }

        Transform resolvedBaseCore = ResolveBaseCoreTransform();
        if (resolvedBaseCore == null)
        {
            if (!warnedMissingBaseCore)
            {
                Debug.LogWarning("[SimpleZombieSpawner] baseCoreTransform is not assigned and no BaseCore/HomeBase object was found.", this);
                warnedMissingBaseCore = true;
            }

            timer = 0f;
            return;
        }

        Vector3 spawnPosition = GetRandomSpawnPositionAroundBase(resolvedBaseCore.position);
        Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        warnedMissingBaseCore = false;
        spawnedCount++;
        totalZombiesSpawned++;
        if (spawnedCount >= wave.zombieCount)
        {
            waveState = WaveState.WaitingForClear;
        }

        timer = 0f;
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
            "[SimpleZombieSpawner] nightWaves is null or empty. Night spawning has stopped; Day transition was not triggered.",
            this);
    }

    private void ClampSettings()
    {
        initialWaveStartDelay = Mathf.Max(0f, initialWaveStartDelay);
        interWaveDelay = Mathf.Max(0f, interWaveDelay);
        arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
        if (nightWaves != null)
        {
            for (int i = 0; i < nightWaves.Length; i++)
            {
                if (nightWaves[i] == null)
                {
                    nightWaves[i] = new WaveDefinition();
                }

                nightWaves[i].zombieCount = Mathf.Max(1, nightWaves[i].zombieCount);
                nightWaves[i].spawnInterval = Mathf.Max(0.01f, nightWaves[i].spawnInterval);
            }
        }

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

    private Vector3 GetRandomSpawnPositionAroundBase(Vector3 baseCorePosition)
    {
        float randomDistance = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 direction;
        if (spawnMode == SpawnMode.DirectionalArc)
        {
            direction = GetHorizontalForward();
            float angle = UnityEngine.Random.Range(-arcAngle * 0.5f, arcAngle * 0.5f);
            direction = Quaternion.AngleAxis(angle, Vector3.up) * direction;
        }
        else
        {
            float randomAngleRadians = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            direction = new Vector3(Mathf.Cos(randomAngleRadians), 0f, Mathf.Sin(randomAngleRadians));
        }

        Vector3 spawnPosition = baseCorePosition + direction * randomDistance;
        spawnPosition.y = baseCorePosition.y + spawnHeightOffset;
        return spawnPosition;
    }

    private Vector3 GetHorizontalForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
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
        if (spawnMode == SpawnMode.FullCircle)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, minSpawnRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, maxSpawnRadius);
            return;
        }

        Vector3 forward = GetHorizontalForward();
        Vector3 left = Quaternion.AngleAxis(-arcAngle * 0.5f, Vector3.up) * forward;
        Vector3 right = Quaternion.AngleAxis(arcAngle * 0.5f, Vector3.up) * forward;

        Gizmos.color = Color.yellow;
        DrawArcGizmo(center, forward, minSpawnRadius);
        Gizmos.color = Color.red;
        DrawArcGizmo(center, forward, maxSpawnRadius);
        Gizmos.DrawLine(center + left * minSpawnRadius, center + left * maxSpawnRadius);
        Gizmos.DrawLine(center + right * minSpawnRadius, center + right * maxSpawnRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center, center + forward * maxSpawnRadius);
    }

    private void DrawArcGizmo(Vector3 center, Vector3 centerDirection, float radius)
    {
        const int segments = 24;
        float startAngle = -arcAngle * 0.5f;
        Vector3 previous = center + Quaternion.AngleAxis(startAngle, Vector3.up) * centerDirection * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, arcAngle * 0.5f, i / (float)segments);
            Vector3 next = center + Quaternion.AngleAxis(angle, Vector3.up) * centerDirection * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
