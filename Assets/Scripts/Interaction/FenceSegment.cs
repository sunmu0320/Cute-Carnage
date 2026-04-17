using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class FenceTierData
{
    [SerializeField] private string tierName = "Tier 1";
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float repairAmount = 30f;
    [SerializeField] private int woodCost = 1;
    [SerializeField] private int scrapCost = 0;

    public string TierName => string.IsNullOrWhiteSpace(tierName) ? "Unnamed Tier" : tierName;
    public float MaxHp => Mathf.Max(1f, maxHp);
    public float RepairAmount => Mathf.Max(0f, repairAmount);
    public int WoodCost => Mathf.Max(0, woodCost);
    public int ScrapCost => Mathf.Max(0, scrapCost);

    public FenceTierData()
    {
    }

    public FenceTierData(string tierName, float maxHp, float repairAmount, int woodCost, int scrapCost)
    {
        this.tierName = tierName;
        this.maxHp = maxHp;
        this.repairAmount = repairAmount;
        this.woodCost = woodCost;
        this.scrapCost = scrapCost;
    }
}

public class FenceSegment : MonoBehaviour, IInteractable, IRepairable
{
    // Tier Data
    [Header("Fence Tiers")]
    [SerializeField] private List<FenceTierData> tiers = new List<FenceTierData>
    {
        new FenceTierData(), // Tier 1 default: 100 HP, repair 30, wood 1, scrap 0
        new FenceTierData(), // Override values below in Reset() for clear startup data
        new FenceTierData(),
        new FenceTierData()
    };

    // Runtime State
    [Header("Fence Runtime State")]
    [SerializeField, Tooltip("0-based index of the active tier.")] private int currentTierIndex;
    [SerializeField] private float currentHp;

    [Header("Interaction (IInteractable Example)")]
    [SerializeField, Tooltip("Optional world-space anchor for this fence prompt.")]
    private Transform uiAnchor;

    [Header("World HP Bar (DayTimerBar)")]
    [SerializeField, Tooltip("Optional anchor for the HP bar. If null, uses uiAnchor or this transform.")]
    private Transform hpBarAnchor;

    [SerializeField, Tooltip("Local position when hpBarAnchor is null (bar sits below prompt anchor).")]
    private Vector3 hpBarLocalOffset = new Vector3(0f, -0.35f, 0f);

    [SerializeField, Tooltip("Reuse DayTimerBar prefab as a world-space HP fill bar.")]
    private GameObject dayTimerBarPrefab;

    [SerializeField, Tooltip("Lower sorting so WorldPromptUI can draw above the bar.")]
    private int hpBarCanvasSortingOrder = -10;

    [SerializeField, Tooltip("Hide the bar when HP is full.")]
    private bool hideHpBarWhenFull = true;

    private GameObject hpBarInstance;
    private Image hpFillImage;

    [Header("Continuous Repair (Repair State)")]
    [SerializeField, Tooltip("HP restored per second while the player is in repair state.")]
    private float repairRatePerSecond = 10f;

    [SerializeField, Tooltip("Max HP restored per paid resource chunk before another spend is required.")]
    private float repairHpPerWood = 20f;

    [SerializeField, Tooltip("Resource spent when a new repair chunk starts.")]
    private ResourceType repairResourceType = ResourceType.Wood;

    [SerializeField, Tooltip("How much of the repair resource to spend per chunk.")]
    private int repairCostPerChunk = 1;

    private bool hasActiveRepairChunk;
    private float repairedInCurrentChunk;

    public int CurrentTierNumber => currentTierIndex + 1;
    public float CurrentHp => currentHp;
    public float MaxHp => CurrentTier.MaxHp;
    public bool IsDestroyed => currentHp <= 0f;

    private FenceTierData CurrentTier
    {
        get
        {
            EnsureValidState();
            return tiers[currentTierIndex];
        }
    }

    private void Reset()
    {
        tiers = new List<FenceTierData>
        {
            new FenceTierData("Tier 1", 100f, 30f, 1, 0),
            new FenceTierData("Tier 2", 160f, 30f, 2, 0),
            new FenceTierData("Tier 3", 210f, 30f, 1, 1),
            new FenceTierData("Tier 4", 260f, 30f, 1, 2)
        };

        currentTierIndex = 0;
        currentHp = tiers[0].MaxHp;
    }

    private void Awake()
    {
        EnsureValidState();
        TrySpawnWorldHpBar();
        RefreshWorldHpBar();
    }

    private void OnValidate()
    {
        EnsureValidState();
        if (hpBarInstance != null)
        {
            RefreshWorldHpBar();
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
        Debug.Log(
            $"[{nameof(FenceSegment)}] {name} took {amount} damage. HP {oldHp:0.#} -> {currentHp:0.#}/{MaxHp:0.#}. " +
            $"Destroyed: {IsDestroyed}.");

        RefreshWorldHpBar();
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

    public bool CanAffordNextRepairChunk(ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return false;
        }

        int cost = Mathf.Max(0, repairCostPerChunk);
        if (cost <= 0)
        {
            return true;
        }

        return resourceManager.HasResource(repairResourceType, cost);
    }

    public int GetRequiredWood()
    {
        return CurrentTier.WoodCost;
    }

    public int GetRequiredScrap()
    {
        return CurrentTier.ScrapCost;
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
        return CurrentTier.RepairAmount;
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

    public bool TickRepair(float deltaTime, ResourceManager resourceManager)
    {
        try
        {
            EnsureValidState();

            if (!IsDamaged)
            {
                return true;
            }

            if (resourceManager == null)
            {
                return false;
            }

            float dt = Mathf.Max(0f, deltaTime);
            if (dt <= 0f)
            {
                return true;
            }

            if (!hasActiveRepairChunk)
            {
                int cost = Mathf.Max(0, repairCostPerChunk);
                if (cost > 0 && !resourceManager.TrySpendResource(repairResourceType, cost))
                {
                    return false;
                }

                hasActiveRepairChunk = true;
                repairedInCurrentChunk = 0f;
            }

            float rate = Mathf.Max(0f, repairRatePerSecond);
            float repairAmount = rate * dt;

            float remainingMissingHp = MaxHp - currentHp;
            float chunkCap = Mathf.Max(0.01f, repairHpPerWood);
            float remainingChunkHp = chunkCap - repairedInCurrentChunk;

            repairAmount = Mathf.Min(repairAmount, remainingMissingHp);
            repairAmount = Mathf.Min(repairAmount, Mathf.Max(0f, remainingChunkHp));

            currentHp += repairAmount;
            currentHp = Mathf.Min(currentHp, MaxHp);
            repairedInCurrentChunk += repairAmount;

            if (repairedInCurrentChunk >= chunkCap - 0.001f)
            {
                hasActiveRepairChunk = false;
                repairedInCurrentChunk = 0f;
            }

            if (!IsDamaged)
            {
                return true;
            }

            if (!hasActiveRepairChunk)
            {
                int cost = Mathf.Max(0, repairCostPerChunk);
                if (cost > 0 && !resourceManager.HasResource(repairResourceType, cost))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            RefreshWorldHpBar();
        }
    }

    public void CancelRepair()
    {
        hasActiveRepairChunk = false;
        repairedInCurrentChunk = 0f;
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
        int availableWood = resourceManager.GetAmount(ResourceType.Wood);
        int availableScrap = resourceManager.GetAmount(ResourceType.Scrap);

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
            Debug.LogWarning($"[{nameof(FenceSegment)}] {name} repair aborted: failed to spend required resources.");
            return false;
        }

        float oldHp = currentHp;
        currentHp = Mathf.Min(MaxHp, currentHp + GetRepairAmount());
        Debug.Log(
            $"[{nameof(FenceSegment)}] {name} repaired successfully. Spent Wood {requiredWood}, Scrap {requiredScrap}. " +
            $"HP {oldHp:0.#} -> {currentHp:0.#}/{MaxHp:0.#}.");

        RefreshWorldHpBar();
        return true;
    }

    public void UpgradeToTier(int newTierNumber, bool fillHpToMax = true)
    {
        EnsureValidState();

        int requestedIndex = newTierNumber - 1;
        if (requestedIndex < 0 || requestedIndex >= tiers.Count)
        {
            Debug.LogWarning(
                $"[{nameof(FenceSegment)}] {name} upgrade failed. Tier {newTierNumber} is out of range (1-{tiers.Count}).");
            return;
        }

        int oldTierNumber = CurrentTierNumber;
        float oldHp = currentHp;

        currentTierIndex = requestedIndex;
        if (fillHpToMax)
        {
            currentHp = MaxHp;
        }
        else
        {
            currentHp = Mathf.Clamp(currentHp, 0f, MaxHp);
        }

        Debug.Log(
            $"[{nameof(FenceSegment)}] {name} upgraded Tier {oldTierNumber} -> {CurrentTierNumber} ({CurrentTier.TierName}). " +
            $"HP {oldHp:0.#} -> {currentHp:0.#}/{MaxHp:0.#} (fillHpToMax={fillHpToMax}).");

        RefreshWorldHpBar();
    }

    void TrySpawnWorldHpBar()
    {
        if (hpBarInstance != null)
        {
            return;
        }

        if (dayTimerBarPrefab == null)
        {
            return;
        }

        Transform parent = hpBarAnchor != null ? hpBarAnchor : uiAnchor != null ? uiAnchor : transform;
        hpBarInstance = Instantiate(dayTimerBarPrefab);
        hpBarInstance.name = $"{dayTimerBarPrefab.name}_HP_{name}";

        Transform barTransform = hpBarInstance.transform;
        barTransform.SetParent(parent, false);
        barTransform.localRotation = Quaternion.identity;

        if (hpBarAnchor != null)
        {
            barTransform.localPosition = Vector3.zero;
        }
        else
        {
            barTransform.localPosition = hpBarLocalOffset;
        }

        Canvas barCanvas = hpBarInstance.GetComponent<Canvas>();
        if (barCanvas != null)
        {
            barCanvas.sortingOrder = hpBarCanvasSortingOrder;
        }

        CacheHpFillImageFromPrefab();
    }

    void CacheHpFillImageFromPrefab()
    {
        hpFillImage = null;
        if (hpBarInstance == null)
        {
            return;
        }

        foreach (Image image in hpBarInstance.GetComponentsInChildren<Image>(true))
        {
            if (image != null && image.type == Image.Type.Filled)
            {
                hpFillImage = image;
                break;
            }
        }

        if (hpFillImage == null)
        {
            Debug.LogWarning(
                $"[{nameof(FenceSegment)}] {name}: DayTimerBar prefab has no Image with Type Filled (expected BarFill). HP bar will not update.",
                this);
        }
    }

    void RefreshWorldHpBar()
    {
        if (hpBarInstance == null)
        {
            return;
        }

        EnsureValidState();

        float max = Mathf.Max(0.01f, MaxHp);
        float fill = Mathf.Clamp01(currentHp / max);

        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = fill;
        }

        bool visible = true;

        if (hideHpBarWhenFull)
        {
            visible = IsDamaged;
        }

        hpBarInstance.SetActive(visible);
    }

    // Interaction / UI
    public Transform GetUIAnchor()
    {
        return uiAnchor != null ? uiAnchor : transform;
    }

    public Vector3 GetInteractPosition()
    {
        return transform.position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        // Keep fence targetable so status prompts can show at broken/damaged/full states.
        return true;
    }

    public InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        ResourceManager manager = interactor != null ? interactor.ResourceManager : null;
        bool canAfford = HasEnoughResources(manager);
        string actionText;
        bool isFullHealth = currentHp >= MaxHp;
        int woodCost = GetRequiredWood();
        int scrapCost = GetRequiredScrap();

        if (IsDestroyed)
        {
            actionText = "Fence is broken - Press E to Repair";
        }
        else if (!isFullHealth)
        {
            actionText = "Press E to Repair";
        }
        else
        {
            actionText = "Fence is fully repaired";
            woodCost = 0;
            scrapCost = 0;
            canAfford = true;
        }

        return new InteractablePromptData
        {
            actionText = actionText,
            woodCost = woodCost,
            scrapCost = scrapCost,
            canAfford = canAfford
        };
    }

    public void Interact(PlayerInteractor interactor)
    {
        // HP repair during play uses IRepairable.TickRepair from PlayerInteractor while in Repairing state.
    }

    // Future icon-based UI usage example (when player is near this fence):
    // if (fenceSegment.ShouldShowWoodCost())  show wood icon with count fenceSegment.GetRequiredWood();
    // if (fenceSegment.ShouldShowScrapCost()) show scrap icon with count fenceSegment.GetRequiredScrap();
    // bool canAfford = fenceSegment.HasEnoughResources(resourceManager); // color icons green/red.
    // Validation
    private void EnsureValidState()
    {
        if (tiers == null)
        {
            tiers = new List<FenceTierData>();
        }

        if (tiers.Count == 0)
        {
            tiers.Add(new FenceTierData("Tier 1", 100f, 30f, 1, 0));
            Debug.LogWarning($"[{nameof(FenceSegment)}] {name} had no tiers configured. Added fallback Tier 1.");
        }

        currentTierIndex = Mathf.Clamp(currentTierIndex, 0, tiers.Count - 1);
        currentHp = Mathf.Clamp(currentHp, 0f, tiers[currentTierIndex].MaxHp);
    }
}
