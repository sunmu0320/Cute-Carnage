using UnityEngine;

public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    protected const string BaseUIAnchorSourceLabel = "base UI Anchor";
    protected const string FallbackTransformSourceLabel = "fallback transform";

    [Header("Interaction")]
    [SerializeField, Tooltip("Display name used in interaction prompts.")]
    protected string interactName = "Object";

    [SerializeField, Tooltip("If false, this interactable is temporarily disabled.")]
    protected bool canUse = true;

    [SerializeField, Tooltip("If true, this interactable can only be used once.")]
    protected bool oneTimeUse = false;

    [SerializeField, Tooltip("Optional world-space anchor for interaction UI.")]
    Transform uiAnchor;

    [Header("Debug")]
    [SerializeField, Tooltip("Logs how this interactable resolves its prompt UI anchor.")]
    bool logUIAnchorResolution;

    bool hasBeenUsed;

    public virtual Transform GetUIAnchor()
    {
        Transform anchor = ResolveBaseUIAnchor(out string sourceLabel);
        LogUIAnchorResolution(sourceLabel, anchor);
        return anchor;
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

    protected Transform ResolveBaseUIAnchor(out string sourceLabel)
    {
        if (uiAnchor != null)
        {
            sourceLabel = BaseUIAnchorSourceLabel;
            return uiAnchor;
        }

        sourceLabel = FallbackTransformSourceLabel;
        return transform;
    }

    protected bool ShouldLogUIAnchorResolution()
    {
        return logUIAnchorResolution;
    }

    protected void LogUIAnchorResolution(string sourceLabel, Transform anchor)
    {
        if (!logUIAnchorResolution)
            return;

        string targetName = gameObject != null ? gameObject.name : "<null>";
        string anchorName = anchor != null ? anchor.name : "<null>";
        Debug.Log(
            $"[{GetType().Name}] Prompt UI anchor resolved. Target='{targetName}', Anchor='{anchorName}', Source={sourceLabel}.",
            this);
    }
}
