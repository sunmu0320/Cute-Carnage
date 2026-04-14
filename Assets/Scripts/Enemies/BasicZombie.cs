using UnityEngine;

public class BasicZombie : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.4f;

    [Header("Attack")]
    [SerializeField] private float attackDamage = 8f;
    [SerializeField] private float attackInterval = 1f;

    [Header("Targeting")]
    [SerializeField] private float targetRefreshInterval = 1f;

    [Header("Health (Prototype)")]
    [SerializeField] private float maxHp = 30f;
    [SerializeField] private KeyCode debugDamageKey = KeyCode.K;
    [SerializeField] private float debugDamageAmount = 10f;

    [Header("Attack Feedback (Prototype Lunge)")]
    [SerializeField] private float lungeDistance = 0.12f;
    [SerializeField] private float lungeDuration = 0.12f;

    private FenceSegment currentTargetFence;
    private float attackTimer;
    private float targetRefreshTimer;

    private bool isLunging;
    private float lungeTimer;
    private Vector3 lungeStartPosition;
    private Vector3 lungePeakPosition;

    private float currentHp;
    private bool hasDied;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsDead => currentHp <= 0f;

    private float StoppingDistance => Mathf.Max(0.1f, attackRange * 0.85f);

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
        FindNearestFence();
    }

    private void Update()
    {
        if (IsDead)
            return;

        if (Input.GetKeyDown(debugDamageKey))
            TakeDamage(debugDamageAmount);

        RefreshTargetIfNeeded();

        if (currentTargetFence == null)
            return;

        if (isLunging)
        {
            UpdateAttackLunge();
            return;
        }

        HandleMovement();
        HandleAttack();
    }

    private void RefreshTargetIfNeeded()
    {
        bool targetInvalid = currentTargetFence == null || currentTargetFence.IsDestroyed;

        targetRefreshTimer -= Time.deltaTime;
        if (targetInvalid || targetRefreshTimer <= 0f)
        {
            FindNearestFence();
            targetRefreshTimer = Mathf.Max(0.1f, targetRefreshInterval);
        }
    }

    public void FindNearestFence()
    {
        FenceSegment[] fences = FindObjectsOfType<FenceSegment>();

        FenceSegment nearestFence = null;
        float nearestDistanceSqr = float.MaxValue;
        Vector3 myPos = transform.position;
        myPos.y = 0f;

        for (int i = 0; i < fences.Length; i++)
        {
            FenceSegment fence = fences[i];
            if (fence == null || fence.IsDestroyed)
                continue;

            Vector3 fencePos = fence.transform.position;
            fencePos.y = 0f;

            float distanceSqr = (fencePos - myPos).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestFence = fence;
            }
        }

        currentTargetFence = nearestFence;
    }

    private void HandleMovement()
    {
        if (currentTargetFence == null || currentTargetFence.IsDestroyed)
            return;

        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentTargetFence.transform.position;
        targetPos.y = currentPos.y;

        Vector3 toTarget = targetPos - currentPos;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= StoppingDistance)
            return;

        Vector3 moveDirection = toTarget / distanceToTarget;
        Vector3 desiredPos = targetPos - moveDirection * StoppingDistance;
        transform.position = Vector3.MoveTowards(currentPos, desiredPos, moveSpeed * Time.deltaTime);

        if (moveDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
    }

    private void HandleAttack()
    {
        if (currentTargetFence == null || currentTargetFence.IsDestroyed)
            return;

        Vector3 myPos = transform.position;
        myPos.y = 0f;

        Vector3 fencePos = currentTargetFence.transform.position;
        fencePos.y = 0f;

        if (Vector3.Distance(myPos, fencePos) > attackRange)
            return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        currentTargetFence.TakeDamage(attackDamage);
        attackTimer = Mathf.Max(0.05f, attackInterval);
        DoAttackLunge();
    }

    private void DoAttackLunge()
    {
        if (isLunging)
            return;

        lungeStartPosition = transform.position;

        Vector3 toTarget = Vector3.forward;
        if (currentTargetFence != null)
        {
            toTarget = currentTargetFence.transform.position - transform.position;
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude < 0.0001f)
            toTarget = transform.forward;

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
            return;

        currentHp = Mathf.Clamp(currentHp - amount, 0f, maxHp);
        Debug.Log($"[BasicZombie] Took {amount} damage. HP: {currentHp}/{maxHp}", this);

        if (currentHp <= 0f)
            Die();
    }

    private void Die()
    {
        if (hasDied)
            return;

        hasDied = true;
        isLunging = false;
        currentTargetFence = null;

        Debug.Log("[BasicZombie] Zombie died and will be destroyed.", this);
        Destroy(gameObject);
    }
}
