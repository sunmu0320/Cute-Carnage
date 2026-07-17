using UnityEngine;

public class ResourceNode : BaseInteractable
{
    private const string DefaultPromptAnchorName = "UIAnchor";
    private const string ExplicitPromptUIAnchorSourceLabel = "Prompt UI Anchor";
    private const string ChildPromptUIAnchorSourceLabel = "child UIAnchor";

    [Header("Resource Node")]
    [SerializeField, Tooltip("Type of resource this node provides.")]
    ResourceType resourceType = ResourceType.Wood;

    [SerializeField, Tooltip("Amount granted when gathered.")]
    int amount = 1;

    [SerializeField, Tooltip("Animation style this node should use for gathering.")]
    GatherAnimationType gatherAnimationType = GatherAnimationType.Pickup;

    [SerializeField, Tooltip("Optional anchor where the world gather bar appears.")]
    Transform gatherBarAnchor;

    [SerializeField, Tooltip("Optional anchor where the screen-space [E] prompt follows. Auto-finds child named UIAnchor when empty.")]
    Transform promptUIAnchor;

    [SerializeField, Tooltip("Max distance the player can be to gather this node.")]
    float gatherDistance = 1.0f;

    public GatherAnimationType GatherAnimationType => gatherAnimationType;
    public Transform GatherBarAnchor => gatherBarAnchor;
    public float GatherDistance => gatherDistance;

    private void Awake()
    {
        AutoAssignAnchorsIfMissing();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignAnchorsIfMissing();
    }
#endif

    public override Transform GetUIAnchor()
    {
        if (promptUIAnchor != null)
        {
            LogUIAnchorResolution(ExplicitPromptUIAnchorSourceLabel, promptUIAnchor);
            return promptUIAnchor;
        }

        if (TryFindPromptUIAnchor(out Transform childAnchor))
        {
            promptUIAnchor = childAnchor;
            LogUIAnchorResolution(ChildPromptUIAnchorSourceLabel, promptUIAnchor);
            return promptUIAnchor;
        }

        Transform fallbackAnchor = ResolveBaseUIAnchor(out string sourceLabel);
        LogUIAnchorResolution(sourceLabel, fallbackAnchor);
        return fallbackAnchor;
    }

    public override InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        return InteractablePromptData.CreateSimple("Press E to Gather");
    }

    private void AutoAssignAnchorsIfMissing()
    {
        if (promptUIAnchor == null)
            TryFindPromptUIAnchor(out promptUIAnchor);
    }

    private bool TryFindPromptUIAnchor(out Transform anchor)
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform candidate = childTransforms[i];
            if (candidate == null || candidate == transform)
                continue;

            if (candidate.name == DefaultPromptAnchorName)
            {
                anchor = candidate;
                return true;
            }
        }

        anchor = null;
        return false;
    }

    protected override void OnInteract(PlayerInteractor interactor)
    {
        ResourceManager resourceManager = interactor != null ? interactor.ResourceManager : null;
        if (resourceManager == null)
        {
            Debug.LogWarning($"[{nameof(ResourceNode)}] No {nameof(ResourceManager)} found on {nameof(PlayerInteractor)} for {gameObject.name}.");
            return;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[{nameof(ResourceNode)}] Invalid gather amount ({amount}) on {gameObject.name}.");
            return;
        }

        resourceManager.AddResource(resourceType, amount);
        int newTotal = resourceManager.GetAmount(resourceType);

        Debug.Log($"[ResourceNode] {gameObject.name} gathered {amount} {resourceType}. New total: {newTotal}");
    }
}
