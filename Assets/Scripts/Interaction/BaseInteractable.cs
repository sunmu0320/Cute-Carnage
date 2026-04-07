using UnityEngine;

public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField, Tooltip("Display name used in interaction prompts.")]
    protected string interactName = "Object";

    [SerializeField, Tooltip("If false, this interactable is temporarily disabled.")]
    protected bool canUse = true;

    [SerializeField, Tooltip("If true, this interactable can only be used once.")]
    protected bool oneTimeUse = false;

    [SerializeField, Tooltip("Optional world-space anchor for interaction UI.")]
    Transform uiAnchor;

    bool hasBeenUsed;

    public virtual Transform GetUIAnchor()
    {
        return uiAnchor != null ? uiAnchor : transform;
    }

    public virtual Vector3 GetInteractPosition()
    {
        return transform.position;
    }

    public virtual InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        return InteractablePromptData.CreateSimple($"Press E to interact with {interactName}");
    }

    public virtual bool CanInteract(PlayerInteractor interactor)
    {
        if (!canUse)
            return false;

        if (oneTimeUse && hasBeenUsed)
            return false;

        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        OnInteract(interactor);

        if (oneTimeUse)
            hasBeenUsed = true;
    }

    protected abstract void OnInteract(PlayerInteractor interactor);
}
