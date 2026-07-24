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
    [SerializeField] private GameObject repairActionRoot;
    [SerializeField] private Button repairButton;
    [SerializeField] private Button closeButton;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private TowerSlot selectedTowerSlot;
    private FenceSlot selectedFenceSlot;
    private PlayerInteractor selectedInteractor;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        if (installButton != null)
        {
            installButton.onClick.AddListener(HandleInstallClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (repairButton != null)
        {
            repairButton.onClick.AddListener(HandleRepairClicked);
        }

        Close();
    }

    private void OnDestroy()
    {
        if (installButton != null)
        {
            installButton.onClick.RemoveListener(HandleInstallClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (repairButton != null)
        {
            repairButton.onClick.RemoveListener(HandleRepairClicked);
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

    private void HandleInstallClicked()
    {
        if (!IsSelectionValid())
        {
            Close();
            return;
        }

        bool installed = selectedTowerSlot != null
            ? selectedTowerSlot.TryBuildTower(selectedInteractor)
            : selectedFenceSlot != null && selectedFenceSlot.TryInstallFence(selectedInteractor);
        if (installed)
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
        if (repaired)
        {
            Refresh();
        }
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
    }

    private void RefreshFence()
    {
        FenceSegment fence = selectedFenceSlot.CurrentFence;
        bool isEmpty = !selectedFenceSlot.HasFence;
        bool hasValidFence = !isEmpty && fence != null;
        bool needsRepair = hasValidFence && fence.CanRepair();

        if (titleText != null) titleText.text = "Fence";
        if (hpSection != null) hpSection.SetActive(hasValidFence);
        if (hasValidFence)
        {
            if (hpFill != null) hpFill.fillAmount = fence.MaxHp > 0f ? Mathf.Clamp01(fence.CurrentHp / fence.MaxHp) : 0f;
            if (hpText != null) hpText.text = $"{Mathf.RoundToInt(fence.CurrentHp)} / {Mathf.RoundToInt(fence.MaxHp)}";
        }
        if (dayActionsRoot != null) dayActionsRoot.SetActive(true);
        if (installButton != null)
        {
            installButton.gameObject.SetActive(isEmpty);
            installButton.interactable = isEmpty && selectedFenceSlot.CanInstallFence();
        }
        if (installButtonText != null) installButtonText.text = "Install Fence";
        if (installCostText != null)
        {
            installCostText.gameObject.SetActive(isEmpty || needsRepair);
            installCostText.text = isEmpty
                ? $"Wood: {selectedFenceSlot.WoodBuildCost}  Scrap: {selectedFenceSlot.ScrapBuildCost}"
                : $"Wood: {selectedFenceSlot.WoodRepairCost}  Scrap: {selectedFenceSlot.ScrapRepairCost}";
        }
        if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
        if (repairActionRoot != null) repairActionRoot.SetActive(needsRepair);
        if (repairButton != null)
        {
            repairButton.interactable = needsRepair
                && fence.HasEnoughResources(selectedInteractor != null ? selectedInteractor.ResourceManager : null);
        }
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
