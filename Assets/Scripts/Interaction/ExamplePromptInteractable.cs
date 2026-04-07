using UnityEngine;

public class ExamplePromptInteractable : MonoBehaviour, IInteractable
{
    [Header("Legacy Example (Non-Driving)")]
    [SerializeField] private string promptMessage = "E - Gather";

    private void Awake()
    {
        if (FindObjectOfType<PlayerInteractor>() != null)
        {
            Debug.LogWarning("[ExamplePromptInteractable] Unified PlayerInteractor prompt system detected. Keep this script non-driving to avoid duplicate prompts.");
        }
    }

    public Transform GetUIAnchor()
    {
        return transform;
    }

    public Vector3 GetInteractPosition()
    {
        return transform.position;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return true;
    }

    public InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        return InteractablePromptData.CreateSimple(promptMessage);
    }

    public void Interact(PlayerInteractor interactor)
    {
    }
}
