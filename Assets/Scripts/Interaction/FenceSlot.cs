using UnityEngine;

[DisallowMultipleComponent]
public class FenceSlot : MonoBehaviour, IInteractable
{
    [Header("Slot State")]
    [SerializeField] private bool isUnlocked = true;
    [SerializeField] private GameObject installedFence;

    [Header("Fence Data")]
    [SerializeField, Tooltip("Fence type this slot builds. Supplies the prefab and all install/repair costs.")]
    private FenceData startingFenceData;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField, Tooltip("Minimum seconds between resource cost debug logs (prompt refresh can be every frame).")]
    private float resourceCostDebugLogCooldownSeconds = 0.6f;

    private float lastResourceCostDebugLogUnscaledTime = float.NegativeInfinity;
#endif

    [Header("Anchors")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private Transform slotMarker;

    private PersistentId persistentId;
    private FenceSegment installedFenceSegment;
    private bool hasWarnedFallbackSpawnPoint;

    public bool IsUnlocked => isUnlocked;
    public bool HasFence => installedFence != null;
    public GameObject InstalledFence => installedFence;
    public FenceSegment CurrentFence => GetFenceSegmentOnInstalledFence();
    public int WoodBuildCost => startingFenceData != null ? Mathf.Max(0, startingFenceData.InstallWoodCost) : 0;
    public int ScrapBuildCost => startingFenceData != null ? Mathf.Max(0, startingFenceData.InstallScrapCost) : 0;
    public int WoodRepairCost => CurrentFence != null ? CurrentFence.GetRequiredWood() : 0;
    public int ScrapRepairCost => CurrentFence != null ? CurrentFence.GetRequiredScrap() : 0;
    public Transform SlotMarker => slotMarker;
    public PersistentId PersistentIdComponent => persistentId;
    public string PersistentSlotId => persistentId != null ? persistentId.Id : string.Empty;

    /// <summary>Next-tier fence data for the installed fence, if any. Null until Phase 2 wires an upgrade action to it.</summary>
    public FenceData NextTierData
    {
        get
        {
            FenceSegment fence = CurrentFence;
            return fence != null ? fence.NextTierData : null;
        }
    }

    public Transform SpawnPoint
    {
        get
        {
            if (spawnPoint != null)
            {
                return spawnPoint;
            }

            if (!hasWarnedFallbackSpawnPoint)
            {
                hasWarnedFallbackSpawnPoint = true;
                Debug.LogWarning($"[FenceSlot] '{name}' has no explicit spawnPoint. Falling back to slot transform.", this);
            }

            return transform;
        }
    }

    private void Awake()
    {
        CachePersistentId();
        ValidatePersistentId();
        RefreshSlotVisualState();
    }

    private void OnValidate()
    {
        CachePersistentId();
        ValidatePersistentId();
        RefreshSlotVisualState();
    }

    public bool CanInstallFence()
    {
        return isUnlocked && !HasFence && startingFenceData != null && startingFenceData.FencePrefab != null;
    }

    public bool HasRequiredBuildResources(ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return false;
        }

        int requiredWood = WoodBuildCost;
        int requiredScrap = ScrapBuildCost;
        bool canAfford = resourceManager.HasResource(ResourceType.Wood, requiredWood) &&
                         resourceManager.HasResource(ResourceType.Scrap, requiredScrap);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float t = Time.unscaledTime;
        if (t - lastResourceCostDebugLogUnscaledTime >= resourceCostDebugLogCooldownSeconds)
        {
            lastResourceCostDebugLogUnscaledTime = t;
            Debug.Log(
                $"[FenceSlot] '{name}' cost check. ResourceManager instanceId={resourceManager.GetInstanceID()} {resourceManager.GetDebugSummary()} " +
                $"requiredWood={requiredWood} requiredScrap={requiredScrap} canAfford={canAfford}",
                this);
        }
#endif

        return canAfford;
    }

    public void SetInstalledFence(GameObject fenceObject)
    {
        installedFence = fenceObject;
        installedFenceSegment = null;
        RefreshSlotVisualState();
    }

    public void ClearInstalledFence()
    {
        installedFence = null;
        installedFenceSegment = null;
        RefreshSlotVisualState();
    }

    private void CachePersistentId()
    {
        if (persistentId != null)
        {
            return;
        }

        persistentId = GetComponent<PersistentId>();
    }

    private void ValidatePersistentId()
    {
        if (persistentId == null)
        {
            Debug.LogWarning($"[FenceSlot] '{name}' is missing PersistentId. Add one for runtime-state mapping.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(persistentId.Id))
        {
            Debug.LogWarning($"[FenceSlot] '{name}' has an empty PersistentId value.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform anchor = spawnPoint != null ? spawnPoint : transform;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(anchor.position, 0.18f);
        Gizmos.DrawLine(transform.position, anchor.position);

        if (slotMarker != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(slotMarker.position, Vector3.one * 0.15f);
        }
    }

    public Transform GetUIAnchor()
    {
        return interactionAnchor != null ? interactionAnchor : SpawnPoint;
    }

    public Vector3 GetInteractPosition()
    {
        return SpawnPoint.position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!isUnlocked || GameManager.Instance == null)
        {
            return false;
        }

        if (GameManager.Instance.IsDay)
        {
            return true;
        }

        FenceSegment segment = CurrentFence;
        return HasFence && segment != null && segment.CanRepair();
    }

    public InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        if (!isUnlocked)
        {
            return InteractablePromptData.CreateSimple("Fence slot locked");
        }

        if (GameManager.Instance != null && !GameManager.Instance.IsDay)
        {
            FenceSegment nightFence = CurrentFence;
            if (nightFence == null || !nightFence.CanRepair())
            {
                return default;
            }

            ResourceManager nightManager = interactor != null ? interactor.ResourceManager : null;
            return new InteractablePromptData
            {
                actionText = "Press E to repair Fence",
                woodCost = nightFence.GetRequiredWood(),
                scrapCost = nightFence.GetRequiredScrap(),
                canAfford = nightFence.HasEnoughResources(nightManager)
            };
        }

        if (HasFence)
        {
            return InteractablePromptData.CreateSimple("Fence Installed");
        }

        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        bool canAfford = HasRequiredBuildResources(resourceManager);
        return new InteractablePromptData
        {
            actionText = "Press E to place fence",
            woodCost = WoodBuildCost,
            scrapCost = ScrapBuildCost,
            canAfford = canAfford
        };
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null || GameManager.Instance == null || !CanInteract(interactor))
        {
            return;
        }

        if (GameManager.Instance.IsDay)
        {
            interactor.OpenStructureActionPanel(this);
        }
        else
        {
            interactor.OpenNightRepairPanel(this);
        }
    }

    public bool TryInstallFence(PlayerInteractor interactor)
    {
        if (!CanInstallFence())
        {
            return false;
        }

        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        if (resourceManager == null)
        {
            Debug.LogWarning($"[FenceSlot] '{name}' cannot place fence: missing ResourceManager.", this);
            return false;
        }

        int requiredWood = WoodBuildCost;
        int requiredScrap = ScrapBuildCost;

        if (!HasRequiredBuildResources(resourceManager))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FenceSlot] '{name}' cannot place fence: not enough resources. ResourceManager instanceId={resourceManager.GetInstanceID()} " +
                $"{resourceManager.GetDebugSummary()} requiredWood={requiredWood} requiredScrap={requiredScrap}.",
                this);
#else
            Debug.Log($"[FenceSlot] '{name}' cannot place fence: not enough resources (Wood {requiredWood}, Scrap {requiredScrap}).", this);
#endif
            return false;
        }

        bool spentWood = resourceManager.TrySpendResource(ResourceType.Wood, requiredWood) || requiredWood == 0;
        bool spentScrap = resourceManager.TrySpendResource(ResourceType.Scrap, requiredScrap) || requiredScrap == 0;
        if (!spentWood || !spentScrap)
        {
            Debug.LogWarning($"[FenceSlot] '{name}' failed to spend build cost. Placement cancelled.", this);
            if (spentWood && requiredWood > 0)
            {
                resourceManager.AddResource(ResourceType.Wood, requiredWood);
            }
            if (spentScrap && requiredScrap > 0)
            {
                resourceManager.AddResource(ResourceType.Scrap, requiredScrap);
            }
            return false;
        }

        if (TryPlaceFence())
        {
            return true;
        }

        if (requiredWood > 0)
        {
            resourceManager.AddResource(ResourceType.Wood, requiredWood);
        }
        if (requiredScrap > 0)
        {
            resourceManager.AddResource(ResourceType.Scrap, requiredScrap);
        }
        return false;
    }

    public bool TryRepairFence(PlayerInteractor interactor)
    {
        FenceSegment segment = CurrentFence;
        return interactor != null
            && segment != null
            && segment.TryRepair(interactor.ResourceManager);
    }

    private FenceSegment GetFenceSegmentOnInstalledFence()
    {
        if (installedFence == null)
        {
            installedFenceSegment = null;
            return null;
        }

        if (installedFenceSegment == null)
        {
            installedFenceSegment = installedFence.GetComponent<FenceSegment>();
            if (installedFenceSegment == null)
            {
                installedFenceSegment = installedFence.GetComponentInChildren<FenceSegment>(true);
            }
        }

        return installedFenceSegment;
    }

    private bool TryPlaceFence()
    {
        if (!isUnlocked || HasFence || startingFenceData == null || startingFenceData.FencePrefab == null)
        {
            return false;
        }

        Transform origin = SpawnPoint;
        GameObject spawnedFence = Instantiate(startingFenceData.FencePrefab, origin.position, origin.rotation);
        installedFence = spawnedFence;
        installedFenceSegment = null;
        RefreshSlotVisualState();
        Debug.Log($"[FenceSlot] Placed fence '{spawnedFence.name}' in slot '{name}'.", this);
        return true;
    }

    private void RefreshSlotVisualState()
    {
        if (slotMarker != null)
        {
            slotMarker.gameObject.SetActive(!HasFence);
        }
    }
}
