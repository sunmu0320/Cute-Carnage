using UnityEngine;

[DisallowMultipleComponent]
public class TowerSlot : MonoBehaviour, IInteractable
{
    public const string ArrowTowerTowerId = "ArrowTower";

    [Header("Slot State")]
    [SerializeField]
    private bool hasTower;

    [SerializeField]
    private GameObject towerPrefab;

    [SerializeField]
    private Transform spawnPoint;

    private GameObject currentTower;

    [Header("Build Cost")]
    [SerializeField]
    private int woodBuildCost = 1;

    [SerializeField]
    private int scrapBuildCost = 1;

    [SerializeField]
    private GameObject slotVisualRoot;

    [SerializeField]
    private Transform slotMarker;

    private PersistentId persistentId;

    public bool HasTower => hasTower;
    public string PersistentSlotId => persistentId != null ? persistentId.Id : string.Empty;
    public string PersistentId => PersistentSlotId;

    private void Awake()
    {
        CachePersistentId();
        RefreshSlotVisualState();
    }

    private void OnValidate()
    {
        CachePersistentId();
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

    public Transform GetUIAnchor()
    {
        return spawnPoint != null ? spawnPoint : transform;
    }

    public Vector3 GetInteractPosition()
    {
        return GetUIAnchor().position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !hasTower;
    }

    public InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        if (hasTower)
        {
            return InteractablePromptData.CreateSimple("Tower Installed");
        }

        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        int wood = Mathf.Max(0, woodBuildCost);
        int scrap = Mathf.Max(0, scrapBuildCost);
        bool canAfford = HasRequiredBuildResources(resourceManager);
        return new InteractablePromptData
        {
            actionText = "Press E to build Tower",
            woodCost = wood,
            scrapCost = scrap,
            canAfford = canAfford
        };
    }

    public void Interact(PlayerInteractor interactor)
    {
        TryBuildTower(interactor);
    }

    public bool TryBuildTower(PlayerInteractor interactor)
    {
        if (hasTower)
        {
            return false;
        }

        if (towerPrefab == null)
        {
            Debug.LogWarning("[TowerSlot] towerPrefab is not assigned.", this);
            return false;
        }

        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        if (resourceManager == null)
        {
            Debug.LogWarning($"[TowerSlot] slot id='{PersistentId}' cannot build: missing ResourceManager.", this);
            return false;
        }

        int reqWood = Mathf.Max(0, woodBuildCost);
        int reqScrap = Mathf.Max(0, scrapBuildCost);

        if (!HasRequiredBuildResources(resourceManager))
        {
            Debug.Log(
                $"[TowerSlot] Build failed slot id='{PersistentId}'. Need wood={reqWood} scrap={reqScrap}. " +
                $"Current {resourceManager.GetDebugSummary()}",
                this);
            return false;
        }

        if (reqWood > 0 && !resourceManager.TrySpendResource(ResourceType.Wood, reqWood))
        {
            Debug.LogWarning($"[TowerSlot] slot id='{PersistentId}' failed to spend wood={reqWood}.", this);
            return false;
        }

        if (reqScrap > 0 && !resourceManager.TrySpendResource(ResourceType.Scrap, reqScrap))
        {
            if (reqWood > 0)
            {
                resourceManager.AddResource(ResourceType.Wood, reqWood);
            }

            Debug.LogWarning($"[TowerSlot] slot id='{PersistentId}' failed to spend scrap={reqScrap}.", this);
            return false;
        }

        PlaceTowerInternal(ArrowTowerTowerId);
        Debug.Log(
            $"[TowerSlot] Build success slot id='{PersistentId}'. Spent wood={reqWood} scrap={reqScrap}. After {resourceManager.GetDebugSummary()}",
            this);
        return true;
    }

    /// <summary>Gameplay convenience: places the configured ArrowTower prefab after resources were spent elsewhere.</summary>
    public void PlaceTowerInternal()
    {
        PlaceTowerInternal(ArrowTowerTowerId);
    }

    /// <summary>Spawns the tower without spending resources (runtime restore / internal).</summary>
    public void PlaceTowerInternal(string towerId)
    {
        if (hasTower)
        {
            return;
        }

        GameObject prefab = ResolveTowerPrefab(towerId);
        if (prefab == null)
        {
            Debug.LogWarning($"[TowerSlot] slot id='{PersistentId}' cannot place towerId='{towerId}' (no prefab).", this);
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        currentTower = Instantiate(prefab, origin.position, origin.rotation);
        hasTower = true;
        RefreshSlotVisualState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TowerSlot] PlaceTowerInternal slot id='{PersistentId}' towerId='{towerId}' placed '{currentTower.name}'.", this);
#endif
    }

    public TowerSlotRuntimeState CreateRuntimeState()
    {
        bool occupied = hasTower || currentTower != null;
        return new TowerSlotRuntimeState
        {
            id = PersistentId,
            hasTower = occupied,
            towerId = occupied ? ArrowTowerTowerId : string.Empty,
            level = 1
        };
    }

    public void ApplyRuntimeState(TowerSlotRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state.id) || state.id != PersistentId)
        {
            return;
        }

        if (!state.hasTower)
        {
            return;
        }

        if (hasTower)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[TowerSlot] ApplyRuntimeState slot id='{PersistentId}' incoming hasTower={state.hasTower} towerId='{state.towerId}' " +
                "restored=False skipped=True (tower already exists on slot).",
                this);
#endif
            return;
        }

        bool hadTowerBeforePlace = hasTower;
        PlaceTowerInternal(state.towerId);
        bool restored = !hadTowerBeforePlace && hasTower;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[TowerSlot] ApplyRuntimeState slot id='{PersistentId}' incoming hasTower={state.hasTower} towerId='{state.towerId}' " +
            $"restored={restored} skipped=False",
            this);
#endif
    }

    private GameObject ResolveTowerPrefab(string towerId)
    {
        if (string.IsNullOrWhiteSpace(towerId) || towerId == ArrowTowerTowerId)
        {
            return towerPrefab;
        }

        Debug.LogWarning($"[TowerSlot] slot id='{PersistentId}' unsupported towerId='{towerId}'.", this);
        return null;
    }

    private bool HasRequiredBuildResources(ResourceManager resourceManager)
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

    private void RefreshSlotVisualState()
    {
        if (slotVisualRoot != null)
        {
            slotVisualRoot.SetActive(!hasTower);
        }

        if (slotMarker != null)
        {
            slotMarker.gameObject.SetActive(!hasTower);
        }
    }
}
