using System;
using System.Collections.Generic;
using UnityEngine;

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

public class FenceSegment : MonoBehaviour, IInteractable
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
    }

    private void OnValidate()
    {
        EnsureValidState();
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
    }

    public bool CanRepair()
    {
        EnsureValidState();
        return currentHp < MaxHp;
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
        ResourceManager manager = interactor != null ? interactor.ResourceManager : null;
        TryRepair(manager);
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
