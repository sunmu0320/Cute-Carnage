using UnityEngine;

public class ArrowTower : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackInterval = 1f;
    [SerializeField] private float targetAimHeightOffset = 0.75f;

    [Header("References")]
    [SerializeField] private Transform aimYawPivot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private LayerMask targetLayerMask;

    private float attackTimer;
    private Transform currentTarget;

    private void Update()
    {
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

    private Transform FindTarget()
    {
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

            BasicZombie zombie = hit.GetComponentInParent<BasicZombie>();
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
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        BasicZombie zombie = target.GetComponentInParent<BasicZombie>();
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
        attackTimer += Time.deltaTime;

        if (attackTimer < Mathf.Max(0.05f, attackInterval))
        {
            return;
        }

        attackTimer = 0f;

        if (firePoint == null || projectilePrefab == null || currentTarget == null)
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

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.identity);

        SimpleProjectile projectile = projectileObject.GetComponent<SimpleProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning($"[ArrowTower] projectilePrefab '{projectilePrefab.name}' has no SimpleProjectile component.", this);
            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(fireDirection, attackRange);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, attackRange));
    }
}
