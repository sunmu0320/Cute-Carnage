using UnityEngine;

public class TowerSlot : MonoBehaviour, IInteractable
{
    [SerializeField]
    private bool hasTower;

    [SerializeField]
    private GameObject towerPrefab;

    [SerializeField]
    private Transform spawnPoint;

    private GameObject currentTower;

    [SerializeField]
    private GameObject slotVisualRoot;

    [SerializeField]
    private Transform slotMarker;

    private void Awake()
    {
        RefreshSlotVisualState();
    }

    private void OnValidate()
    {
        RefreshSlotVisualState();
    }

    public Transform GetUIAnchor()
    {
        return spawnPoint != null ? spawnPoint : transform;
    }

    public Vector3 GetInteractPosition()
    {
        return GetUIAnchor().position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !hasTower;
    }

    public InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        if (!hasTower)
        {
            return InteractablePromptData.CreateSimple("Press E to build Tower");
        }

        return InteractablePromptData.CreateSimple("Tower Installed");
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (hasTower)
        {
            return;
        }

        if (towerPrefab == null)
        {
            Debug.LogWarning("[TowerSlot] towerPrefab is not assigned.", this);
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        currentTower = Instantiate(towerPrefab, origin.position, origin.rotation);
        hasTower = true;
        RefreshSlotVisualState();
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
}
