using System.Collections;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    const string IsGatheringParameter = "IsGathering";
    const string GatherTypeParameter = "GatherType";
    const int GatherTypeNone = 0;
    const int GatherTypePickup = 1;
    const int GatherTypeLogging = 2;

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
    [SerializeField, Tooltip("Optional movement script to lock while gathering.")]
    PlayerMovement playerMovement;

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
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
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
        BeginGatherState(resourceNode);
        CreateGatherBar(resourceNode);

        SimpleShake shake = resourceNode.GetComponentInChildren<SimpleShake>();
        float shakeTimer = 0f;

        float elapsed = 0f;
        while (elapsed < gatherDurationSeconds)
        {
            if (resourceNode == null || !resourceNode.CanInteract(this))
            {
                EndGatherState();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = gatherDurationSeconds > 0f ? elapsed / gatherDurationSeconds : 1f;
            if (activeGatherBar != null)
                activeGatherBar.SetProgress(progress);

            shakeTimer -= Time.deltaTime;
            if (shake != null && shakeTimer <= 0f)
            {
                shake.Shake(0.1f, 0.05f);
                shakeTimer = 0.15f;
            }


            yield return null;
        }
        

        if (resourceNode != null && resourceNode.CanInteract(this))
            resourceNode.Interact(this);

        EndGatherState();
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

    void BeginGatherState(ResourceNode resourceNode)
    {
        if (playerMovement != null)
            playerMovement.SetMovementLocked(true);

        if (playerAnimator == null)
            return;

        int gatherType = GatherTypeNone;
        if (resourceNode != null)
        {
            gatherType = resourceNode.GatherAnimationType == GatherAnimationType.Logging
                ? GatherTypeLogging
                : GatherTypePickup;
        }

        playerAnimator.SetInteger(GatherTypeParameter, gatherType);
        playerAnimator.SetBool(IsGatheringParameter, true);
    }

    void EndGatherState()
    {
        if (playerMovement != null)
            playerMovement.SetMovementLocked(false);

        if (playerAnimator != null)
        {
            playerAnimator.SetBool(IsGatheringParameter, false);
            playerAnimator.SetInteger(GatherTypeParameter, GatherTypeNone);
        }

        CleanupGatherBar();
        isGathering = false;
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
