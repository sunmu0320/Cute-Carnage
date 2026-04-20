using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    // Projectile owns damage tuning (set in projectile prefab/inspector).
    [SerializeField] private float damage = 5f;
    [SerializeField, Tooltip("Visual-only rotation offset for mesh axis alignment. Try (0, 0, 90) or (0, 90, 0).")]
    private Vector3 rotationOffsetEuler;

    private Vector3 moveDirection;
    private Vector3 startPosition;
    private float maxTravelDistance;
    private bool isInitialized;
    private int zombieLayer = -1;

    private void Awake()
    {
        zombieLayer = LayerMask.NameToLayer("Zombie");
    }

    // Backward-compatible overload: damage now stays projectile-owned.
    public void Initialize(Vector3 direction, float _unusedDamageAmount, float travelDistance)
    {
        Initialize(direction, travelDistance);
    }

    public void Initialize(Vector3 direction, float travelDistance)
    {
        InitializeMovement(direction, travelDistance);
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
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        transform.position += moveDirection * Mathf.Max(0.01f, speed) * Time.deltaTime;

        float traveledDistance = Vector3.Distance(startPosition, transform.position);
        if (traveledDistance >= maxTravelDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitCollider(other);
    }

    private void TryHitCollider(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return;
        }

        if (zombieLayer >= 0 && hitCollider.gameObject.layer != zombieLayer)
        {
            return;
        }

        BasicZombie zombie = hitCollider.GetComponentInParent<BasicZombie>();
        if (zombie == null)
        {
            return;
        }

        TryHitZombie(zombie);
    }

    private void TryHitZombie(BasicZombie zombie)
    {
        if (zombie == null || zombie.IsDead)
        {
            return;
        }

        zombie.TakeDamage(damage);
        Destroy(gameObject);
    }
}
