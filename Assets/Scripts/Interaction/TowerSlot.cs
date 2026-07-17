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

    [SerializeField]
    private Transform interactionAnchor;

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

    [SerializeField]
    private float towerReferenceSearchRadius = 2f;

    [Header("Tower HP Bar")]
    [SerializeField]
    private WorldGatherBar towerHpBarTemplate;

    [SerializeField]
    private Transform towerHpBarParent;

    [SerializeField]
    private Vector3 towerHpBarLocalOffset = new Vector3(0f, 2.8f, 0f);

    [SerializeField]
    private bool keepTowerHpBarAlwaysVisible = true;

    [SerializeField]
    private bool hideTowerHpBarWhenDestroyed = false;

    private PersistentId persistentId;
    private WorldGatherBar spawnedTowerHpBar;
    private bool hasLoggedMissingHpBarTemplate;
    private bool hasLoggedMissingHpBarParent;

    public bool HasTower => hasTower;
    public ArrowTower CurrentTower
    {
        get
        {
            TryGetCurrentTowerComponent(out ArrowTower tower);
            return tower;
        }
    }
    public int WoodBuildCost => Mathf.Max(0, woodBuildCost);
    public int ScrapBuildCost => Mathf.Max(0, scrapBuildCost);
    public string PersistentSlotId => persistentId != null ? persistentId.Id : string.Empty;
    public string PersistentId => PersistentSlotId;

    private void Awake()
    {
        CachePersistentId();
        EnsureCurrentTowerReference();
        EnsureTowerHpBarBinding();
        RefreshSlotVisualState();
    }

    private void OnValidate()
    {
        CachePersistentId();
        RefreshSlotVisualState();
        UpdateHpBarPosition();
    }

    private void Update()
    {
        SyncTowerHpToBar();
    }

    private void UpdateHpBarPosition()
    {
        if (spawnedTowerHpBar != null)
        {
            spawnedTowerHpBar.transform.localPosition = towerHpBarLocalOffset;
        }
    }

    private void SyncTowerHpToBar()
    {
        if (spawnedTowerHpBar == null)
        {
            return;
        }

        if (!hasTower || currentTower == null)
        {
            spawnedTowerHpBar.HideInstant();
            return;
        }

        if (TryGetCurrentTowerComponent(out ArrowTower tower))
        {
            if (hideTowerHpBarWhenDestroyed && tower.IsDestroyed)
            {
                spawnedTowerHpBar.HideInstant();
                return;
            }

            float ratio = tower.MaxHp > 0.01f ? tower.CurrentHp / tower.MaxHp : 0f;
            
            bool isDamaged = tower.CurrentHp < tower.MaxHp - 0.01f;
            bool shouldShow = keepTowerHpBarAlwaysVisible || isDamaged;

            if (shouldShow)
            {
                spawnedTowerHpBar.Show();
                spawnedTowerHpBar.SetProgress(ratio);
            }
            else
            {
                spawnedTowerHpBar.HideInstant();
            }
        }
        else
        {
            spawnedTowerHpBar.HideInstant();
        }
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
        return interactionAnchor != null ? interactionAnchor : spawnPoint != null ? spawnPoint : transform;
    }

    public Vector3 GetInteractPosition()
    {
        return spawnPoint != null ? spawnPoint.position : transform.position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return GameManager.Instance != null && GameManager.Instance.IsDay;
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
        if (interactor != null)
        {
            interactor.OpenStructureActionPanel(this);
        }
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
        EnsureTowerHpBarBinding();
        RefreshSlotVisualState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TowerSlot] PlaceTowerInternal slot id='{PersistentId}' towerId='{towerId}' placed '{currentTower.name}'.", this);
#endif
    }

    public TowerSlotRuntimeState CreateRuntimeState()
    {
        EnsureCurrentTowerReference();
        bool occupied = hasTower || currentTower != null;
        float capturedHp = 0f;
        bool capturedDestroyed = false;
        if (occupied && TryGetCurrentTowerComponent(out ArrowTower tower))
        {
            capturedHp = tower.CurrentHp;
            capturedDestroyed = tower.IsDestroyed;
        }

        return new TowerSlotRuntimeState
        {
            id = PersistentId,
            hasTower = occupied,
            towerId = occupied ? ArrowTowerTowerId : string.Empty,
            level = 1,
            currentHp = capturedHp,
            isDestroyed = capturedDestroyed
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

        EnsureCurrentTowerReference();
        if (hasTower)
        {
            ApplyTowerDurabilityState(state);
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
        EnsureCurrentTowerReference();
        ApplyTowerDurabilityState(state);
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

    private bool TryGetCurrentTowerComponent(out ArrowTower tower)
    {
        tower = null;
        if (currentTower == null)
        {
            return false;
        }

        tower = currentTower.GetComponentInChildren<ArrowTower>(true);
        return tower != null;
    }

    private void ApplyTowerDurabilityState(TowerSlotRuntimeState state)
    {
        if (state == null || !state.hasTower)
        {
            return;
        }

        if (!TryGetCurrentTowerComponent(out ArrowTower tower))
        {
            return;
        }

        tower.ApplyRuntimeDurability(state.currentHp, state.isDestroyed);
        EnsureTowerHpBarBinding();
    }

    private void EnsureCurrentTowerReference()
    {
        if (currentTower != null)
        {
            hasTower = true;
            return;
        }

        if (!hasTower)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        ArrowTower[] sceneTowers = FindObjectsByType<ArrowTower>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        float maxDistanceSqr = Mathf.Max(0.01f, towerReferenceSearchRadius) * Mathf.Max(0.01f, towerReferenceSearchRadius);
        float bestDistanceSqr = float.MaxValue;
        ArrowTower nearest = null;

        for (int i = 0; i < sceneTowers.Length; i++)
        {
            ArrowTower candidate = sceneTowers[i];
            if (candidate == null)
            {
                continue;
            }

            float distSqr = (candidate.transform.position - origin.position).sqrMagnitude;
            if (distSqr > maxDistanceSqr || distSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distSqr;
            nearest = candidate;
        }

        if (nearest != null)
        {
            currentTower = nearest.gameObject;
            EnsureTowerHpBarBinding();
        }
        else
        {
            hasTower = false;
        }
    }

    private void EnsureTowerHpBarBinding()
    {
        if (!TryGetCurrentTowerComponent(out ArrowTower tower))
        {
            return;
        }

        if (towerHpBarTemplate == null)
        {
            if (!hasLoggedMissingHpBarTemplate)
            {
                hasLoggedMissingHpBarTemplate = true;
                Debug.LogWarning($"[TowerSlot] slot id='{PersistentId}' missing Tower HP bar template. Assign 'towerHpBarTemplate'.", this);
            }
            return;
        }

        if (spawnedTowerHpBar == null)
        {
            Transform parent = towerHpBarParent != null ? towerHpBarParent : towerHpBarTemplate.transform.parent;
            if (parent == null)
            {
                Canvas fallbackCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
                if (fallbackCanvas != null)
                {
                    parent = fallbackCanvas.transform;
                }
            }

            if (parent == null)
            {
                if (!hasLoggedMissingHpBarParent)
                {
                    hasLoggedMissingHpBarParent = true;
                    Debug.LogWarning(
                        $"[TowerSlot] slot id='{PersistentId}' has no HP bar parent/canvas. Assign 'towerHpBarParent' or place template under a Canvas.",
                        this);
                }
                return;
            }

            spawnedTowerHpBar = Instantiate(towerHpBarTemplate, parent);
            spawnedTowerHpBar.name = $"{towerHpBarTemplate.name}_{PersistentId}_TowerHp";
            spawnedTowerHpBar.transform.localPosition = towerHpBarLocalOffset;
            spawnedTowerHpBar.gameObject.SetActive(true);
        }
    }
}
