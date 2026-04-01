using System.Collections;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Tooltip("How far the player can reach to interact.")]
    float interactionRadius = 2f;

    [SerializeField, Tooltip("Only colliders on these layers are checked for interaction.")]
    LayerMask interactableLayerMask = ~0;

    [SerializeField, Tooltip("Press this key to interact with the nearest valid target.")]
    KeyCode interactionKey = KeyCode.E;

    [Header("Gather Animation")]
    [SerializeField, Tooltip("Animator used to trigger gather animations. Auto-found in children if left empty.")]
    Animator playerAnimator;
    [SerializeField, Tooltip("Animator trigger parameter for pickup-style gathering.")]
    string pickupTriggerParameter = "GatherPickup";
    [SerializeField, Tooltip("Animator trigger parameter for logging-style gathering.")]
    string loggingTriggerParameter = "GatherLogging";

    [Header("Gather Timing")]
    [SerializeField, Tooltip("Time in seconds required to finish gathering a resource node.")]
    float gatherDurationSeconds = 3f;
    [SerializeField, Tooltip("World-space gather bar prefab shown above a resource while gathering.")]
    WorldGatherBar worldGatherBarPrefab;

    IInteractable currentInteractable;
    bool isGathering;
    WorldGatherBar activeGatherBar;

    public IInteractable CurrentInteractable => currentInteractable;

    void Awake()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Re-scan every frame so we always know the nearest valid target.
        currentInteractable = FindNearestValidInteractable();

        if (currentInteractable == null)
            return;

        if (Input.GetKeyDown(interactionKey))
        {
            ResourceNode resourceNode = currentInteractable as ResourceNode;
            if (resourceNode != null)
            {
                if (!isGathering)
                    StartCoroutine(GatherResourceOverTime(resourceNode));
            }
            else
            {
                currentInteractable.Interact(this);
            }
        }
    }

    IEnumerator GatherResourceOverTime(ResourceNode resourceNode)
    {
        isGathering = true;
        TriggerGatherAnimationIfNeeded(resourceNode);
        CreateGatherBar(resourceNode);

        float elapsed = 0f;
        while (elapsed < gatherDurationSeconds)
        {
            if (resourceNode == null || !resourceNode.CanInteract(this))
            {
                CleanupGatherBar();
                isGathering = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = gatherDurationSeconds > 0f ? elapsed / gatherDurationSeconds : 1f;
            if (activeGatherBar != null)
                activeGatherBar.SetProgress(progress);

            yield return null;
        }

        if (resourceNode != null && resourceNode.CanInteract(this))
            resourceNode.Interact(this);

        CleanupGatherBar();
        isGathering = false;
    }

    void CreateGatherBar(ResourceNode resourceNode)
    {
        CleanupGatherBar();

        if (worldGatherBarPrefab == null || resourceNode == null)
            return;

        Transform anchor = resourceNode.GatherBarAnchor != null ? resourceNode.GatherBarAnchor : resourceNode.transform;
        activeGatherBar = Instantiate(worldGatherBarPrefab, anchor.position, anchor.rotation, anchor);
        if (activeGatherBar == null)
            return;

        activeGatherBar.SetProgress(0f);
        activeGatherBar.Show();
    }

    void CleanupGatherBar()
    {
        if (activeGatherBar == null)
            return;

        Destroy(activeGatherBar.gameObject);
        activeGatherBar = null;
    }

    void TriggerGatherAnimationIfNeeded(IInteractable interactable)
    {
        if (playerAnimator == null)
            return;

        ResourceNode resourceNode = interactable as ResourceNode;
        if (resourceNode == null)
            return;

        string triggerParameter = resourceNode.GatherAnimationType == GatherAnimationType.Logging
            ? loggingTriggerParameter
            : pickupTriggerParameter;

        if (string.IsNullOrWhiteSpace(triggerParameter))
            return;

        playerAnimator.SetTrigger(triggerParameter);
    }

    IInteractable FindNearestValidInteractable()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(
            transform.position,
            interactionRadius,
            interactableLayerMask);

        IInteractable nearestInteractable = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < nearbyColliders.Length; i++)
        {
            Collider candidateCollider = nearbyColliders[i];
            IInteractable candidateInteractable = ResolveInteractable(candidateCollider);

            if (candidateInteractable == null)
                continue;

            if (!candidateInteractable.CanInteract(this))
                continue;

            float distanceSqr = (candidateCollider.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestInteractable = candidateInteractable;
            }
        }

        return nearestInteractable;
    }

    IInteractable ResolveInteractable(Collider candidateCollider)
    {
        // First, try the same object as the collider.
        IInteractable interactable = candidateCollider.GetComponent<IInteractable>();
        if (interactable != null)
            return interactable;

        // If the collider is on a child, try parents as a fallback.
        return candidateCollider.GetComponentInParent<IInteractable>();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
