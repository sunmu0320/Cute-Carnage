using UnityEngine;

public class ArrowTower : MonoBehaviour, IStructureHpSource
{
    [Header("Health")]
    [SerializeField] private float maxHp = 80f;
    [SerializeField] private float currentHp = 80f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackInterval = 1f;
    [SerializeField] private float damage = 5f;
    [SerializeField] private float hitRadius = 0.25f;
    [SerializeField] private float targetAimHeightOffset = 0.75f;

    [Header("References")]
    [SerializeField] private Transform aimYawPivot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectileVisualPrefab;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private LayerMask targetLayerMask;

    [Header("Destroyed State Visuals")]
    [SerializeField] private GameObject activeVisualRoot;
    [SerializeField] private GameObject destroyedVisualRoot;
    [SerializeField] private Collider[] towerColliders;

    private readonly Collider[] hitBuffer = new Collider[64];
    private float attackTimer;
    private Transform currentTarget;
    private Collider[] cachedColliders;
    private bool isDestroyed;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsDestroyed => isDestroyed;
    public Transform HpAnchorTransform => transform;

    public void ApplyRuntimeDurability(float runtimeCurrentHp, bool runtimeDestroyed)
    {
        float clampedHp = Mathf.Clamp(runtimeCurrentHp, 0f, maxHp);
        isDestroyed = runtimeDestroyed || clampedHp <= 0f;
        currentHp = isDestroyed ? 0f : clampedHp;
        if (isDestroyed)
        {
            ClearAttackState();
        }

        ApplyDestroyedState();
    }

    private void Awake()
    {
        ClampHealthSettings(resetCurrentToMaxIfNeeded: true);
        isDestroyed = currentHp <= 0f;
        ResolveTowerColliders();
        ApplyDestroyedState();
    }

    private void OnValidate()
    {
        ClampHealthSettings(resetCurrentToMaxIfNeeded: false);
        isDestroyed = currentHp <= 0f;
        ResolveTowerColliders();
        ApplyDestroyedState();
    }

    private void Update()
    {
        if (isDestroyed || currentHp <= 0f)
        {
            if (!isDestroyed)
            {
                SetDestroyed();
            }
            return;
        }

        // ArrowTower controls target selection, yaw-only visual aiming, and firing cadence.
        if (!IsTargetValid(currentTarget))
        {
            currentTarget = null;
        }

        if (currentTarget == null)
        {
            currentTarget = FindTarget();
        }

        if (currentTarget == null)
        {
            return;
        }

        RotateYawTowardTarget();
        TryFireAtTarget();
    }

    public void TakeDamage(float damageAmount)
    {
        if (damageAmount <= 0f || isDestroyed)
        {
            return;
        }

        currentHp = Mathf.Clamp(currentHp - damageAmount, 0f, maxHp);
        Debug.Log($"[ArrowTower] Tower damaged by {damageAmount:0.##}. HP: {currentHp:0.##}/{maxHp:0.##}", this);

        if (currentHp <= 0f)
        {
            SetDestroyed();
        }
    }

    private void SetDestroyed()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;
        currentHp = 0f;
        ClearAttackState();
        ApplyDestroyedState();
        Debug.Log($"[ArrowTower] Destroyed: {name}", this);
    }

    private void ClearAttackState()
    {
        currentTarget = null;
        attackTimer = 0f;
    }

    private void ResolveTowerColliders()
    {
        if (towerColliders != null && towerColliders.Length > 0)
        {
            cachedColliders = towerColliders;
            return;
        }

        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void ApplyDestroyedState()
    {
        if (activeVisualRoot != null)
        {
            activeVisualRoot.SetActive(!isDestroyed);
        }

        if (destroyedVisualRoot != null)
        {
            destroyedVisualRoot.SetActive(isDestroyed);
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            ResolveTowerColliders();
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = !isDestroyed;
            }
        }
    }

    private void ClampHealthSettings(bool resetCurrentToMaxIfNeeded)
    {
        maxHp = Mathf.Max(1f, maxHp);
        if (resetCurrentToMaxIfNeeded)
        {
            currentHp = maxHp;
        }
        else
        {
            currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        }

    }

    private Transform FindTarget()
    {
        if (isDestroyed || currentHp <= 0f)
        {
            return null;
        }

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            Mathf.Max(0.01f, attackRange),
            targetLayerMask,
            QueryTriggerInteraction.Collide);

        float nearestDistanceSqr = float.MaxValue;
        Transform nearest = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Zombie zombie = hit.GetComponentInParent<Zombie>();
            if (zombie == null || zombie.IsDead || !zombie.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 zombiePos = zombie.transform.position;
            if (!IsWithinRange(zombiePos))
            {
                continue;
            }

            float distanceSqr = HorizontalDistanceSqr(transform.position, zombiePos);
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = zombie.transform;
            }
        }

        if (nearest != null)
        {
            return nearest;
        }

        return null;
    }

    private bool IsTargetValid(Transform target)
    {
        if (isDestroyed || currentHp <= 0f)
        {
            return false;
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Zombie zombie = target.GetComponentInParent<Zombie>();
        if (zombie == null || zombie.IsDead)
        {
            return false;
        }

        return IsWithinRange(target.position);
    }

    private bool IsWithinRange(Vector3 worldPosition)
    {
        float range = Mathf.Max(0.01f, attackRange);
        return HorizontalDistanceSqr(transform.position, worldPosition) <= range * range;
    }

    private static float HorizontalDistanceSqr(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return (to - from).sqrMagnitude;
    }

    private void RotateYawTowardTarget()
    {
        if (aimYawPivot == null || currentTarget == null)
        {
            return;
        }

        Vector3 direction = currentTarget.position - aimYawPivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion lookRot = Quaternion.LookRotation(direction);
        aimYawPivot.rotation = Quaternion.Euler(0f, lookRot.eulerAngles.y, 0f);
    }

    private Vector3 GetTargetAimPoint()
    {
        if (currentTarget == null)
        {
            return transform.position + Vector3.up * targetAimHeightOffset;
        }

        Collider collider = currentTarget.GetComponentInChildren<Collider>();
        if (collider != null)
        {
            return collider.bounds.center;
        }

        return currentTarget.position + Vector3.up * targetAimHeightOffset;
    }

    private void TryFireAtTarget()
    {
        if (isDestroyed || currentHp <= 0f)
        {
            ClearAttackState();
            return;
        }

        attackTimer += Time.deltaTime;

        if (attackTimer < Mathf.Max(0.05f, attackInterval))
        {
            return;
        }

        attackTimer = 0f;

        if (firePoint == null || projectileVisualPrefab == null || currentTarget == null)
        {
            return;
        }

        Vector3 targetAimPoint = GetTargetAimPoint();
        Vector3 rawFireDirection = targetAimPoint - firePoint.position;
        if (rawFireDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 fireDirection = rawFireDirection.normalized;
        Vector3 missEndPoint = firePoint.position + fireDirection * Mathf.Max(0.01f, attackRange);

        bool hasHit = TryResolveHitAtFireTime(
            firePoint.position,
            fireDirection,
            out Zombie hitZombie,
            out Vector3 hitPoint);

        Vector3 projectileEndPoint = missEndPoint;
        Zombie deferredTarget = null;
        float deferredDamage = 0f;
        GameObject deferredHitVfxPrefab = null;
        Vector3 deferredHitPoint = projectileEndPoint;
        if (hasHit && hitZombie != null && !hitZombie.IsDead)
        {
            projectileEndPoint = hitPoint;
            deferredTarget = hitZombie;
            deferredDamage = Mathf.Max(0f, damage);
            deferredHitVfxPrefab = hitVfxPrefab;
            deferredHitPoint = hitPoint;
        }

        GameObject projectileObject = Instantiate(
            projectileVisualPrefab,
            firePoint.position,
            Quaternion.identity);

        SimpleProjectile projectile = projectileObject.GetComponent<SimpleProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning($"[ArrowTower] projectileVisualPrefab '{projectileVisualPrefab.name}' has no SimpleProjectile component.", this);
            Destroy(projectileObject);
            return;
        }

        Vector3 projectileTravel = projectileEndPoint - firePoint.position;
        float projectileDistance = projectileTravel.magnitude;
        Vector3 projectileDirection = fireDirection;
        if (projectileDistance <= 0.001f)
        {
            projectileDistance = 0.05f;
        }
        else
        {
            projectileDirection = projectileTravel / projectileDistance;
        }

        projectile.InitializeDeferred(
            projectileDirection,
            projectileDistance,
            projectileEndPoint,
            deferredTarget,
            deferredDamage,
            deferredHitVfxPrefab,
            deferredHitPoint);
    }

    private bool TryResolveHitAtFireTime(
        Vector3 shotOrigin,
        Vector3 shotDirection,
        out Zombie resolvedZombie,
        out Vector3 resolvedHitPoint)
    {
        resolvedZombie = null;
        resolvedHitPoint = shotOrigin + shotDirection * Mathf.Max(0.01f, attackRange);

        float clampedRange = Mathf.Max(0.01f, attackRange);
        float clampedRadius = Mathf.Max(0.01f, hitRadius);
        Vector3 shotEnd = shotOrigin + shotDirection * clampedRange;

        int count = Physics.OverlapCapsuleNonAlloc(
            shotOrigin,
            shotEnd,
            clampedRadius,
            hitBuffer,
            targetLayerMask,
            QueryTriggerInteraction.Collide);

        if (count <= 0)
        {
            return false;
        }

        float bestLineDistanceSqr = float.MaxValue;
        float bestForwardDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            Zombie zombie = hit.GetComponentInParent<Zombie>();
            if (zombie == null || zombie.IsDead || !zombie.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 candidateCenter = hit.bounds.center;
            Vector3 toCandidate = candidateCenter - shotOrigin;
            float forwardDistance = Vector3.Dot(toCandidate, shotDirection);
            if (forwardDistance < 0f || forwardDistance > clampedRange)
            {
                continue;
            }

            Vector3 closestPointOnLine = shotOrigin + shotDirection * forwardDistance;
            Vector3 closestPointOnZombie = hit.ClosestPoint(closestPointOnLine);
            float lineDistanceSqr = (closestPointOnZombie - closestPointOnLine).sqrMagnitude;
            if (lineDistanceSqr > clampedRadius * clampedRadius)
            {
                continue;
            }

            bool isBetter = lineDistanceSqr < bestLineDistanceSqr ||
                (Mathf.Approximately(lineDistanceSqr, bestLineDistanceSqr) && forwardDistance < bestForwardDistance);

            if (!isBetter)
            {
                continue;
            }

            bestLineDistanceSqr = lineDistanceSqr;
            bestForwardDistance = forwardDistance;
            resolvedZombie = zombie;
            resolvedHitPoint = closestPointOnZombie;
        }

        return resolvedZombie != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, attackRange));

        if (firePoint == null || currentTarget == null)
        {
            return;
        }

        Vector3 toTarget = GetTargetAimPoint() - firePoint.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 direction = toTarget.normalized;
        float range = Mathf.Max(0.01f, attackRange);
        float radius = Mathf.Max(0.01f, hitRadius);
        Vector3 end = firePoint.position + direction * range;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(firePoint.position, radius);
        Gizmos.DrawWireSphere(end, radius);
        Gizmos.DrawLine(firePoint.position + Vector3.right * radius, end + Vector3.right * radius);
        Gizmos.DrawLine(firePoint.position - Vector3.right * radius, end - Vector3.right * radius);
        Gizmos.DrawLine(firePoint.position + Vector3.forward * radius, end + Vector3.forward * radius);
        Gizmos.DrawLine(firePoint.position - Vector3.forward * radius, end - Vector3.forward * radius);
    }
}
