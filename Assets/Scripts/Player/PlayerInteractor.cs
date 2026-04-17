using System.Collections;
using UnityEngine;

public enum PlayerActionState
{
    Normal,
    Repairing
}

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

    [SerializeField, Tooltip("Shared resource inventory used by interactables (fences, nodes, towers, chests).")]
    ResourceManager resourceManager;

    [SerializeField, Tooltip("Optional world prompt UI that follows the current target.")]
    WorldPromptUI worldPromptUI;

    [SerializeField, Tooltip("Max colliders scanned each frame by NonAlloc overlap.")]
    int overlapBufferSize = 32;

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

    [Header("Debug")]
    [SerializeField, Tooltip("Log repair state transitions and blocked E presses to the Console.")]
    bool logRepairState = true;

    IInteractable currentInteractable;
    IRepairable currentRepairable;
    IRepairable activeRepairTarget;
    PlayerActionState currentState = PlayerActionState.Normal;
    bool isGathering;
    WorldGatherBar activeGatherBar;
    Collider[] overlapBuffer;

    public IInteractable CurrentInteractable => currentInteractable;
    public ResourceManager ResourceManager => resourceManager;
    public bool IsRepairing => currentState == PlayerActionState.Repairing;

    public PlayerActionState DebugCurrentState => currentState;
    public IRepairable DebugCurrentRepairable => currentRepairable;
    public IRepairable DebugActiveRepairTarget => activeRepairTarget;

    void Awake()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (resourceManager == null)
            resourceManager = FindObjectOfType<ResourceManager>();
        if (worldPromptUI == null)
            worldPromptUI = FindObjectOfType<WorldPromptUI>();

        if (overlapBufferSize < 4)
            overlapBufferSize = 4;
        overlapBuffer = new Collider[overlapBufferSize];
    }

    void Update()
    {
        RefreshCurrentInteractable();
        RefreshCurrentRepairable();

        if (currentState == PlayerActionState.Repairing && HasMovementInput())
        {
            ExitRepairState("movement");
        }

        if (Input.GetKeyDown(interactionKey)
            && currentState == PlayerActionState.Normal
            && currentRepairable != null
            && CanEnterRepairState(currentRepairable))
        {
            EnterRepairState(currentRepairable);
        }
        else if (logRepairState
                 && Input.GetKeyDown(interactionKey)
                 && currentState == PlayerActionState.Normal
                 && currentInteractable is IRepairable blockedRepairable
                 && !CanEnterRepairState(blockedRepairable))
        {
            Debug.Log(
                $"[PlayerInteractor] {interactionKey} ignored: cannot enter repair (full HP, missing resources, etc.). " +
                $"Target={(blockedRepairable as Component)?.name}",
                this);
        }
        else if (logRepairState
                 && Input.GetKeyDown(interactionKey)
                 && currentState == PlayerActionState.Normal
                 && currentInteractable == null)
        {
            Debug.Log("[PlayerInteractor] No interactable in range.", this);
        }

        if (currentState == PlayerActionState.Repairing && activeRepairTarget != null)
        {
            if (!IsActiveRepairTargetInRange(activeRepairTarget))
            {
                ExitRepairState("out_of_range");
            }
            else if (resourceManager == null)
            {
                ExitRepairState("no_resource_manager");
            }
            else
            {
                bool continueSession = activeRepairTarget.TickRepair(Time.deltaTime, resourceManager);
                if (!continueSession)
                {
                    ExitRepairState("no_wood");
                }
                else if (activeRepairTarget.IsFullyRepaired())
                {
                    ExitRepairState("fully_repaired");
                }
            }
        }

        RefreshPrompt();
        HandleInteractInput();
    }

    void RefreshCurrentRepairable()
    {
        if (currentInteractable is IRepairable repairable)
        {
            currentRepairable = repairable;
        }
        else
        {
            currentRepairable = null;
        }
    }

    bool CanEnterRepairState(IRepairable repairable)
    {
        if (repairable == null)
        {
            return false;
        }

        if (!repairable.IsDamaged)
        {
            return false;
        }

        if (repairable is FenceSegment fence)
        {
            return fence.CanRepair() && fence.CanAffordNextRepairChunk(resourceManager);
        }

        return true;
    }

    void EnterRepairState(IRepairable target)
    {
        activeRepairTarget = target;
        currentState = PlayerActionState.Repairing;

        if (logRepairState)
        {
            Debug.Log(
                $"[PlayerInteractor] Enter Repairing (key={interactionKey}). Target={(target as Component)?.name}, state={currentState}",
                this);
        }
    }

    void ExitRepairState(string reason = "movement")
    {
        if (activeRepairTarget != null)
        {
            activeRepairTarget.CancelRepair();
        }

        activeRepairTarget = null;
        currentState = PlayerActionState.Normal;

        if (logRepairState)
        {
            Debug.Log($"[PlayerInteractor] Exit Repairing -> Normal. ({reason})", this);
        }
    }

    bool IsActiveRepairTargetInRange(IRepairable target)
    {
        Component c = target as Component;
        if (c == null)
        {
            return false;
        }

        Vector3 a = transform.position;
        Vector3 b = c.transform.position;
        a.y = 0f;
        b.y = 0f;
        return (b - a).sqrMagnitude <= interactionRadius * interactionRadius;
    }

    bool HasMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        return Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
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

    void RefreshCurrentInteractable()
    {
        IInteractable nearestInteractable = FindNearestInteractable();
        currentInteractable = nearestInteractable;
    }

    IInteractable FindNearestInteractable()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            interactionRadius,
            overlapBuffer,
            interactableLayerMask);

        IInteractable nearestInteractable = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidateCollider = overlapBuffer[i];
            if (candidateCollider == null)
                continue;

            IInteractable candidateInteractable = ResolveInteractable(candidateCollider);

            if (candidateInteractable == null)
                continue;

            if (!candidateInteractable.CanInteract(this))
                continue;

            float distanceSqr = (candidateInteractable.GetInteractPosition() - transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestInteractable = candidateInteractable;
            }
        }

        return nearestInteractable;
    }

    void RefreshPrompt()
    {
        if (worldPromptUI == null)
            return;

        bool shouldHide = currentInteractable == null;
        InteractablePromptData promptData = default;

        if (!shouldHide)
        {
            promptData = currentInteractable.GetInteractionPromptData(this);
            shouldHide = string.IsNullOrWhiteSpace(promptData.actionText);
        }

        if (shouldHide)
            worldPromptUI.Hide();
        else
            worldPromptUI.Show(currentInteractable, promptData);
    }

    void HandleInteractInput()
    {
        if (!Input.GetKeyDown(interactionKey))
            return;

        if (currentInteractable == null)
            return;

        if (!currentInteractable.CanInteract(this))
            return;

        if (currentInteractable is IRepairable)
            return;

        ResourceNode resourceNode = currentInteractable as ResourceNode;
        if (resourceNode != null)
        {
            if (!isGathering)
                StartCoroutine(GatherResourceOverTime(resourceNode));
            return;
        }

        currentInteractable.Interact(this);
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
