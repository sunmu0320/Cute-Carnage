using UnityEngine;
using UnityEngine.SceneManagement;

public class BasicZombie : MonoBehaviour
{
    /// <summary>Prototype: all BasicZombie instances in the play session (OnEnable/OnDestroy).</summary>
    public static int AliveZombieCount { get; private set; }

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

    [Header("Attack Feedback (Prototype Lunge)")]
    [SerializeField] private float lungeDistance = 0.12f;
    [SerializeField] private float lungeDuration = 0.12f;

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
    private Transform cachedBaseCoreTransform;
    private readonly Collider[] forwardHitBuffer = new Collider[16];

    private float attackTimer;
    private float targetRefreshTimer;

    private bool isLunging;
    private float lungeTimer;
    private Vector3 lungeStartPosition;
    private Vector3 lungePeakPosition;

    private float currentHp;
    private bool hasDied;
    private string lastFrontTargetLogKey;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsDead => currentHp <= 0f;

    private float StoppingDistance => Mathf.Max(0.1f, attackRange * 0.85f);
    private float AttackRangeSqr => attackRange * attackRange;

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
        maxHp = Mathf.Max(0.1f, maxHp);
        currentHp = Mathf.Clamp(maxHp, 0f, maxHp);
        hasDied = false;
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

        if (Input.GetKeyDown(debugDamageKey))
        {
            TakeDamage(debugDamageAmount);
        }

        RefreshTargetIfNeeded();

        if (isLunging)
        {
            UpdateAttackLunge();
            return;
        }

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

        switch (currentTargetKind)
        {
            case TargetKind.Fence:
                return currentTargetFence == null || currentTargetFence.IsDestroyed;
            case TargetKind.Tower:
                return currentTargetTower == null || currentTargetTower.IsDestroyed;
            case TargetKind.Player:
                return currentTargetPlayer == null || currentTargetPlayer.IsDead;
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
                }

                return currentTargetBaseCore != null && currentTargetBaseCore.IsDestroyed;
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
        Vector3 pos = transform.position;
        pos.y = 0f;
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

        TargetKind bestKind = TargetKind.None;
        float bestTargetSqr = float.MaxValue;
        FenceSegment bestFence = null;
        ArrowTower bestTower = null;
        PlayerHealth bestPlayer = null;
        BaseCore bestBaseCore = null;
        Transform bestTransform = null;
        float attackRangeSqr = AttackRangeSqr;

        for (int i = 0; i < count; i++)
        {
            Collider col = forwardHitBuffer[i];
            if (col == null)
            {
                continue;
            }

            BasicZombie zombie = col.GetComponentInParent<BasicZombie>();
            if (zombie != null)
            {
                if (zombie == this)
                {
                    continue;
                }
            }

            Vector3 colCenter = col.bounds.center;
            colCenter.y = 0f;
            float sqr = HorizontalDistanceSqr(pos, colCenter);

            FenceSegment fence = col.GetComponentInParent<FenceSegment>();
            if (fence != null)
            {
                if (!fence.IsDestroyed && sqr < bestTargetSqr)
                {
                    bestKind = TargetKind.Fence;
                    bestTargetSqr = sqr;
                    bestFence = fence;
                    bestTower = null;
                    bestPlayer = null;
                    bestBaseCore = null;
                    bestTransform = fence.transform;
                }

                continue;
            }

            ArrowTower tower = col.GetComponentInParent<ArrowTower>();
            if (tower != null)
            {
                if (!tower.IsDestroyed && sqr < bestTargetSqr)
                {
                    bestKind = TargetKind.Tower;
                    bestTargetSqr = sqr;
                    bestFence = null;
                    bestTower = tower;
                    bestPlayer = null;
                    bestBaseCore = null;
                    bestTransform = tower.transform;
                }

                continue;
            }

            PlayerHealth ph = col.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                if (!ph.IsDead && sqr < bestTargetSqr)
                {
                    bestKind = TargetKind.Player;
                    bestTargetSqr = sqr;
                    bestFence = null;
                    bestTower = null;
                    bestPlayer = ph;
                    bestBaseCore = null;
                    bestTransform = ph.transform;
                }

                continue;
            }

            BaseCore core = col.GetComponentInParent<BaseCore>();
            if (core != null)
            {
                if (!core.IsDestroyed && sqr <= attackRangeSqr + 0.01f && sqr < bestTargetSqr)
                {
                    bestKind = TargetKind.BaseCore;
                    bestTargetSqr = sqr;
                    bestFence = null;
                    bestTower = null;
                    bestPlayer = null;
                    bestBaseCore = core;
                    bestTransform = core.transform;
                }
            }
        }

        if (bestKind == TargetKind.Fence && bestFence != null)
        {
            currentTargetKind = TargetKind.Fence;
            currentTargetFence = bestFence;
            currentTargetTransform = bestFence.transform;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestFence.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestFence.name}");
            return;
        }

        if (bestKind == TargetKind.Tower && bestTower != null)
        {
            currentTargetKind = TargetKind.Tower;
            currentTargetTower = bestTower;
            currentTargetTransform = bestTower.transform;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestTower.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestTower.name}");
            return;
        }

        if (bestKind == TargetKind.Player && bestPlayer != null)
        {
            currentTargetKind = TargetKind.Player;
            currentTargetPlayer = bestPlayer;
            currentTargetTransform = bestPlayer.transform;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestPlayer.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestPlayer.name}");
            return;
        }

        if (bestKind == TargetKind.BaseCore && bestBaseCore != null)
        {
            currentTargetKind = TargetKind.BaseCore;
            currentTargetBaseCore = bestBaseCore;
            currentTargetTransform = bestTransform != null ? bestTransform : bestBaseCore.transform;
            LogFrontTargetChange($"front:{currentTargetKind}:{bestBaseCore.GetInstanceID()}", $"Front target selected: {currentTargetKind} {bestBaseCore.name}");
            return;
        }

        if (cachedBaseCoreTransform != null)
        {
            currentTargetBaseCore = cachedBaseCoreTransform.GetComponentInParent<BaseCore>();
            currentTargetKind = TargetKind.BaseCore;
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
        Debug.Log($"[BasicZombie] {message}", this);
    }

    void ClearTargetSelection()
    {
        currentTargetKind = TargetKind.None;
        currentTargetFence = null;
        currentTargetTower = null;
        currentTargetPlayer = null;
        currentTargetBaseCore = null;
        currentTargetTransform = null;
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
        Vector3 targetPos = currentTargetTransform.position;
        targetPos.y = currentPos.y;

        Vector3 toTarget = targetPos - currentPos;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= StoppingDistance)
        {
            return;
        }

        Vector3 moveDirection = toTarget / distanceToTarget;
        Vector3 desiredPos = targetPos - moveDirection * StoppingDistance;
        transform.position = Vector3.MoveTowards(currentPos, desiredPos, moveSpeed * Time.deltaTime);

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        }
    }

    private void HandleAttack()
    {
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
            }

            if (currentTargetBaseCore == null || currentTargetBaseCore.IsDestroyed)
            {
                return;
            }
        }

        Vector3 myPos = transform.position;
        myPos.y = 0f;

        Vector3 targetPos = currentTargetTransform.position;
        targetPos.y = 0f;

        if (Vector3.Distance(myPos, targetPos) > attackRange)
        {
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
        {
            return;
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
        DoAttackLunge();
    }

    private void DoAttackLunge()
    {
        if (isLunging)
        {
            return;
        }

        lungeStartPosition = transform.position;

        Vector3 toTarget = Vector3.forward;
        if (currentTargetTransform != null)
        {
            toTarget = currentTargetTransform.position - transform.position;
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            toTarget = transform.forward;
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            toTarget = Vector3.forward;
        }

        Vector3 lungeDirection = toTarget.normalized;
        lungePeakPosition = lungeStartPosition + lungeDirection * Mathf.Max(0f, lungeDistance);

        isLunging = true;
        lungeTimer = 0f;
    }

    private void UpdateAttackLunge()
    {
        float safeDuration = Mathf.Max(0.02f, lungeDuration);
        lungeTimer += Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(lungeTimer / safeDuration);
        if (normalizedTime < 0.5f)
        {
            float t = normalizedTime / 0.5f;
            transform.position = Vector3.Lerp(lungeStartPosition, lungePeakPosition, t);
        }
        else
        {
            float t = (normalizedTime - 0.5f) / 0.5f;
            transform.position = Vector3.Lerp(lungePeakPosition, lungeStartPosition, t);
        }

        if (normalizedTime >= 1f)
        {
            transform.position = lungeStartPosition;
            isLunging = false;
        }
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        currentHp = Mathf.Clamp(currentHp - amount, 0f, maxHp);
        Debug.Log($"[BasicZombie] Took {amount} damage. HP: {currentHp}/{maxHp}", this);

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
        isLunging = false;
        ClearTargetSelection();
        cachedBaseCoreTransform = null;

        Debug.Log("[BasicZombie] Zombie died and will be destroyed.", this);
        Destroy(gameObject);
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
