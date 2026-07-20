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

        if (selectedTowerSlot.TryRepairTower(selectedInteractor))
        {
            ArrowTower tower = selectedTowerSlot.CurrentTower;
            if (tower == null || tower.CurrentHp >= tower.MaxHp - 0.01f)
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
            woodCostText.text = selectedTowerSlot.WoodRepairCost.ToString();
        }

        if (scrapCostText != null)
        {
            scrapCostText.text = selectedTowerSlot.ScrapRepairCost.ToString();
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

    private bool IsSelectionValid()
    {
        return CanOpen(selectedTowerSlot, selectedInteractor)
            && selectedTowerSlot.CanInteract(selectedInteractor);
    }

    private bool CanAffordRepair(ResourceManager resourceManager)
    {
        return selectedTowerSlot != null
            && resourceManager != null
            && resourceManager.HasResource(ResourceType.Wood, selectedTowerSlot.WoodRepairCost)
            && resourceManager.HasResource(ResourceType.Scrap, selectedTowerSlot.ScrapRepairCost);
    }
}
