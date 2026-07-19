using UnityEngine;
using UnityEngine.SceneManagement;

public class Zombie : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private const float AttackRangeTolerance = 0.01f;

    /// <summary>Prototype: all Zombie instances in the play session (OnEnable/OnDestroy).</summary>
    public static int AliveZombieCount { get; private set; }

    [Header("Data (optional)")]
    [SerializeField] private ZombieData zombieData;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.4f;

    [Header("Attack")]
    [SerializeField] private float attackDamage = 8f;
    [SerializeField] private float attackInterval = 1f;

    [Header("Targeting")]
    [SerializeField] private float targetRefreshInterval = 1f;

    [SerializeField] private float forwardDetectRange = 1.6f;
    [SerializeField] private float forwardDetectRadius = 0.6f;
    [SerializeField] private float forwardDetectStartOffset = 0.2f;
    [SerializeField] private LayerMask forwardDetectMask = ~0;
    [SerializeField] private string[] baseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };
    [SerializeField] private bool enableFrontTargetDebugLogs = false;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawDetectGizmoAlways = false;
    [SerializeField] private Color detectGizmoColor = new Color(0.2f, 1f, 0.4f, 0.8f);
    [SerializeField] private Color attackRangeGizmoColor = new Color(1f, 0.4f, 0.3f, 0.6f);

    [Header("Health (Prototype)")]
    [SerializeField] private float maxHp = 30f;
    [SerializeField] private KeyCode debugDamageKey = KeyCode.K;
    [SerializeField] private float debugDamageAmount = 10f;
    [SerializeField, Min(0f)] private float deathDestroyDelay = 5f;

    private enum TargetKind
    {
        None,
        Fence,
        Tower,
        Player,
        BaseCore
    }

    private TargetKind currentTargetKind;
    private FenceSegment currentTargetFence;
    private ArrowTower currentTargetTower;
    private PlayerHealth currentTargetPlayer;
    private BaseCore currentTargetBaseCore;
    private Transform currentTargetTransform;
    private Collider currentTargetCollider;
    private bool hasDetectedCombatTarget;
    private Transform cachedBaseCoreTransform;
    private readonly Collider[] forwardHitBuffer = new Collider[64];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool hasWarnedForwardHitBufferSaturated;
#endif

    private float attackTimer;
    private float targetRefreshTimer;

    private float currentHp;
    private bool hasDied;
    private string lastFrontTargetLogKey;

    // Cached from ZombieData
    private ZombieType zombieType;
    private ZombieTargetPreference targetPreference;
    private RuntimeAnimatorController animatorController;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsDead => currentHp <= 0f;

    private void OnEnable()
    {
        AliveZombieCount++;
    }

    private void OnDestroy()
    {
        AliveZombieCount = Mathf.Max(0, AliveZombieCount - 1);
    }

    private void Awake()
    {
        ApplyZombieData();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            if (animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = animatorController;
            }

            animator.applyRootMotion = false;
        }

        maxHp = Mathf.Max(0.1f, maxHp);
        currentHp = Mathf.Clamp(maxHp, 0f, maxHp);
        hasDied = false;
    }

    private void ApplyZombieData()
    {
        if (zombieData == null)
        {
            return; // Use serialized fallbacks
        }

        maxHp = zombieData.maxHp;
        moveSpeed = zombieData.moveSpeed;
        attackDamage = zombieData.attackDamage;
        attackRange = zombieData.attackRange;
        attackInterval = zombieData.attackInterval;
        targetRefreshInterval = zombieData.targetRefreshInterval;
        forwardDetectRange = zombieData.forwardDetectRange;
        forwardDetectRadius = zombieData.forwardDetectRadius;

        zombieType = zombieData.zombieType;
        targetPreference = zombieData.targetPreference;
        animatorController = zombieData.animatorController;
    }

    private void Start()
    {
        attackTimer = 0f;
        targetRefreshTimer = 0f;
        EnsureBaseCoreCached();
        SelectFrontTarget();
    }

    private void Update()
    {
        if (IsDead)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f);
        }

        if (Input.GetKeyDown(debugDamageKey))
        {
            TakeDamage(debugDamageAmount);
        }

        RefreshTargetIfNeeded();

        if (currentTargetTransform == null)
        {
            return;
        }

        HandleMovement();
        HandleAttack();
    }

    private void RefreshTargetIfNeeded()
    {
        bool targetInvalid = IsCurrentTargetInvalid();

        targetRefreshTimer -= Time.deltaTime;
        if (targetInvalid || targetRefreshTimer <= 0f)
        {
            SelectFrontTarget();
            targetRefreshTimer = Mathf.Max(0.1f, targetRefreshInterval);
        }
    }

    void EnsureBaseCoreCached()
    {
        if (cachedBaseCoreTransform == null)
        {
            cachedBaseCoreTransform = ResolveBaseCoreTransform();
        }
    }

    Transform ResolveBaseCoreTransform()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            // Prefer the same "HomeBase + PersistentId" rule as GameManager, when present.
            if (roots[i].name == "HomeBase")
            {
                PersistentId rootPersistentId = roots[i].GetComponent<PersistentId>();
                if (rootPersistentId != null)
                {
                    return roots[i].transform;
                }

                PersistentId[] childPersistentIds = roots[i].GetComponentsInChildren<PersistentId>(true);
                if (childPersistentIds.Length > 0 && childPersistentIds[0] != null)
                {
                    return childPersistentIds[0].transform;
                }
            }
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            GameObject found = FindBaseObjectByNameRecursive(roots[i].transform);
            if (found != null)
            {
                return found.transform;
            }
        }

        return null;
    }

    GameObject FindBaseObjectByNameRecursive(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        if (baseCoreNameCandidates != null)
        {
            for (int i = 0; i < baseCoreNameCandidates.Length; i++)
            {
                if (root.name == baseCoreNameCandidates[i])
                {
                    return root.gameObject;
                }
            }
        }

        for (int c = 0; c < root.childCount; c++)
        {
            GameObject childFound = FindBaseObjectByNameRecursive(root.GetChild(c));
            if (childFound != null)
            {
                return childFound;
            }
        }

        return null;
    }

    bool IsCurrentTargetInvalid()
    {
        // Idling with no target is valid; re-pick on interval only (avoids OverlapCapsule every frame).
        if (currentTargetKind == TargetKind.None)
        {
            return false;
        }

        if (currentTargetTransform == null)
        {
            return true;
        }

        if (!currentTargetTransform.gameObject.activeInHierarchy)
        {
            return true;
        }

        if (hasDetectedCombatTarget && !IsCurrentTargetColliderValid(currentTargetCollider))
        {
            return true;
        }

        switch (currentTargetKind)
        {
            case TargetKind.Fence:
                return currentTargetFence == null || !currentTargetFence.isActiveAndEnabled || currentTargetFence.IsDestroyed;
            case TargetKind.Tower:
                return currentTargetTower == null || !currentTargetTower.isActiveAndEnabled || currentTargetTower.IsDestroyed;
            case TargetKind.Player:
                return currentTargetPlayer == null || !currentTargetPlayer.isActiveAndEnabled || currentTargetPlayer.IsDead;
            case TargetKind.BaseCore:
                if (cachedBaseCoreTransform == null)
                {
                    return true;
                }

                if (!cachedBaseCoreTransform)
                {
                    return true;
                }

                if (!cachedBaseCoreTransform.gameObject.activeInHierarchy)
                {
                    return true;
                }

                if (currentTargetBaseCore == null)
                {
                    currentTargetBaseCore = cachedBaseCoreTransform.GetComponentInParent<BaseCore>();
                    if (currentTargetBaseCore == null)
                    {
                        currentTargetBaseCore = cachedBaseCoreTransform.GetComponentInChildren<BaseCore>();
                    }
                }

                return currentTargetBaseCore != null &&
                    (!currentTargetBaseCore.isActiveAndEnabled || currentTargetBaseCore.IsDestroyed);
        }

        return true;
    }

    void SelectFrontTarget()
    {
        ClearTargetSelection();

        if (cachedBaseCoreTransform == null || !cachedBaseCoreTransform)
        {
            cachedBaseCoreTransform = null;
        }

        EnsureBaseCoreCached();

        Vector3 forward = GetPlanarForward();
        float range = Mathf.Max(0.01f, forwardDetectRange);
        float radius = Mathf.Max(0.01f, forwardDetectRadius);
        float startOff = forwardDetectStartOffset;
        Vector3 fFlat = new Vector3(forward.x, 0f, forward.z).normalized;

        Vector3 p0 = transform.position + fFlat * startOff;
        Vector3 p1 = transform.position + fFlat * (startOff + range);
        p0.y = transform.position.y;
        p1.y = transform.position.y;

        int count = Physics.OverlapCapsuleNonAlloc(
            p0,
            p1,
            radius,
            forwardHitBuffer,
            forwardDetectMask,
            QueryTriggerInteraction.Collide);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (count == forwardHitBuffer.Length && !hasWarnedForwardHitBufferSaturated)
        {
            hasWarnedForwardHitBufferSaturated = true;
            Debug.LogWarning("[Zombie] Forward target detection buffer is full; some Colliders may not have been evaluated.", this);
        }
#endif

        TargetKind bestKind = TargetKind.None;
        float bestTargetSqr = float.MaxValue;
        FenceSegment bestFence = null;
        ArrowTower bestTower = null;
        PlayerHealth bestPlayer = null;
        BaseCore bestBaseCore = null;
        Transform bestTransform = null;
        Collider bestCollider = null;
        for (int i = 0; i < count; i++)
        {
            Collider col = forwardHitBuffer[i];
            if (col == null)
            {
                continue;
            }

            Zombie zombie = col.GetComponentInParent<Zombie>();
            if (zombie != null)
            {
                if (zombie == this)
                {
                    continue;
                }
            }

            Vector3 closestPoint = col.ClosestPoint(transform.position);
            closestPoint.y = transform.position.y;
            float surfaceSqr = HorizontalDistanceSqr(transform.position, closestPoint);

            FenceSegment fence = col.GetComponentInParent<FenceSegment>();
            if (fence != null)
            {
                if (fence.isActiveAndEnabled && !fence.IsDestroyed && surfaceSqr < bestTargetSqr)
                {
                    bestKind = TargetKind.Fence;
                    bestTargetSqr = surfaceSqr;
                    bestFence = fence;
                    bestTower = null;
                    bestPlayer = null;
                    bestBaseCore = null;
                    bestTransform = fence.transform;
                    bestCollider = col;
                }

                continue;
            }

            ArrowTower tower = col.GetComponentInParent<ArrowTower>();
            if (tower != null)
            {
                if (tower.isActiveAndEnabled && !tower.IsDestroyed && surfaceSqr < bestTargetSqr)
                {
                    bestKind = TargetKind.Tower;
                    bestTargetSqr = surfaceSqr;
                    bestFence = null;
                    bestTower = tower;
                    bestPlayer = null;
                    bestBaseCore = null;
                    bestTransform = tower.transform;
                    bestCollider = col;
                }

                continue;
            }

            PlayerHealth ph = col.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                if (ph.isActiveAndEnabled && !ph.IsDead && surfaceSqr < bestTargetSqr)
                {
                    bestKind = TargetKind.Player;
                    bestTargetSqr = surfaceSqr;
                    bestFence = null;
                    bestTower = null;
                    bestPlayer = ph;
                    bestBaseCore = null;
                    bestTransform = ph.transform;
                    bestCollider = col;
                }

                continue;
            }

            BaseCore core = col.GetComponentInParent<BaseCore>();
            if (core != null)
            {
                if (core.isActiveAndEnabled &&
                    !core.IsDestroyed &&
                    core.AttackCollider != null &&
                    col == core.AttackCollider &&
                    col.enabled &&
                    col.gameObject.activeInHierarchy &&
                    surfaceSqr < bestTargetSqr)
                {
                    bestKind = TargetKind.BaseCore;
                    bestTargetSqr = surfaceSqr;
                    bestFence = null;
                    bestTower = null;
                    bestPlayer = null;
                    bestBaseCore = core;
                    bestTransform = core.transform;
                    bestCollider = col;
                }
            }
        }

        if (bestKind == TargetKind.Fence && bestFence != null)
        {
            currentTargetKind = TargetKind.Fence;
            currentTargetFence = bestFence;
            currentTargetTransform = bestFence.transform;
            currentTargetCollider = bestCollider;
            hasDetectedCombatTarget = true;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestFence.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestFence.name}");
            return;
        }

        if (bestKind == TargetKind.Tower && bestTower != null)
        {
            currentTargetKind = TargetKind.Tower;
            currentTargetTower = bestTower;
            currentTargetTransform = bestTower.transform;
            currentTargetCollider = bestCollider;
            hasDetectedCombatTarget = true;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestTower.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestTower.name}");
            return;
        }

        if (bestKind == TargetKind.Player && bestPlayer != null)
        {
            currentTargetKind = TargetKind.Player;
            currentTargetPlayer = bestPlayer;
            currentTargetTransform = bestPlayer.transform;
            currentTargetCollider = bestCollider;
            hasDetectedCombatTarget = true;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestPlayer.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestPlayer.name}");
            return;
        }

        if (bestKind == TargetKind.BaseCore && bestBaseCore != null)
        {
            currentTargetKind = TargetKind.BaseCore;
            currentTargetBaseCore = bestBaseCore;
            currentTargetTransform = bestTransform != null ? bestTransform : bestBaseCore.transform;
            currentTargetCollider = bestBaseCore.AttackCollider;
            hasDetectedCombatTarget = true;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestBaseCore.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestBaseCore.name}");
            return;
        }

        if (cachedBaseCoreTransform != null)
        {
            currentTargetKind = TargetKind.None;
            currentTargetTransform = cachedBaseCoreTransform;
            LogFrontTargetChange("fallback:basecore", "No front target, moving to BaseCore");
            return;
        }

        currentTargetKind = TargetKind.None;
        LogFrontTargetChange("fallback:none", "No front target, moving to BaseCore");
    }

    private void LogFrontTargetChange(string key, string message)
    {
        if (!enableFrontTargetDebugLogs)
        {
            return;
        }

        if (lastFrontTargetLogKey == key)
        {
            return;
        }

        lastFrontTargetLogKey = key;
        Debug.Log($"[Zombie] {message}", this);
    }

    void ClearTargetSelection()
    {
        currentTargetKind = TargetKind.None;
        currentTargetFence = null;
        currentTargetTower = null;
        currentTargetPlayer = null;
        currentTargetBaseCore = null;
        currentTargetTransform = null;
        currentTargetCollider = null;
        hasDetectedCombatTarget = false;
    }

    private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (b - a).sqrMagnitude;
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f)
        {
            f = Vector3.forward;
        }
        else
        {
            f.Normalize();
        }

        return f;
    }

    private void HandleMovement()
    {
        if (currentTargetTransform == null)
        {
            return;
        }

        if (!hasDetectedCombatTarget)
        {
            MoveTowardBaseFallback();
            return;
        }

        if (currentTargetKind == TargetKind.Fence && (currentTargetFence == null || currentTargetFence.IsDestroyed))
        {
            return;
        }

        if (currentTargetKind == TargetKind.Tower && (currentTargetTower == null || currentTargetTower.IsDestroyed))
        {
            return;
        }

        if (currentTargetKind == TargetKind.Player && (currentTargetPlayer == null || currentTargetPlayer.IsDead))
        {
            return;
        }

        if (currentTargetKind == TargetKind.BaseCore)
        {
            if (cachedBaseCoreTransform == null)
            {
                return;
            }

            if (!cachedBaseCoreTransform)
            {
                return;
            }

            if (currentTargetBaseCore != null && currentTargetBaseCore.IsDestroyed)
            {
                return;
            }
        }

        Vector3 currentPos = transform.position;
        Collider targetCollider = ResolveCurrentTargetCollider();
        if (targetCollider == null)
        {
            return;
        }

        Vector3 closestPoint = targetCollider.ClosestPoint(currentPos);
        closestPoint.y = currentPos.y;

        Vector3 toSurface = closestPoint - currentPos;
        float surfaceDistance = toSurface.magnitude;
        if (surfaceDistance <= AttackRangeTolerance && TryMoveOutsideTarget(targetCollider, currentPos))
        {
            return;
        }

        if (surfaceDistance <= attackRange + AttackRangeTolerance)
        {
            return;
        }

        Vector3 moveDirection = toSurface / surfaceDistance;
        Vector3 desiredPos = closestPoint - moveDirection * attackRange;
        transform.position = Vector3.MoveTowards(currentPos, desiredPos, moveSpeed * Time.deltaTime);

        if (animator != null && transform.position != currentPos)
        {
            animator.SetFloat(SpeedHash, 1f);
        }

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        }
    }

    private void HandleAttack()
    {
        if (!hasDetectedCombatTarget)
        {
            return;
        }

        if (currentTargetTransform == null)
        {
            return;
        }

        if (currentTargetKind == TargetKind.Fence)
        {
            if (currentTargetFence == null || currentTargetFence.IsDestroyed)
            {
                return;
            }
        }
        else if (currentTargetKind == TargetKind.Tower)
        {
            if (currentTargetTower == null || currentTargetTower.IsDestroyed)
            {
                return;
            }
        }
        else if (currentTargetKind == TargetKind.Player)
        {
            if (currentTargetPlayer == null || currentTargetPlayer.IsDead)
            {
                return;
            }
        }
        else if (currentTargetKind == TargetKind.BaseCore)
        {
            if (cachedBaseCoreTransform == null)
            {
                return;
            }

            if (currentTargetBaseCore == null && currentTargetTransform != null)
            {
                currentTargetBaseCore = currentTargetTransform.GetComponentInParent<BaseCore>();
                if (currentTargetBaseCore == null)
                {
                    currentTargetBaseCore = currentTargetTransform.GetComponentInChildren<BaseCore>();
                }
            }

            if (currentTargetBaseCore == null || currentTargetBaseCore.IsDestroyed)
            {
                return;
            }
        }

        Vector3 myPos = transform.position;
        myPos.y = 0f;

        float distanceToTarget = GetPlanarDistanceToCurrentTargetSurface(myPos);
        if (distanceToTarget > attackRange + AttackRangeTolerance)
        {
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }

        if (currentTargetKind == TargetKind.Fence && currentTargetFence != null)
        {
            currentTargetFence.TakeDamage(attackDamage);
        }
        else if (currentTargetKind == TargetKind.Tower && currentTargetTower != null)
        {
            currentTargetTower.TakeDamage(attackDamage);
        }
        else if (currentTargetKind == TargetKind.Player && currentTargetPlayer != null)
        {
            int damage = Mathf.Max(1, Mathf.CeilToInt(attackDamage));
            currentTargetPlayer.TakeDamage(damage);
        }
        else if (currentTargetKind == TargetKind.BaseCore && currentTargetBaseCore != null)
        {
            currentTargetBaseCore.TakeDamage(attackDamage);
        }

        attackTimer = Mathf.Max(0.05f, attackInterval);
    }

    private float GetPlanarDistanceToCurrentTargetSurface(Vector3 fromPosition)
    {
        Collider targetCollider = ResolveCurrentTargetCollider();
        if (targetCollider == null)
        {
            return float.MaxValue;
        }

        Vector3 closestPoint = targetCollider.ClosestPoint(fromPosition);
        closestPoint.y = fromPosition.y;
        return Vector3.Distance(fromPosition, closestPoint);
    }

    private Collider ResolveCurrentTargetCollider()
    {
        if (IsCurrentTargetColliderValid(currentTargetCollider))
        {
            return currentTargetCollider;
        }

        if (currentTargetKind == TargetKind.BaseCore)
        {
            currentTargetCollider = currentTargetBaseCore != null
                ? currentTargetBaseCore.AttackCollider
                : null;
            return IsCurrentTargetColliderValid(currentTargetCollider)
                ? currentTargetCollider
                : null;
        }

        Transform targetRoot = GetCurrentTargetRoot();
        if (targetRoot == null)
        {
            currentTargetCollider = null;
            return null;
        }

        currentTargetCollider = FindPreferredTargetCollider(targetRoot);
        return currentTargetCollider;
    }

    private bool IsCurrentTargetColliderValid(Collider candidate)
    {
        if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        switch (currentTargetKind)
        {
            case TargetKind.Fence:
                return candidate.GetComponentInParent<FenceSegment>() == currentTargetFence;
            case TargetKind.Tower:
                return candidate.GetComponentInParent<ArrowTower>() == currentTargetTower;
            case TargetKind.Player:
                return candidate.GetComponentInParent<PlayerHealth>() == currentTargetPlayer;
            case TargetKind.BaseCore:
                return currentTargetBaseCore != null &&
                    currentTargetBaseCore.AttackCollider != null &&
                    candidate == currentTargetBaseCore.AttackCollider;
            default:
                return false;
        }
    }

    private Transform GetCurrentTargetRoot()
    {
        switch (currentTargetKind)
        {
            case TargetKind.Fence:
                return currentTargetFence != null ? currentTargetFence.transform : null;
            case TargetKind.Tower:
                return currentTargetTower != null ? currentTargetTower.transform : null;
            case TargetKind.Player:
                return currentTargetPlayer != null ? currentTargetPlayer.transform : null;
            case TargetKind.BaseCore:
                return currentTargetBaseCore != null ? currentTargetBaseCore.transform : null;
            default:
                return null;
        }
    }

    private static Collider FindPreferredTargetCollider(Transform targetRoot)
    {
        Collider fallback = null;
        Collider[] colliders = targetRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!candidate.isTrigger)
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private void MoveTowardBaseFallback()
    {
        if (cachedBaseCoreTransform == null || !cachedBaseCoreTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 destination = cachedBaseCoreTransform.position;
        destination.y = currentPosition.y;

        Vector3 toDestination = destination - currentPosition;
        if (toDestination.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 moveDirection = toDestination.normalized;
        transform.position = Vector3.MoveTowards(
            currentPosition,
            destination,
            moveSpeed * Time.deltaTime);

        if (animator != null && transform.position != currentPosition)
        {
            animator.SetFloat(SpeedHash, 1f);
        }

        transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
    }

    private bool TryMoveOutsideTarget(Collider targetCollider, Vector3 currentPosition)
    {
        Collider zombieCollider = GetComponent<Collider>();
        if (zombieCollider == null || !zombieCollider.enabled)
        {
            return false;
        }

        if (!Physics.ComputePenetration(
                zombieCollider,
                zombieCollider.transform.position,
                zombieCollider.transform.rotation,
                targetCollider,
                targetCollider.transform.position,
                targetCollider.transform.rotation,
                out Vector3 separationDirection,
                out float separationDistance))
        {
            return false;
        }

        separationDirection.y = 0f;
        if (separationDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        separationDirection.Normalize();
        Vector3 outsidePosition = currentPosition +
            separationDirection * (separationDistance + AttackRangeTolerance);
        outsidePosition.y = currentPosition.y;
        transform.position = Vector3.MoveTowards(
            currentPosition,
            outsidePosition,
            moveSpeed * Time.deltaTime);

        if (animator != null && transform.position != currentPosition)
        {
            animator.SetFloat(SpeedHash, 1f);
        }

        transform.rotation = Quaternion.LookRotation(separationDirection, Vector3.up);
        return true;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        currentHp = Mathf.Clamp(currentHp - amount, 0f, maxHp);
        Debug.Log($"[Zombie] Took {amount} damage. HP: {currentHp}/{maxHp}", this);

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (hasDied)
        {
            return;
        }

        hasDied = true;
        ClearTargetSelection();
        cachedBaseCoreTransform = null;

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }

        CapsuleCollider rootCollider = GetComponent<CapsuleCollider>();
        if (rootCollider != null)
        {
            rootCollider.enabled = false;
        }

        Debug.Log("[Zombie] Zombie died and will be destroyed.", this);
        Destroy(gameObject, deathDestroyDelay);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawDetectGizmo();
    }

    private void OnDrawGizmos()
    {
        if (drawDetectGizmoAlways)
        {
            DrawDetectGizmo();
        }
    }

    private void DrawDetectGizmo()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        Vector3 origin = transform.position;
        Vector3 forward = GetPlanarForward();
        float startOff = forwardDetectStartOffset;
        float range = Mathf.Max(0.01f, forwardDetectRange);
        float radius = Mathf.Max(0.01f, forwardDetectRadius);
        Vector3 p0 = origin + forward * startOff;
        Vector3 p1 = origin + forward * (startOff + range);
        p0.y = origin.y;
        p1.y = origin.y;

        Gizmos.color = detectGizmoColor;
        Gizmos.DrawWireSphere(p0, radius);
        Gizmos.DrawWireSphere(p1, radius);

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.up;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = transform.right;
        }

        Gizmos.DrawLine(p0 + right * radius, p1 + right * radius);
        Gizmos.DrawLine(p0 - right * radius, p1 - right * radius);
        Gizmos.DrawLine(p0 + up * radius, p1 + up * radius);
        Gizmos.DrawLine(p0 - up * radius, p1 - up * radius);

        Gizmos.DrawLine(origin, p0);
        Gizmos.DrawLine(p0, p1);

        Gizmos.color = attackRangeGizmoColor;
        DrawHorizontalCircle(origin, Mathf.Max(0.01f, attackRange), 32);
    }

    private static void DrawHorizontalCircle(Vector3 center, float radius, int segments)
    {
        if (segments < 3)
        {
            segments = 3;
        }

        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
