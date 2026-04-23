using UnityEngine;

[DisallowMultipleComponent]
public class FenceSlot : MonoBehaviour, IInteractable
{
    [Header("Slot State")]
    [SerializeField] private bool isUnlocked = true;
    [SerializeField] private GameObject installedFence;
    [SerializeField] private GameObject fencePrefab;

    [Header("Build Cost")]
    [SerializeField] private int woodBuildCost = 1;
    [SerializeField] private int scrapBuildCost = 0;

    [Header("Anchors")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform slotMarker;

    private PersistentId persistentId;
    private bool hasWarnedFallbackSpawnPoint;

    public bool IsUnlocked => isUnlocked;
    public bool HasFence => installedFence != null;
    public GameObject InstalledFence => installedFence;
    public Transform SlotMarker => slotMarker;
    public PersistentId PersistentIdComponent => persistentId;
    public string PersistentSlotId => persistentId != null ? persistentId.Id : string.Empty;

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
        return isUnlocked && !HasFence && fencePrefab != null;
    }

    public bool HasRequiredBuildResources(ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return false;
        }

        int requiredWood = Mathf.Max(0, woodBuildCost);
        int requiredScrap = Mathf.Max(0, scrapBuildCost);
        return resourceManager.HasResource(ResourceType.Wood, requiredWood) &&
               resourceManager.HasResource(ResourceType.Scrap, requiredScrap);
    }

    public void SetInstalledFence(GameObject fenceObject)
    {
        installedFence = fenceObject;
        RefreshSlotVisualState();
    }

    public void ClearInstalledFence()
    {
        installedFence = null;
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
        return SpawnPoint;
    }

    public Vector3 GetInteractPosition()
    {
        return GetUIAnchor().position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return isUnlocked && !HasFence;
    }

    public InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        if (!isUnlocked)
        {
            return InteractablePromptData.CreateSimple("Fence slot locked");
        }

        if (HasFence)
        {
            return InteractablePromptData.CreateSimple("Fence already installed");
        }

        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        bool canAfford = HasRequiredBuildResources(resourceManager);
        return new InteractablePromptData
        {
            actionText = "Press E to place fence",
            woodCost = Mathf.Max(0, woodBuildCost),
            scrapCost = Mathf.Max(0, scrapBuildCost),
            canAfford = canAfford
        };
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!isUnlocked)
        {
            Debug.Log($"[FenceSlot] '{name}' is locked. Placement blocked.", this);
            return;
        }

        if (HasFence)
        {
            Debug.Log($"[FenceSlot] '{name}' already has an installed fence.", this);
            return;
        }

        if (fencePrefab == null)
        {
            Debug.LogWarning($"[FenceSlot] '{name}' cannot place fence: fencePrefab is not assigned.", this);
            return;
        }

        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        if (resourceManager == null)
        {
            Debug.LogWarning($"[FenceSlot] '{name}' cannot place fence: missing ResourceManager.", this);
            return;
        }

        if (!HasRequiredBuildResources(resourceManager))
        {
            Debug.Log($"[FenceSlot] '{name}' cannot place fence: not enough resources (Wood {Mathf.Max(0, woodBuildCost)}, Scrap {Mathf.Max(0, scrapBuildCost)}).", this);
            return;
        }

        bool spentWood = resourceManager.TrySpendResource(ResourceType.Wood, Mathf.Max(0, woodBuildCost)) || Mathf.Max(0, woodBuildCost) == 0;
        bool spentScrap = resourceManager.TrySpendResource(ResourceType.Scrap, Mathf.Max(0, scrapBuildCost)) || Mathf.Max(0, scrapBuildCost) == 0;
        if (!spentWood || !spentScrap)
        {
            Debug.LogWarning($"[FenceSlot] '{name}' failed to spend build cost. Placement cancelled.", this);
            return;
        }

        TryPlaceFence();
    }

    public FenceSlotRuntimeState CreateRuntimeState()
    {
        return new FenceSlotRuntimeState
        {
            id = PersistentSlotId,
            hasFence = HasFence
        };
    }

    public void ApplyRuntimeState(FenceSlotRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state.id) || state.id != PersistentSlotId)
        {
            return;
        }

        if (state.hasFence && !HasFence)
        {
            TryPlaceFence();
        }
    }

    private bool TryPlaceFence()
    {
        if (!isUnlocked || HasFence || fencePrefab == null)
        {
            return false;
        }

        Transform origin = SpawnPoint;
        GameObject spawnedFence = Instantiate(fencePrefab, origin.position, origin.rotation);
        installedFence = spawnedFence;
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
