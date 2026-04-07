using UnityEngine;

public class ResourceNode : BaseInteractable
{
    [Header("Resource Node")]
    [SerializeField, Tooltip("Type of resource this node provides.")]
    ResourceType resourceType = ResourceType.Wood;

    [SerializeField, Tooltip("Amount granted when gathered.")]
    int amount = 1;

    [SerializeField, Tooltip("Animation style this node should use for gathering.")]
    GatherAnimationType gatherAnimationType = GatherAnimationType.Pickup;

    [SerializeField, Tooltip("Optional anchor where the world gather bar appears.")]
    Transform gatherBarAnchor;

    public GatherAnimationType GatherAnimationType => gatherAnimationType;
    public Transform GatherBarAnchor => gatherBarAnchor;

    public override InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor)
    {
        return InteractablePromptData.CreateSimple("Press E to Gather");
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
