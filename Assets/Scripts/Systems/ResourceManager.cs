using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceManager : MonoBehaviour
{
    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    /// <summary>
    /// Same as <see cref="ResolveForRunStateTransfer"/> (kept for call sites that predate the rename).
    /// </summary>
    public static ResourceManager FindInActiveLoadedScene()
    {
        return ResolveForRunStateTransfer();
    }

    /// <summary>
    /// Resolves the <see cref="ResourceManager"/> that should own run-state capture/apply for the current moment.
    /// Includes inactive objects, unions wired references from all <see cref="PlayerInteractor"/>s with scene-local
    /// instances, then picks the highest wood+scrap+food total (tie: interactor in active scene, else first).
    /// </summary>
    public static ResourceManager ResolveForRunStateTransfer()
    {
        Scene active = SceneManager.GetActiveScene();
        List<ResourceManager> candidates = new List<ResourceManager>();

        PlayerInteractor[] interactors = FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < interactors.Length; i++)
        {
            PlayerInteractor pi = interactors[i];
            if (pi == null)
            {
                continue;
            }

            AddUniqueCandidate(candidates, pi.ResourceManager);
        }

        ResourceManager[] managers = FindObjectsByType<ResourceManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            ResourceManager m = managers[i];
            if (m == null)
            {
                continue;
            }

            bool referencedByInteractor = false;
            for (int j = 0; j < interactors.Length; j++)
            {
                PlayerInteractor pi = interactors[j];
                if (pi != null && pi.ResourceManager == m)
                {
                    referencedByInteractor = true;
                    break;
                }
            }

            if (referencedByInteractor || (active.IsValid() && m.gameObject.scene == active))
            {
                AddUniqueCandidate(candidates, m);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int bestTotal = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            ResourceManager c = candidates[i];
            if (c == null)
            {
                continue;
            }

            int t = GetInventoryTotal(c);
            if (t > bestTotal)
            {
                bestTotal = t;
            }
        }

        List<ResourceManager> top = new List<ResourceManager>();
        for (int i = 0; i < candidates.Count; i++)
        {
            ResourceManager c = candidates[i];
            if (c == null)
            {
                continue;
            }

            if (GetInventoryTotal(c) == bestTotal)
            {
                top.Add(c);
            }
        }

        if (top.Count == 0)
        {
            return null;
        }

        if (top.Count == 1)
        {
            return top[0];
        }

        if (active.IsValid())
        {
            for (int j = 0; j < interactors.Length; j++)
            {
                PlayerInteractor pi = interactors[j];
                if (pi == null || pi.gameObject.scene != active)
                {
                    continue;
                }

                ResourceManager wired = pi.ResourceManager;
                if (wired == null)
                {
                    continue;
                }

                for (int k = 0; k < top.Count; k++)
                {
                    if (top[k] == wired)
                    {
                        return wired;
                    }
                }
            }
        }

        return top[0];
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void TryLogDuplicateResourceManagersInActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid())
        {
            return;
        }

        ResourceManager[] managers = FindObjectsByType<ResourceManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<ResourceManager> inActive = new List<ResourceManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            ResourceManager m = managers[i];
            if (m != null && m.gameObject.scene == active)
            {
                inActive.Add(m);
            }
        }

        if (inActive.Count <= 1)
        {
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[ResourceManager] Multiple ResourceManagers in active scene '{active.name}' ({inActive.Count}): ");
        for (int i = 0; i < inActive.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(" | ");
            }

            sb.Append(inActive[i].GetDebugSummary());
        }

        Debug.LogWarning(sb.ToString());
    }
#endif

    private static void AddUniqueCandidate(List<ResourceManager> list, ResourceManager m)
    {
        if (m == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == m)
            {
                return;
            }
        }

        list.Add(m);
    }

    private static int GetInventoryTotal(ResourceManager rm)
    {
        if (rm == null)
        {
            return -1;
        }

        return rm.GetAmount(ResourceType.Wood) + rm.GetAmount(ResourceType.Scrap) + rm.GetAmount(ResourceType.Food);
    }

    public string GetDebugSummary()
    {
        InitializeIfNeeded();
        return $"instanceId={GetInstanceID()} wood={GetAmount(ResourceType.Wood)} scrap={GetAmount(ResourceType.Scrap)} food={GetAmount(ResourceType.Food)}";
    }

    private void Awake()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentRunState != null)
        {
            return;
        }

        InitializeIfNeeded();
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        InitializeIfNeeded();

        resources[type] += amount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[ResourceManager] AddResource {type} +{amount} on instance={GetInstanceID()} values: wood={GetAmount(ResourceType.Wood)} scrap={GetAmount(ResourceType.Scrap)} food={GetAmount(ResourceType.Food)}",
            this);
#else
        Debug.Log($"Added {amount} {type}. New total: {resources[type]}");
#endif
    }

    public bool HasResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        InitializeIfNeeded();
        return resources[type] >= amount;
    }

    public bool TrySpendResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();

        if (!HasResource(type, amount))
        {
            return false;
        }

        resources[type] -= amount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[ResourceManager] TrySpendResource {type} -{amount} on instance={GetInstanceID()} values: wood={GetAmount(ResourceType.Wood)} scrap={GetAmount(ResourceType.Scrap)} food={GetAmount(ResourceType.Food)}",
            this);
#else
        Debug.Log($"Spent {amount} {type}. New total: {resources[type]}");
#endif
        return true;
    }

    public void RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (TrySpendResource(type, amount))
        {
            return;
        }

        int available = GetAmount(type);
        if (available <= 0)
        {
            return;
        }

        TrySpendResource(type, available);
    }

    public bool HasEnough(ResourceType type, int amount)
    {
        return HasResource(type, amount);
    }

    public int GetAmount(ResourceType type)
    {
        InitializeIfNeeded();
        return resources[type];
    }

    private void InitializeIfNeeded()
    {
        if (resources.Count > 0)
        {
            return;
        }

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
    }

    public ResourceRuntimeState CaptureRuntimeState()
    {
        InitializeIfNeeded();
        return new ResourceRuntimeState
        {
            wood = GetAmount(ResourceType.Wood),
            scrap = GetAmount(ResourceType.Scrap),
            food = GetAmount(ResourceType.Food)
        };
    }

    public void ApplyRuntimeState(ResourceRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        InitializeIfNeeded();
        resources[ResourceType.Wood] = Mathf.Max(0, state.wood);
        resources[ResourceType.Scrap] = Mathf.Max(0, state.scrap);
        resources[ResourceType.Food] = Mathf.Max(0, state.food);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ResourceManager] After ApplyRuntimeState. {GetDebugSummary()}", this);
#endif
    }
}
