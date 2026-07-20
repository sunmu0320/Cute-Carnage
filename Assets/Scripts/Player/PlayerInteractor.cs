using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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

    [SerializeField, Min(0.1f), Tooltip("Maximum horizontal distance from a structure target before its action panel closes.")]
    float structurePanelStayOpenRadius = 4f;

    [SerializeField, Tooltip("Only colliders on these layers are checked for interaction.")]
    LayerMask interactableLayerMask = ~0;

    [SerializeField, Tooltip("Press this key to interact with the nearest valid target.")]
    KeyCode interactionKey = KeyCode.E;

    [SerializeField, Tooltip("Shared resource inventory used by interactables (fences, nodes, towers, chests).")]
    ResourceManager resourceManager;

    [FormerlySerializedAs("worldPromptUI")]
    [SerializeField, Tooltip("Optional shared interaction prompt UI that follows the current target.")]
    InteractionPromptUI interactionPromptUI;

    [SerializeField, Tooltip("Shared structure action panel opened by structure slots.")]
    StructureActionPanelUI structureActionPanelUI;

    [SerializeField, Tooltip("Night-only repair panel opened for damaged towers.")]
    NightRepairPanelUI nightRepairPanelUI;

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
    [SerializeField, Tooltip("Temporary gather bar UI prefab shown while gathering a resource.")]
    WorldGatherBar worldGatherBarPrefab;
    [SerializeField, Tooltip("Screen Space Overlay canvas RectTransform used to convert gather bar screen positions.")]
    RectTransform gatherOverlayCanvasRect;
    [SerializeField, Tooltip("Container under the overlay canvas where the temporary gather progress bar is instantiated.")]
    RectTransform gatherProgressUIRoot;

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

    public void OpenStructureActionPanel(TowerSlot towerSlot)
    {
        if (nightRepairPanelUI != null)
            nightRepairPanelUI.Close();

        if (structureActionPanelUI != null)
        {
            structureActionPanelUI.Open(towerSlot, this);
            if (structureActionPanelUI.IsOpen && interactionPromptUI != null)
                interactionPromptUI.Hide();
        }
    }

    public void OpenNightRepairPanel(TowerSlot towerSlot)
    {
        if (structureActionPanelUI != null)
            structureActionPanelUI.Close();

        if (nightRepairPanelUI != null)
        {
            nightRepairPanelUI.Open(towerSlot, this);
            if (nightRepairPanelUI.IsOpen && interactionPromptUI != null)
                interactionPromptUI.Hide();
        }
    }

    public bool IsInteractableInRange(IInteractable target)
    {
        if (target == null)
            return false;

        Vector3 playerPosition = transform.position;
        Vector3 targetPosition = target.GetInteractPosition();
        playerPosition.y = 0f;
        targetPosition.y = 0f;

        return (targetPosition - playerPosition).sqrMagnitude
            <= structurePanelStayOpenRadius * structurePanelStayOpenRadius;
    }

    void Awake()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        BindResourceManagerFromActiveScene();
        if (interactionPromptUI == null)
            interactionPromptUI = FindObjectOfType<InteractionPromptUI>();

        if (overlapBufferSize < 4)
            overlapBufferSize = 4;
        overlapBuffer = new Collider[overlapBufferSize];

        SceneManager.sceneLoaded += OnSceneLoadedForResources;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForResources;
    }

    void OnEnable()
    {
        BindResourceManagerFromActiveScene();
    }

    void Start()
    {
        BindResourceManagerFromActiveScene();
    }

    void OnSceneLoadedForResources(Scene scene, LoadSceneMode mode)
    {
        BindResourceManagerFromActiveScene();
    }

    void BindResourceManagerFromActiveScene()
    {
        ResourceManager found = ResourceManager.ResolveForRunStateTransfer();
        if (found != null)
        {
            resourceManager = found;
        }
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

            Vector3 pPos = transform.position;
            Vector3 nPos = resourceNode.transform.position;
            pPos.y = 0f;
            nPos.y = 0f;
            if (Vector3.Distance(pPos, nPos) > resourceNode.GatherDistance + 0.5f)
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

        if (gatherOverlayCanvasRect == null || gatherProgressUIRoot == null)
        {
            Debug.LogWarning("[PlayerInteractor] Gather bar UI references are missing. Assign overlay canvas and GatherProgressUIRoot.", this);
            return;
        }

        Transform anchor = resourceNode.GatherBarAnchor != null ? resourceNode.GatherBarAnchor : resourceNode.transform;
        activeGatherBar = Instantiate(worldGatherBarPrefab, gatherProgressUIRoot);
        if (activeGatherBar == null)
            return;

        activeGatherBar.Initialize(gatherOverlayCanvasRect, anchor);
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
        if (interactionPromptUI == null)
            return;

        if ((structureActionPanelUI != null && structureActionPanelUI.IsOpen)
            || (nightRepairPanelUI != null && nightRepairPanelUI.IsOpen))
        {
            interactionPromptUI.Hide();
            return;
        }

        bool shouldHide = currentInteractable == null;
        InteractablePromptData promptData = default;

        if (!shouldHide)
        {
            promptData = currentInteractable.GetInteractionPromptData(this);
            shouldHide = string.IsNullOrWhiteSpace(promptData.actionText);
        }

        if (shouldHide)
            interactionPromptUI.Hide();
        else
            interactionPromptUI.Show(currentInteractable, promptData);
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
            Vector3 playerPos = transform.position;
            Vector3 nodePos = resourceNode.transform.position;
            playerPos.y = 0f;
            nodePos.y = 0f;
            float horizontalDistance = Vector3.Distance(playerPos, nodePos);
            if (horizontalDistance > resourceNode.GatherDistance)
            {
                if (logRepairState)
                {
                    Debug.Log($"[PlayerInteractor] Too far to gather {resourceNode.gameObject.name}. Distance: {horizontalDistance:F2}, GatherDistance: {resourceNode.GatherDistance:F2}");
                }
                return;
            }

            if (!isGathering)
                StartCoroutine(GatherResourceOverTime(resourceNode));
            return;
        }

        currentInteractable.Interact(this);
    }

    IInteractable ResolveInteractable(Collider candidateCollider)
    {
        // Prioritize ResourceNode in parent hierarchy or on the collider itself.
        ResourceNode parentNode = candidateCollider.GetComponentInParent<ResourceNode>();
        if (parentNode != null)
            return parentNode;

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
