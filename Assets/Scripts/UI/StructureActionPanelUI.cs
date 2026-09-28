using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StructureActionPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject hpSection;
    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private GameObject dayActionsRoot;
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
    [SerializeField] private KeyCode upgradeKey = KeyCode.E;
    [SerializeField] private KeyCode repairKey = KeyCode.R;

    // The E press that opened this panel (PlayerInteractor.Interact) is still
    // GetKeyDown this frame - ignore hotkeys on the open frame so it doesn't
    // also count as an instant Upgrade/Install.
    private int openedFrame = -1;

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
            return;
        }

        if (Time.frameCount == openedFrame)
        {
            return;
        }

        // Same gates as clicking: only fire when the button itself is shown and interactable.
        if (Input.GetKeyDown(upgradeKey) && IsButtonUsable(upgradeButton))
        {
            HandleUpgradeClicked();
        }
        else if (Input.GetKeyDown(repairKey) && IsButtonUsable(repairButton))
        {
            HandleRepairClicked();
        }
    }

    private static bool IsButtonUsable(Button button)
    {
        return button != null && button.interactable && button.gameObject.activeInHierarchy;
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
        openedFrame = Time.frameCount;

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
        openedFrame = Time.frameCount;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        Refresh();
    }

    // Two separate causes pin a button's Color Tint on its Highlighted/
    // white look after it should have returned to Normal:
    // 1) A button that becomes interactable directly under an already-
    //    stationary cursor (this panel opening on top of it, no mouse
    //    movement involved) fires OnPointerEnter once panelRoot activates,
    //    but nothing fires OnPointerExit until the cursor actually moves
    //    off and back on. Forcing an explicit exit here re-syncs it to its
    //    real current state; if the cursor genuinely is still over it, the
    //    next frame's raycast re-enters normally, so live hover feedback
    //    keeps working afterward.
    // 2) Clicking a button (Repair/Upgrade) makes the EventSystem select
    //    it (mouse-down selection, independent of hover), so Selectable's
    //    state stays "Selected" - which uses the same near-white tint as
    //    Highlighted - even after the cursor leaves and returns. Moving
    //    the mouse away only fixes (1); it never clears the selection, so
    //    without also clearing it here the button stays stuck until the
    //    player clicks something else entirely. Clearing the selected
    //    object after every click-triggered Refresh() (not just on Open())
    //    is what actually resolves that case.
    private void ResetButtonHoverState()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        if (EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        if (upgradeButton != null) upgradeButton.OnPointerExit(pointerEventData);
        if (repairButton != null) repairButton.OnPointerExit(pointerEventData);
        if (evolveButton != null) evolveButton.OnPointerExit(pointerEventData);
        if (closeButton != null) closeButton.OnPointerExit(pointerEventData);
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

        bool upgraded = selectedTowerSlot != null
            ? selectedTowerSlot.TryUpgradeTower(selectedInteractor)
            : selectedFenceSlot != null && selectedFenceSlot.TryUpgradeFence(selectedInteractor);
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

        ResourceManager resourceManager = selectedInteractor != null ? selectedInteractor.ResourceManager : null;
        int upgradeWood = selectedTowerSlot.UpgradeWoodCost;
        int upgradeScrap = selectedTowerSlot.UpgradeScrapCost;
        bool canAffordUpgrade = resourceManager != null
            && resourceManager.HasResource(ResourceType.Wood, upgradeWood)
            && resourceManager.HasResource(ResourceType.Scrap, upgradeScrap);

        if (tierBadgeText != null) tierBadgeText.text = isEmpty ? "—" : $"T{selectedTowerSlot.CurrentTierNumber}";

        // Installed towers stay non-interactable here: T1->T2+ goes through
        // TowerUpgradeConfirmPanel (approved, not built), and no T2 data exists
        // yet anyway - TowerSlot.TryUpgradeTower only handles the Empty case.
        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(true);
            upgradeButton.interactable = isEmpty && canAffordUpgrade;
        }
        if (upgradeButtonText != null) upgradeButtonText.text = isEmpty ? "INSTALL" : "UPGRADE";
        if (upgradeWoodCountText != null) upgradeWoodCountText.text = upgradeWood.ToString();
        if (upgradeScrapCountText != null) upgradeScrapCountText.text = upgradeScrap.ToString();

        bool needsRepair = hasValidTower && tower.CurrentHp < tower.MaxHp - 0.01f;
        int repairWood = selectedTowerSlot.WoodRepairCost;
        int repairScrap = selectedTowerSlot.ScrapRepairCost;
        bool canAffordRepair = resourceManager != null
            && resourceManager.HasResource(ResourceType.Wood, repairWood)
            && resourceManager.HasResource(ResourceType.Scrap, repairScrap);

        if (repairActionRoot != null)
        {
            repairActionRoot.SetActive(needsRepair);
        }

        if (repairButton != null)
        {
            repairButton.interactable = needsRepair && canAffordRepair;
        }
        if (repairAmountText != null) repairAmountText.text = $"+{Mathf.RoundToInt(selectedTowerSlot.RepairAmountPerStep)} HP";
        if (repairWoodCountText != null) repairWoodCountText.text = repairWood.ToString();
        if (repairScrapCountText != null) repairScrapCountText.text = repairScrap.ToString();

        SyncBackgroundSize();
        ResetButtonHoverState();
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
        ResetButtonHoverState();
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
