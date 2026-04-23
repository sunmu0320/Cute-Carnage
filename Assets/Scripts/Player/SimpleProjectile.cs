using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float arrivalThreshold = 0.05f;
    [SerializeField, Tooltip("Visual-only rotation offset for mesh axis alignment. Try (0, 0, 90) or (0, 90, 0).")]
    private Vector3 rotationOffsetEuler;

    private Vector3 moveDirection;
    private Vector3 startPosition;
    private Vector3 destinationPoint;
    private float maxTravelDistance;
    private bool isInitialized;
    private bool hasResolvedArrival;
    private BasicZombie deferredTarget;
    private float deferredDamage;
    private GameObject deferredHitVfxPrefab;
    private Vector3 deferredHitPoint;

    // Backward-compatible overload: damage now stays projectile-owned.
    public void Initialize(Vector3 direction, float _unusedDamageAmount, float travelDistance)
    {
        Initialize(direction, travelDistance);
    }

    public void Initialize(Vector3 direction, float travelDistance)
    {
        InitializeMovement(direction, travelDistance);
        destinationPoint = transform.position + moveDirection * maxTravelDistance;
        deferredTarget = null;
        deferredDamage = 0f;
        deferredHitVfxPrefab = null;
        deferredHitPoint = destinationPoint;
    }

    public void InitializeDeferred(
        Vector3 direction,
        float travelDistance,
        Vector3 destination,
        BasicZombie resolvedTarget,
        float damageAmount,
        GameObject hitVfxPrefab,
        Vector3 hitPoint)
    {
        InitializeMovement(direction, travelDistance);
        destinationPoint = destination;
        deferredTarget = resolvedTarget;
        deferredDamage = Mathf.Max(0f, damageAmount);
        deferredHitVfxPrefab = hitVfxPrefab;
        deferredHitPoint = hitPoint;
    }

    private void InitializeMovement(Vector3 direction, float travelDistance)
    {
        Vector3 safeDirection = direction;
        if (safeDirection.sqrMagnitude <= 0.0001f)
        {
            safeDirection = transform.forward;
        }

        moveDirection = safeDirection.normalized;
        Quaternion baseRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = baseRotation * Quaternion.Euler(rotationOffsetEuler);
        maxTravelDistance = Mathf.Max(0.05f, travelDistance);
        startPosition = transform.position;
        isInitialized = true;
        hasResolvedArrival = false;
    }

    private void Update()
    {
        if (!isInitialized || hasResolvedArrival)
        {
            return;
        }

        transform.position += moveDirection * Mathf.Max(0.01f, speed) * Time.deltaTime;
        float remainingDistance = Vector3.Distance(transform.position, destinationPoint);
        float traveledDistance = Vector3.Distance(startPosition, transform.position);
        if (remainingDistance <= Mathf.Max(0.001f, arrivalThreshold) || traveledDistance >= maxTravelDistance)
        {
            transform.position = destinationPoint;
            ResolveArrival();
        }
    }

    private void ResolveArrival()
    {
        if (hasResolvedArrival)
        {
            return;
        }

        hasResolvedArrival = true;

        bool canApplyDamage =
            deferredTarget != null &&
            deferredTarget.gameObject.activeInHierarchy &&
            !deferredTarget.IsDead &&
            deferredDamage > 0f;

        if (canApplyDamage)
        {
            deferredTarget.TakeDamage(deferredDamage);
            if (deferredHitVfxPrefab != null)
            {
                Instantiate(deferredHitVfxPrefab, deferredHitPoint, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }
}

