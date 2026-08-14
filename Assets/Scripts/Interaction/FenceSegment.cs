using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class FenceSegment : MonoBehaviour, IStructureHpSource
{
    [Header("Fence Data")]
    [SerializeField, Tooltip("Authoritative design-time data for this fence. Sole source of MaxHp/repair values.")]
    private FenceData fenceData;

    // Runtime State
    [Header("Fence Runtime State")]
    [SerializeField] private float currentHp;

    [SerializeField, Tooltip("Explicit broken state; also toggles barrier colliders. HP at 0 when true during play.")]
    private bool isDestroyed;

    [Header("Fence Visual State")]
    [SerializeField, Tooltip("Normal fence visual root. Active while the fence is not destroyed.")]
    private GameObject normalVisualRoot;

    [SerializeField, Tooltip("Destroyed fence visual root. Active only while the fence is destroyed.")]
    private GameObject destroyedVisualRoot;

    /// <summary>Non-trigger colliders: disabled when <see cref="isDestroyed"/> is true.</summary>
    private Collider[] cachedBarrierColliders;

    [Header("Interaction (IInteractable Example)")]
    [SerializeField, Tooltip("Optional world-space anchor for this fence prompt.")]
    private Transform uiAnchor;

    [Header("Screen-Space HP Bar")]
    [SerializeField, Tooltip("World-space target tracked by the screen-space HP bar. If null, uses uiAnchor or this transform.")]
    private Transform hpBarAnchor;

    [FormerlySerializedAs("dayTimerBarPrefab")]
    [SerializeField, Tooltip("Screen-space structure HP bar prefab. Assign the same Placeholder Bar prefab used by TowerSlot.")]
    private GameObject structureHpBarPrefab;

    private GameObject hpBarInstance;
    private WorldGatherBar hpBarPresenter;
    private static RectTransform cachedStructureHpBarsRoot;

    public FenceData Data => fenceData;
    public int CurrentTierNumber => fenceData != null ? fenceData.TierNumber : 1;
    public float CurrentHp => currentHp;
    public float MaxHp => fenceData != null ? Mathf.Max(1f, fenceData.MaxHp) : 1f;
    public bool IsDestroyed => isDestroyed;
    public Transform HpAnchorTransform => hpBarAnchor != null ? hpBarAnchor : uiAnchor != null ? uiAnchor : transform;

    /// <summary>Next-tier fence data, if this fence's data has one configured. Null at the maximum tier.</summary>
    public FenceData NextTierData => fenceData != null ? fenceData.NextFence : null;

    private void Reset()
    {
        isDestroyed = false;
        currentHp = fenceData != null ? Mathf.Max(1f, fenceData.MaxHp) : 1f;
    }

    private void Awake()
    {
        EnsureValidState();
        CacheBarrierColliders();
        TrySpawnScreenSpaceHpBar();
        RefreshScreenSpaceHpBar();
        ApplyDestroyedState();
    }

    private void OnDestroy()
    {
        if (hpBarInstance != null)
        {
            Destroy(hpBarInstance);
        }
    }

    private void OnValidate()
    {
        EnsureValidState();
        if (fenceData == null)
        {
            Debug.LogWarning($"[{nameof(FenceSegment)}] '{name}' has no FenceData assigned. MaxHp/repair values will fall back to 1/0.", this);
        }
        else if (fenceData.MaxHp <= 0f)
        {
            Debug.LogWarning($"[{nameof(FenceSegment)}] '{name}' has FenceData '{fenceData.name}' with non-positive MaxHp.", this);
        }

        if (hpBarInstance != null)
        {
            RefreshScreenSpaceHpBar();
        }
    }

    // Damage / Repair
    public void TakeDamage(float amount)
    {
        EnsureValidState();

        if (amount <= 0f)
        {
            Debug.LogWarning($"[{nameof(FenceSegment)}] {name} ignored non-positive damage: {amount}.");
            return;
        }

        float oldHp = currentHp;
        currentHp = Mathf.Max(0f, currentHp - amount);
        if (currentHp <= 0f && !isDestroyed)
        {
            isDestroyed = true;
        }

        ApplyDestroyedState();
        Debug.Log(
            $"[{nameof(FenceSegment)}] {name} took {amount} damage. HP {oldHp:0.#} -> {currentHp:0.#}/{MaxHp:0.#}. " +
            $"Destroyed: {IsDestroyed}.");

        RefreshScreenSpaceHpBar();
    }

    void CacheBarrierColliders()
    {
        if (cachedBarrierColliders != null)
        {
            return;
        }

        Collider[] all = GetComponentsInChildren<Collider>(true);
        List<Collider> barrier = new List<Collider>(all != null ? all.Length : 0);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Collider c = all[i];
                if (c != null && !c.isTrigger)
                {
                    barrier.Add(c);
                }
            }
        }

        cachedBarrierColliders = barrier.ToArray();
    }

    void ApplyDestroyedState()
    {
        if (normalVisualRoot != null)
        {
            normalVisualRoot.SetActive(!isDestroyed);
        }

        if (destroyedVisualRoot != null)
        {
            destroyedVisualRoot.SetActive(isDestroyed);
        }

        if (cachedBarrierColliders == null)
        {
            return;
        }

        for (int i = 0; i < cachedBarrierColliders.Length; i++)
        {
            Collider c = cachedBarrierColliders[i];
            if (c != null)
            {
                c.enabled = !isDestroyed;
            }
        }
    }

    void ClearDestroyedStateIfRepaired()
    {
        if (isDestroyed && currentHp > 0f)
        {
            isDestroyed = false;
            ApplyDestroyedState();
        }
    }

    /// <summary>Sets CurrentHp directly (clamped to [0, MaxHp]). Used by FenceSlot.TryUpgradeFence to
    /// carry the pre-upgrade HP ratio over to the newly instantiated tier.</summary>
    public void SetCurrentHp(float hp)
    {
        currentHp = Mathf.Clamp(hp, 0f, MaxHp);
        ClearDestroyedStateIfRepaired();
        ApplyDestroyedState();
        RefreshScreenSpaceHpBar();
    }

    public bool CanRepair()
    {
        EnsureValidState();
        return currentHp < MaxHp;
    }

    public bool IsDamaged
    {
        get
        {
            EnsureValidState();
            return currentHp < MaxHp;
        }
    }

    public bool IsFullyRepaired()
    {
        EnsureValidState();
        return currentHp >= MaxHp;
    }

    public int GetRequiredWood()
    {
        return fenceData != null ? Mathf.Max(0, fenceData.RepairWoodCost) : 0;
    }

    public int GetRequiredScrap()
    {
        return fenceData != null ? Mathf.Max(0, fenceData.RepairScrapCost) : 0;
    }

    public bool ShouldShowWoodCost()
    {
        return GetRequiredWood() > 0;
    }

    public bool ShouldShowScrapCost()
    {
        return GetRequiredScrap() > 0;
    }

    public float GetRepairAmount()
    {
        return fenceData != null ? Mathf.Max(0f, fenceData.RepairAmount) : 0f;
    }

    // Display helper only. Core repair logic uses numeric getters and checks.
    public string GetRepairRequirementText()
    {
        return $"Repair Cost - Wood: {GetRequiredWood()}, Scrap: {GetRequiredScrap()}";
    }

    public bool HasEnoughResources(ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return false;
        }

        int requiredWood = GetRequiredWood();
        int requiredScrap = GetRequiredScrap();

        int availableWood = resourceManager.GetAmount(ResourceType.Wood);
        int availableScrap = resourceManager.GetAmount(ResourceType.Scrap);

        return availableWood >= requiredWood && availableScrap >= requiredScrap;
    }

    public bool TryRepair(ResourceManager resourceManager)
    {
        EnsureValidState();

        if (!CanRepair())
        {
            Debug.Log($"[{nameof(FenceSegment)}] {name} is already at full HP ({currentHp:0.#}/{MaxHp:0.#}). Repair skipped.");
            return false;
        }

        if (resourceManager == null)
        {
            Debug.LogWarning($"[{nameof(FenceSegment)}] {name} cannot repair: no {nameof(ResourceManager)} provided.");
            return false;
        }

        int requiredWood = GetRequiredWood();
        int requiredScrap = GetRequiredScrap();
        float repairAmount = GetRepairAmount();
        int availableWood = resourceManager.GetAmount(ResourceType.Wood);
        int availableScrap = resourceManager.GetAmount(ResourceType.Scrap);

        if (repairAmount <= 0f)
        {
            Debug.LogWarning($"[{nameof(FenceSegment)}] {name} repair aborted: configured repair amount is not positive.");
            return false;
        }

        // Numeric-only core check for repair affordability.
        if (availableWood < requiredWood || availableScrap < requiredScrap)
        {
            Debug.Log(
                $"[{nameof(FenceSegment)}] {name} repair failed. Need Wood {requiredWood} (have {availableWood}), " +
                $"Scrap {requiredScrap} (have {availableScrap}).");
            return false;
        }

        bool spentWood = requiredWood == 0 || resourceManager.TrySpendResource(ResourceType.Wood, requiredWood);
        bool spentScrap = requiredScrap == 0 || resourceManager.TrySpendResource(ResourceType.Scrap, requiredScrap);

        if (!spentWood || !spentScrap)
        {
            if (spentWood && requiredWood > 0)
            {
                resourceManager.AddResource(ResourceType.Wood, requiredWood);
            }
            if (spentScrap && requiredScrap > 0)
            {
                resourceManager.AddResource(ResourceType.Scrap, requiredScrap);
            }
            Debug.LogWarning($"[{nameof(FenceSegment)}] {name} repair aborted: failed to spend required resources.");
            return false;
        }

        float oldHp = currentHp;
        currentHp = Mathf.Min(MaxHp, currentHp + repairAmount);
        ClearDestroyedStateIfRepaired();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[{nameof(FenceSegment)}] TryRepair {GetFenceDebugContext()}. HP {oldHp:0.#} -> {currentHp:0.#}/{MaxHp:0.#}. " +
            $"Spent Wood {requiredWood}, Scrap {requiredScrap}.",
            this);
#else
        Debug.Log(
            $"[{nameof(FenceSegment)}] {name} repaired successfully. Spent Wood {requiredWood}, Scrap {requiredScrap}. " +
            $"HP {oldHp:0.#} -> {currentHp:0.#}/{MaxHp:0.#}.");
#endif

        RefreshScreenSpaceHpBar();
        return true;
    }

    private string GetFenceDebugContext()
    {
        PersistentId persistentId = GetComponent<PersistentId>();
        if (persistentId == null)
        {
            persistentId = GetComponentInParent<PersistentId>();
        }

        if (persistentId != null && !string.IsNullOrWhiteSpace(persistentId.Id))
        {
            return $"{name} (id={persistentId.Id})";
        }

        return name;
    }

    void TrySpawnScreenSpaceHpBar()
    {
        if (hpBarInstance != null)
        {
            return;
        }

        if (structureHpBarPrefab == null)
        {
            return;
        }

        RectTransform parent = ResolveStructureHpBarsRoot();
        if (parent == null)
        {
            Debug.LogWarning(
                $"[{nameof(FenceSegment)}] {name}: no screen-space StructureHpBarsRoot was found. Fence HP bar was not created.",
                this);
            return;
        }

        hpBarInstance = Instantiate(structureHpBarPrefab, parent);
        hpBarInstance.name = $"{structureHpBarPrefab.name}_HP_{name}";
        hpBarPresenter = hpBarInstance.GetComponent<WorldGatherBar>();
        if (hpBarPresenter == null)
        {
            Debug.LogWarning(
                $"[{nameof(FenceSegment)}] {name}: structure HP bar prefab '{structureHpBarPrefab.name}' has no {nameof(WorldGatherBar)} component.",
                this);
            Destroy(hpBarInstance);
            hpBarInstance = null;
            return;
        }

        hpBarPresenter.Initialize(parent, HpAnchorTransform);
    }

    static RectTransform ResolveStructureHpBarsRoot()
    {
        if (cachedStructureHpBarsRoot != null)
        {
            return cachedStructureHpBarsRoot;
        }

        GameObject root = GameObject.Find("StructureHpBarsRoot");
        if (root != null && root.TryGetComponent(out RectTransform rootRect))
        {
            cachedStructureHpBarsRoot = rootRect;
            return cachedStructureHpBarsRoot;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null
                && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                && canvas.transform is RectTransform canvasRect)
            {
                cachedStructureHpBarsRoot = canvasRect;
                return cachedStructureHpBarsRoot;
            }
        }

        return null;
    }

    void RefreshScreenSpaceHpBar()
    {
        if (hpBarPresenter == null)
        {
            return;
        }

        EnsureValidState();

        float max = Mathf.Max(0.01f, MaxHp);
        float fill = Mathf.Clamp01(currentHp / max);
        if (currentHp < max)
        {
            hpBarPresenter.SetProgress(fill);
            hpBarPresenter.Show();
        }
        else
        {
            hpBarPresenter.HideInstant();
        }
    }

    // Validation
    private void EnsureValidState()
    {
        currentHp = Mathf.Clamp(currentHp, 0f, MaxHp);
        if (currentHp > 0f && isDestroyed)
        {
            isDestroyed = false;
            ApplyDestroyedState();
        }
    }
}
