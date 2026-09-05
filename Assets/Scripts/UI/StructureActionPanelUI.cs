using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StructureActionPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject hpSection;
    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private GameObject dayActionsRoot;
    [SerializeField] private Button installButton;
    [SerializeField] private TextMeshProUGUI installButtonText;
    [SerializeField] private TextMeshProUGUI installCostText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private GameObject repairActionRoot;
    [SerializeField] private Button repairButton;
    [SerializeField] private Button closeButton;

    [Header("Fence Panel (Phase 1)")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI tierBadgeText;
    [SerializeField] private TextMeshProUGUI repairAmountText;
    [SerializeField] private TextMeshProUGUI upgradeWoodCountText;
    [SerializeField] private TextMeshProUGUI upgradeScrapCountText;
    [SerializeField] private TextMeshProUGUI repairWoodCountText;
    [SerializeField] private TextMeshProUGUI repairScrapCountText;

    [Header("Tower Panel (Stats/Evolve)")]
    [SerializeField] private GameObject statsSection;
    [SerializeField] private TextMeshProUGUI damageValueText;
    [SerializeField] private TextMeshProUGUI attackSpeedValueText;
    [SerializeField] private TextMeshProUGUI rangeValueText;
    [SerializeField] private Button evolveButton;
    [SerializeField] private TextMeshProUGUI evolveButtonText;

    [Header("Background Sync")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform backgroundBorder;

    // Extra height reserved below ContentRoot's computed content height
    // when resizing Background - keeps a visible margin instead of the
    // background hugging the last row exactly.
    private const float BackgroundHeightMargin = 16f;

    // Matches StructureActionPanelUIBuilder.BorderThickness*2 (the amount
    // EnsureAccentBorder grows BackgroundBorder past Background on each
    // axis) - duplicated here because that constant lives in an Editor-only
    // assembly this runtime script can't reference.
    private const float BackgroundBorderExtraHeight = 4f;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private TowerSlot selectedTowerSlot;
    private FenceSlot selectedFenceSlot;
    private PlayerInteractor selectedInteractor;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(HandleUpgradeClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (repairButton != null)
        {
            repairButton.onClick.AddListener(HandleRepairClicked);
        }

        if (evolveButton != null)
        {
            evolveButton.onClick.AddListener(HandleEvolveClicked);
        }

        Close();
    }

    private void OnDestroy()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (repairButton != null)
        {
            repairButton.onClick.RemoveListener(HandleRepairClicked);
        }

        if (evolveButton != null)
        {
            evolveButton.onClick.RemoveListener(HandleEvolveClicked);
        }
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey) || !IsSelectionValid())
        {
            Close();
        }
    }

    public void Open(TowerSlot towerSlot, PlayerInteractor interactor)
    {
        if (towerSlot == null || interactor == null || GameManager.Instance == null || !GameManager.Instance.IsDay)
        {
            return;
        }

        selectedTowerSlot = towerSlot;
        selectedFenceSlot = null;
        selectedInteractor = interactor;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        Refresh();
    }

    public void Open(FenceSlot fenceSlot, PlayerInteractor interactor)
    {
        if (fenceSlot == null || interactor == null || GameManager.Instance == null || !GameManager.Instance.IsDay)
        {
            return;
        }

        selectedTowerSlot = null;
        selectedFenceSlot = fenceSlot;
        selectedInteractor = interactor;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        Refresh();
    }

    public void Close()
    {
        selectedTowerSlot = null;
        selectedFenceSlot = null;
        selectedInteractor = null;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void HandleUpgradeClicked()
    {
        if (!IsSelectionValid())
        {
            Close();
            return;
        }

        // Tower has no Upgrade logic yet (Phase 1-B); upgradeButton stays
        // non-interactable for Tower selections in Refresh(), so this is a
        // no-op for that case rather than something reachable via UI.
        bool upgraded = selectedFenceSlot != null && selectedFenceSlot.TryUpgradeFence(selectedInteractor);
        if (upgraded)
        {
            Refresh();
        }
    }

    private void HandleRepairClicked()
    {
        if (!IsSelectionValid())
        {
            Close();
            return;
        }

        bool repaired = selectedTowerSlot != null
            ? selectedTowerSlot.TryRepairTower(selectedInteractor)
            : selectedFenceSlot != null && selectedFenceSlot.TryRepairFence(selectedInteractor);

        Debug.Log($"[TEMP-DEBUG][HandleRepairClicked] repaired={repaired}, " +
            $"IsSelectionValid={IsSelectionValid()}");

        if (repaired)
        {
            Refresh();
        }
    }

    private void HandleEvolveClicked()
    {
        // Evolution selection screen (8-way grid) is a separate session's
        // scope. This button is only interactable at CurrentTierNumber >= 3,
        // which is currently unreachable (Tower T2/T3 data doesn't exist
        // yet), so this is a no-op in practice.
        Debug.Log("[TODO] Evolution selection screen not yet implemented");
    }

    private void Refresh()
    {
        if (selectedTowerSlot == null && selectedFenceSlot == null)
        {
            Close();
            return;
        }

        if (selectedFenceSlot != null)
        {
            RefreshFence();
            return;
        }

        bool isEmpty = !selectedTowerSlot.HasTower;
        ArrowTower tower = selectedTowerSlot.CurrentTower;
        bool hasValidTower = !isEmpty && tower != null;

        if (titleText != null)
        {
            titleText.text = "Arrow Tower";
        }

        if (subtitleText != null)
        {
            subtitleText.text = "DEFENSE TOWER";
        }

        if (hpSection != null)
        {
            hpSection.SetActive(hasValidTower);
        }

        if (hasValidTower)
        {
            if (hpFill != null)
            {
                hpFill.fillAmount = tower.MaxHp > 0f ? Mathf.Clamp01(tower.CurrentHp / tower.MaxHp) : 0f;
            }

            if (hpText != null)
            {
                hpText.text = $"{Mathf.RoundToInt(tower.CurrentHp)} / {Mathf.RoundToInt(tower.MaxHp)}";
            }
        }

        if (statsSection != null)
        {
            statsSection.SetActive(true);
        }

        // hasValidTower && tower.Data == null is reachable if a tower prefab
        // is placed without its TowerData assigned (see ArrowTower.OnValidate
        // warning) - treated the same as "no tower" for stat display rather
        // than crashing.
        TowerData towerData = hasValidTower ? tower.Data : null;
        if (towerData != null)
        {
            if (damageValueText != null)
            {
                damageValueText.text = towerData.Damage.ToString("0.#");
            }

            if (attackSpeedValueText != null)
            {
                float attackInterval = Mathf.Max(0.05f, towerData.AttackInterval);
                attackSpeedValueText.text = $"{1f / attackInterval:0.#}/s";
            }

            if (rangeValueText != null)
            {
                rangeValueText.text = $"{towerData.AttackRange:0.#}m";
            }
        }
        else
        {
            if (damageValueText != null) damageValueText.text = "—";
            if (attackSpeedValueText != null) attackSpeedValueText.text = "—";
            if (rangeValueText != null) rangeValueText.text = "—";
        }

        if (evolveButton != null)
        {
            evolveButton.gameObject.SetActive(true);
            bool canEvolve = selectedTowerSlot.CurrentTierNumber >= 3;
            evolveButton.interactable = canEvolve;
            if (evolveButtonText != null)
            {
                evolveButtonText.text = canEvolve ? "EVOLVE" : "EVOLVE (T3 필요)";
            }
        }

        if (dayActionsRoot != null)
        {
            dayActionsRoot.SetActive(true);
        }

        if (installButton != null)
        {
            installButton.gameObject.SetActive(isEmpty);
            installButton.interactable = isEmpty;
        }

        if (installButtonText != null)
        {
            installButtonText.text = "Install";
        }

        if (installCostText != null)
        {
            installCostText.gameObject.SetActive(isEmpty);
            installCostText.text = $"Wood: {selectedTowerSlot.WoodBuildCost}  Scrap: {selectedTowerSlot.ScrapBuildCost}";
        }

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(true);
            upgradeButton.interactable = false;
        }

        bool needsRepair = hasValidTower && tower.CurrentHp < tower.MaxHp - 0.01f;

        if (repairActionRoot != null)
        {
            repairActionRoot.SetActive(needsRepair);
        }

        if (repairButton != null)
        {
            repairButton.interactable = needsRepair;
        }

        SyncBackgroundSize();
    }

    // ContentRoot has a ContentSizeFitter (set by StructureActionPanelUIBuilder)
    // that tracks its stacked content's real height - Fence and Tower show a
    // different number of sections, so the "right" panel height can only be
    // known here, after a selection's content is actually laid out. Forces a
    // synchronous rebuild (deferred/per-frame layout isn't guaranteed to have
    // run yet within the same Refresh() call that just changed visibility/
    // text) then stretches Background/BackgroundBorder to match.
    private void SyncBackgroundSize()
    {
        if (contentRoot == null || background == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        float targetHeight = contentRoot.rect.height + BackgroundHeightMargin;

        Vector2 backgroundSize = background.sizeDelta;
        backgroundSize.y = targetHeight;
        background.sizeDelta = backgroundSize;

        if (backgroundBorder != null)
        {
            Vector2 borderSize = backgroundBorder.sizeDelta;
            borderSize.y = targetHeight + BackgroundBorderExtraHeight;
            backgroundBorder.sizeDelta = borderSize;
        }

        // ContentRoot stretch-anchors to panelRoot (not to Background), so its real
        // rect height is panelRoot.height + its own sizeDelta.y offset. Without also
        // growing panelRoot here, ContentRoot's actual content overflows past
        // Background/BackgroundBorder's box (confirmed: ~10-15px overflow on the
        // fully-expanded Tower evolve state) and CloseButton - anchored to
        // panelRoot's corner - ends up misaligned relative to the visible content.
        RectTransform panelRootRect = panelRoot != null ? panelRoot.GetComponent<RectTransform>() : null;
        if (panelRootRect != null)
        {
            Vector2 rootSize = panelRootRect.sizeDelta;
            rootSize.y = targetHeight;
            panelRootRect.sizeDelta = rootSize;

            // Growing panelRoot invalidates the ContentSizeFitter offset just baked
            // into ContentRoot.sizeDelta (it was computed against the old, smaller
            // parent height) - rebuild once more so it re-settles against the new
            // parent height before this frame renders, instead of a one-frame
            // overflow flash until Unity's normal deferred layout pass catches up.
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
    }

    private void RefreshFence()
    {
        FenceSegment fence = selectedFenceSlot.CurrentFence;
        ResourceManager resourceManager = selectedInteractor != null ? selectedInteractor.ResourceManager : null;
        bool isEmpty = !selectedFenceSlot.HasFence;
        bool hasValidFence = !isEmpty && fence != null;
        bool needsRepair = hasValidFence && fence.CanRepair();
        bool isMaxTier = hasValidFence && fence.NextTierData == null;
        bool canAffordUpgrade = resourceManager != null
            && resourceManager.HasResource(ResourceType.Wood, selectedFenceSlot.UpgradeWoodCost)
            && resourceManager.HasResource(ResourceType.Scrap, selectedFenceSlot.UpgradeScrapCost);

        if (titleText != null) titleText.text = "Fence";
        if (subtitleText != null) subtitleText.text = "DEFENSE BARRIER";
        if (tierBadgeText != null) tierBadgeText.text = isEmpty ? "—" : $"T{selectedFenceSlot.CurrentTierNumber}";

        if (statsSection != null) statsSection.SetActive(false);
        if (evolveButton != null) evolveButton.gameObject.SetActive(false);

        if (hpSection != null) hpSection.SetActive(hasValidFence);

        Debug.Log($"[TEMP-DEBUG][RefreshFence] called. hasValidFence={hasValidFence}, " +
            $"fence.CurrentHp={fence?.CurrentHp}, fence.MaxHp={fence?.MaxHp}, " +
            $"hpFill.fillAmount(before)={hpFill?.fillAmount}, hpText.text(before)={hpText?.text}");

        if (hasValidFence)
        {
            if (hpFill != null) hpFill.fillAmount = fence.MaxHp > 0f ? Mathf.Clamp01(fence.CurrentHp / fence.MaxHp) : 0f;
            if (hpText != null) hpText.text = $"{Mathf.RoundToInt(fence.CurrentHp)} / {Mathf.RoundToInt(fence.MaxHp)}";
        }

        Debug.Log($"[TEMP-DEBUG][RefreshFence] after set. " +
            $"hpFill.fillAmount(after)={hpFill?.fillAmount}, hpText.text(after)={hpText?.text}, " +
            $"hpFill.gameObject.activeInHierarchy={hpFill?.gameObject.activeInHierarchy}, " +
            $"hpFill.canvasRenderer null={hpFill?.canvas == null}");

        if (dayActionsRoot != null) dayActionsRoot.SetActive(true);

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(true);
            upgradeButton.interactable = !isMaxTier && canAffordUpgrade;
        }
        if (upgradeButtonText != null) upgradeButtonText.text = isEmpty ? "INSTALL" : "UPGRADE";
        if (upgradeWoodCountText != null) upgradeWoodCountText.text = selectedFenceSlot.UpgradeWoodCost.ToString();
        if (upgradeScrapCountText != null) upgradeScrapCountText.text = selectedFenceSlot.UpgradeScrapCost.ToString();

        if (repairActionRoot != null) repairActionRoot.SetActive(needsRepair);
        if (repairButton != null)
        {
            repairButton.interactable = needsRepair && fence.HasEnoughResources(resourceManager);
        }
        if (repairAmountText != null && hasValidFence)
        {
            repairAmountText.text = $"+{Mathf.RoundToInt(fence.GetRepairAmount())} HP";
        }
        if (repairWoodCountText != null) repairWoodCountText.text = selectedFenceSlot.WoodRepairCost.ToString();
        if (repairScrapCountText != null) repairScrapCountText.text = selectedFenceSlot.ScrapRepairCost.ToString();

        SyncBackgroundSize();
    }

    private bool IsSelectionValid()
    {
        if ((selectedTowerSlot == null && selectedFenceSlot == null)
            || selectedInteractor == null || !selectedInteractor.isActiveAndEnabled)
        {
            return false;
        }

        if (GameManager.Instance == null || !GameManager.Instance.IsDay)
        {
            return false;
        }

        if (selectedTowerSlot != null)
        {
            return selectedTowerSlot.CanInteract(selectedInteractor)
                && selectedInteractor.IsInteractableInRange(selectedTowerSlot);
        }

        return selectedFenceSlot.CanInteract(selectedInteractor)
            && selectedInteractor.IsInteractableInRange(selectedFenceSlot);
    }
}
