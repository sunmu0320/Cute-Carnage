using UnityEngine;

public class ResourceNode : BaseInteractable
{
    [Header("Resource Node")]
    [SerializeField, Tooltip("Type of resource this node provides.")]
    ResourceType resourceType = ResourceType.Wood;

    [SerializeField, Tooltip("Amount granted when gathered.")]
    int amount = 1;

    public override string GetInteractionPrompt()
    {
        return $"Press E to gather {resourceType}";
    }

    protected override void OnInteract(PlayerInteractor interactor)
    {
        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        if (resourceManager == null)
        {
            Debug.LogWarning($"[{nameof(ResourceNode)}] No {nameof(ResourceManager)} found for {gameObject.name}.");
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
