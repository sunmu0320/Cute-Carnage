using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NightRepairPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject repairActionRoot;
    [SerializeField] private TextMeshProUGUI woodCostText;
    [SerializeField] private TextMeshProUGUI scrapCostText;
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
        if (repairButton != null)
        {
            repairButton.onClick.AddListener(HandleRepairClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        Close();
    }

    private void OnDestroy()
    {
        if (repairButton != null)
        {
            repairButton.onClick.RemoveListener(HandleRepairClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
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

        Refresh();
    }

    public void Open(TowerSlot towerSlot, PlayerInteractor interactor)
    {
        if (!CanOpen(towerSlot, interactor))
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
        if (!CanOpen(fenceSlot, interactor))
        {
            return;
        }

        selectedTowerSlot = null;
        selectedFenceSlot = fenceSlot;
        selectedInteractor = interactor;
        if (panelRoot != null) panelRoot.SetActive(true);
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

    private void HandleRepairClicked()
    {
        if (!IsSelectionValid())
        {
            Close();
            return;
        }

        ResourceManager resourceManager = selectedInteractor.ResourceManager;
        if (!CanAffordRepair(resourceManager))
        {
            Refresh();
            return;
        }

        bool repaired = selectedTowerSlot != null
            ? selectedTowerSlot.TryRepairTower(selectedInteractor)
            : selectedFenceSlot != null && selectedFenceSlot.TryRepairFence(selectedInteractor);
        if (repaired)
        {
            bool isFull = selectedTowerSlot != null
                ? selectedTowerSlot.CurrentTower == null
                    || selectedTowerSlot.CurrentTower.CurrentHp >= selectedTowerSlot.CurrentTower.MaxHp - 0.01f
                : selectedFenceSlot.CurrentFence == null || !selectedFenceSlot.CurrentFence.CanRepair();
            if (isFull)
            {
                Close();
                return;
            }

            Refresh();
        }
    }

    private void Refresh()
    {
        if (!IsSelectionValid())
        {
            Close();
            return;
        }

        if (repairActionRoot != null)
        {
            repairActionRoot.SetActive(true);
        }

        if (woodCostText != null)
        {
            woodCostText.text = (selectedTowerSlot != null
                ? selectedTowerSlot.WoodRepairCost
                : selectedFenceSlot.WoodRepairCost).ToString();
        }

        if (scrapCostText != null)
        {
            scrapCostText.text = (selectedTowerSlot != null
                ? selectedTowerSlot.ScrapRepairCost
                : selectedFenceSlot.ScrapRepairCost).ToString();
        }

        if (repairButton != null)
        {
            repairButton.interactable = CanAffordRepair(selectedInteractor.ResourceManager);
        }
    }

    private bool CanOpen(TowerSlot towerSlot, PlayerInteractor interactor)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsDay
            || towerSlot == null || interactor == null || !interactor.isActiveAndEnabled)
        {
            return false;
        }

        ArrowTower tower = towerSlot.CurrentTower;
        return towerSlot.HasTower
            && tower != null
            && tower.CurrentHp < tower.MaxHp - 0.01f
            && interactor.IsInteractableInRange(towerSlot);
    }

    private bool CanOpen(FenceSlot fenceSlot, PlayerInteractor interactor)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsDay
            || fenceSlot == null || interactor == null || !interactor.isActiveAndEnabled)
        {
            return false;
        }

        FenceSegment fence = fenceSlot.CurrentFence;
        return fenceSlot.HasFence
            && fence != null
            && fence.CanRepair()
            && interactor.IsInteractableInRange(fenceSlot);
    }

    private bool IsSelectionValid()
    {
        if (selectedTowerSlot != null)
        {
            return CanOpen(selectedTowerSlot, selectedInteractor)
                && selectedTowerSlot.CanInteract(selectedInteractor);
        }

        return CanOpen(selectedFenceSlot, selectedInteractor)
            && selectedFenceSlot.CanInteract(selectedInteractor);
    }

    private bool CanAffordRepair(ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return false;
        }

        if (selectedTowerSlot != null)
        {
            return resourceManager.HasResource(ResourceType.Wood, selectedTowerSlot.WoodRepairCost)
                && resourceManager.HasResource(ResourceType.Scrap, selectedTowerSlot.ScrapRepairCost);
        }

        FenceSegment fence = selectedFenceSlot != null ? selectedFenceSlot.CurrentFence : null;
        return fence != null && fence.HasEnoughResources(resourceManager);
    }
}
