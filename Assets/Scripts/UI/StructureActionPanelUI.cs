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

        if (selectedTowerSlot.TryBuildTower(selectedInteractor))
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

        if (selectedTowerSlot.TryRepairTower(selectedInteractor))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (selectedTowerSlot == null)
        {
            Close();
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

    private bool IsSelectionValid()
    {
        if (selectedTowerSlot == null || selectedInteractor == null || !selectedInteractor.isActiveAndEnabled)
        {
            return false;
        }

        if (GameManager.Instance == null || !GameManager.Instance.IsDay)
        {
            return false;
        }

        return selectedTowerSlot.CanInteract(selectedInteractor)
            && selectedInteractor.IsInteractableInRange(selectedTowerSlot);
    }
}