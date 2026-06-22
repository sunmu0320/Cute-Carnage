using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleZombieSpawner : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private Transform baseCoreTransform;
    [SerializeField] private int zombiesPerWave = 12;
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private float minSpawnRadius = 12f;
    [SerializeField] private float maxSpawnRadius = 18f;
    [SerializeField] private float spawnHeightOffset = 0f;
    [SerializeField] private int maxSpawnPositionAttempts = 4;

    public event Action OnNightWaveCompleted;

    private static readonly string[] BaseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };

    private float timer;
    private int spawnedCount;
    private int totalZombiesSpawned;
    private bool spawningFinished;
    private bool hasTriggeredDayTransition;
    private bool warnedMissingPrefab;
    private bool warnedMissingBaseCore;
    private bool warnedSpawnFailed;
    private bool warnedInvalidSpawnRadiusOrder;

    private void Awake()
    {
        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Update()
    {
        if (spawningFinished)
        {
            TryCompleteNightWhenCleared();
            return;
        }

        timer += Time.deltaTime;
        if (timer < spawnInterval)
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

        bool spawnedThisTick = false;
        for (int i = 0; i < maxSpawnPositionAttempts; i++)
        {
            Vector3 spawnPosition = GetRandomSpawnPositionAroundBase(resolvedBaseCore.position);
            Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
            spawnedThisTick = true;
            break;
        }

        if (!spawnedThisTick)
        {
            if (!warnedSpawnFailed)
            {
                Debug.LogWarning("[SimpleZombieSpawner] Failed to resolve a valid spawn position this tick.", this);
                warnedSpawnFailed = true;
            }

            timer = 0f;
            return;
        }

        warnedSpawnFailed = false;
        warnedMissingBaseCore = false;
        spawnedCount++;
        totalZombiesSpawned++;
        if (spawnedCount >= zombiesPerWave)
        {
            spawningFinished = true;
        }

        timer = 0f;
    }

    private void TryCompleteNightWhenCleared()
    {
        if (hasTriggeredDayTransition)
        {
            return;
        }

        if (Zombie.AliveZombieCount > 0)
        {
            return;
        }

        hasTriggeredDayTransition = true;
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
            $"[SimpleZombieSpawner] Night wave complete (spawned={spawnedCount}, totalSpawned={totalZombiesSpawned}). TransitionToDay invoked.",
            this);
    }

    private void ClampSettings()
    {
        zombiesPerWave = Mathf.Max(1, zombiesPerWave);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
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
        float randomAngleRadians = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float randomDistance = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 direction = new Vector3(Mathf.Cos(randomAngleRadians), 0f, Mathf.Sin(randomAngleRadians));
        Vector3 spawnPosition = baseCorePosition + direction * randomDistance;
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
