using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float damage = 5f;

    private Vector3 moveDirection;
    private Vector3 startPosition;
    private float maxTravelDistance;
    private bool isInitialized;

    public void Initialize(Vector3 direction, float damageAmount, float travelDistance)
    {
        Vector3 safeDirection = direction;
        safeDirection.y = 0f;
        if (safeDirection.sqrMagnitude <= 0.0001f)
        {
            safeDirection = transform.forward;
        }

        moveDirection = safeDirection.normalized;
        damage = Mathf.Max(0.01f, damageAmount);
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
        TryHitZombie(other != null ? other.GetComponentInParent<BasicZombie>() : null);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHitZombie(collision != null ? collision.collider.GetComponentInParent<BasicZombie>() : null);
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
