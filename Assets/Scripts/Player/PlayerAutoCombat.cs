using System.Collections.Generic;
using UnityEngine;

public class PlayerAutoCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private Transform combatLookRoot;
    [SerializeField] private SimpleProjectile projectileVisualPrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private LayerMask targetLayerMask = ~0;

    [Header("Targeting")]
    [SerializeField] private float targetRefreshInterval = 0.2f;
    [SerializeField] private float hitRadius = 0.25f;
    [SerializeField] private float missEndpointDistance = 0f;

    [Header("Attack Feedback (Prototype)")]
    [SerializeField] private Transform attackFeedbackRoot;
    [SerializeField] private float feedbackDistance = 0.05f;
    [SerializeField] private float feedbackDuration = 0.08f;
    [SerializeField] private bool logAttacksWhenNoFeedbackRoot = true;

    [Header("Shooting Animation")]
    [Tooltip("Body Animator with the upper-body shoot layer. Auto-found in children if left empty.")]
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private string shootLayerName = "UpperBodyShoot";
    [Tooltip("Layer weight change per second when a target enters/leaves range.")]
    [SerializeField] private float shootLayerBlendSpeed = 6f;

    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private int shootLayer = -1;

    private Zombie currentTarget;
private float targetRefreshTimer;
    private float nextAttackTime;

    private Vector3 feedbackStartLocalPosition;
    private bool feedbackInitialized;
    private bool isFeedbackAnimating;
    private float feedbackTimer;
    private readonly Collider[] hitBuffer = new Collider[64];

    private void Awake()
    {
        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (attackFeedbackRoot != null)
        {
            feedbackStartLocalPosition = attackFeedbackRoot.localPosition;
            feedbackInitialized = true;
        }

        if (bodyAnimator == null)
        {
            bodyAnimator = GetComponentInChildren<Animator>();
        }

        if (bodyAnimator != null)
        {
            shootLayer = bodyAnimator.GetLayerIndex(shootLayerName);
        }
    }

    private void OnDisable()
    {
        // GameOver / pause lock: drop the bow pose instead of freezing mid-draw.
        if (shootLayer >= 0 && bodyAnimator != null)
        {
            bodyAnimator.SetLayerWeight(shootLayer, 0f);
        }
    }

    /// <summary>Flat direction to the current target while one is in range (PlayerMovement turns toward it).</summary>
    public bool TryGetFacingDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!isActiveAndEnabled || !IsValidTarget(currentTarget))
        {
            return false;
        }

        direction = currentTarget.transform.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f;
    }

    /// <summary>Bow pose fades in while a target is in range and out when there is none.</summary>
    private void UpdateShootLayerWeight()
    {
        if (shootLayer < 0 || bodyAnimator == null)
        {
            return;
        }

        float goal = IsValidTarget(currentTarget) ? 1f : 0f;
        float weight = bodyAnimator.GetLayerWeight(shootLayer);
        if (weight != goal)
        {
            bodyAnimator.SetLayerWeight(shootLayer, Mathf.MoveTowards(weight, goal, shootLayerBlendSpeed * Time.deltaTime));
        }
    }

    private void Update()
    {
        WeaponData weaponData = GetCurrentWeaponData();
        if (weaponData == null)
        {
            currentTarget = null;
            UpdateAttackFeedback();
            return;
        }

        RefreshTargetIfNeeded(weaponData.attackRange);
        UpdateLookAtTarget();
        TryAttack(weaponData);
        UpdateShootLayerWeight();
        UpdateAttackFeedback();
    }

    private WeaponData GetCurrentWeaponData()
    {
        if (weaponController == null)
        {
            return null;
        }

        return weaponController.EquippedWeaponData;
    }

    private void RefreshTargetIfNeeded(float attackRange)
    {
        targetRefreshTimer -= Time.deltaTime;
        bool targetInvalid = !IsValidTarget(currentTarget);
        bool targetOutOfRange = !targetInvalid && !IsWithinRange(currentTarget, attackRange);

        if (targetInvalid || targetOutOfRange || targetRefreshTimer <= 0f)
        {
            currentTarget = FindNearestZombieInRange(attackRange);
            targetRefreshTimer = Mathf.Max(0.05f, targetRefreshInterval);
        }
    }

    private Zombie FindNearestZombieInRange(float attackRange)
    {
        IReadOnlyList<Zombie> zombies = Zombie.Active;
        if (zombies.Count == 0)
        {
            return null;
        }

        Vector3 myPos = transform.position;
        myPos.y = 0f;

        float attackRangeSqr = attackRange * attackRange;
        float nearestDistanceSqr = float.MaxValue;
        Zombie nearestZombie = null;

        for (int i = 0; i < zombies.Count; i++)
        {
            Zombie zombie = zombies[i];
            if (!IsValidTarget(zombie))
            {
                continue;
            }

            Vector3 zombiePos = zombie.transform.position;
            zombiePos.y = 0f;

            float distanceSqr = (zombiePos - myPos).sqrMagnitude;
            if (distanceSqr > attackRangeSqr)
            {
                continue;
            }

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestZombie = zombie;
            }
        }

        return nearestZombie;
    }

    private void TryAttack(WeaponData weaponData)
    {
        if (!CanAttack(weaponData))
        {
            return;
        }

        if (!TrySpawnProjectileAttack(weaponData))
        {
            return;
        }

        nextAttackTime = Time.time + Mathf.Max(0.05f, weaponData.attackInterval);
        if (shootLayer >= 0)
        {
            bodyAnimator.SetTrigger(ShootHash); // release (Recoil), then re-draw and hold until the next shot
        }

        SfxPlayer.Play(weaponData.releaseSounds, transform.position, weaponData.releaseVolume);
        TriggerAttackFeedback();

        if (!feedbackInitialized && logAttacksWhenNoFeedbackRoot)
        {
            Debug.Log($"[PlayerAutoCombat] Fired projectile at {currentTarget.name} for {weaponData.damage}", this);
        }
    }

    private bool TrySpawnProjectileAttack(WeaponData weaponData)
    {
        if (projectileVisualPrefab == null)
        {
            Debug.LogWarning("[PlayerAutoCombat] Missing projectileVisualPrefab reference.", this);
            return false;
        }

        if (!IsValidTarget(currentTarget))
        {
            return false;
        }

        Transform spawnRoot = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 spawnPosition = spawnRoot.position;

        Vector3 targetAimPoint = GetZombieAimPoint(currentTarget);
        Vector3 direction = targetAimPoint - spawnPosition;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = spawnRoot.forward;
        }

        Vector3 fireDirection = direction.normalized;
        float maxDistance = Mathf.Max(0.01f, weaponData.attackRange);
        float missDistance = missEndpointDistance > 0f ? missEndpointDistance : maxDistance;
        Vector3 missEndPoint = spawnPosition + fireDirection * missDistance;

        bool hasHit = TryResolveHitAtAttackTime(
            spawnPosition,
            fireDirection,
            maxDistance,
            out Zombie hitZombie,
            out Vector3 resolvedHitPoint);

        Vector3 projectileEndPoint = missEndPoint;
        Zombie deferredTarget = null;
        float deferredDamage = 0f;
GameObject deferredHitVfxPrefab = null;
        Vector3 deferredHitPoint = projectileEndPoint;
        if (hasHit && hitZombie != null && !hitZombie.IsDead)
        {
            projectileEndPoint = resolvedHitPoint;
            deferredTarget = hitZombie;
            deferredDamage = Mathf.Max(0f, weaponData.damage);
            deferredHitVfxPrefab = hitVfxPrefab;
            deferredHitPoint = resolvedHitPoint;
        }

        Vector3 projectileTravel = projectileEndPoint - spawnPosition;
        float projectileDistance = projectileTravel.magnitude;
        Vector3 projectileDirection = fireDirection;
        if (projectileDistance > 0.001f)
        {
            projectileDirection = projectileTravel / projectileDistance;
        }
        else
        {
            projectileDistance = 0.05f;
        }

        SimpleProjectile projectileInstance = Instantiate(
            projectileVisualPrefab,
            spawnPosition,
            Quaternion.LookRotation(projectileDirection, Vector3.up));

        if (projectileInstance == null)
        {
            Debug.LogWarning("[PlayerAutoCombat] Failed to instantiate projectile.", this);
            return false;
        }

        projectileInstance.InitializeDeferred(
            projectileDirection,
            projectileDistance,
            projectileEndPoint,
            deferredTarget,
            deferredDamage,
            deferredHitVfxPrefab,
            deferredHitPoint);
        return true;
    }

    private bool TryResolveHitAtAttackTime(
        Vector3 shotOrigin,
        Vector3 shotDirection,
        float maxDistance,
        out Zombie resolvedZombie,
        out Vector3 resolvedHitPoint)
    {
        resolvedZombie = null;
        float clampedDistance = Mathf.Max(0.01f, maxDistance);
        float clampedHitRadius = Mathf.Max(0.01f, hitRadius);
        Vector3 shotEnd = shotOrigin + shotDirection * clampedDistance;
        resolvedHitPoint = shotEnd;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            shotOrigin,
            shotEnd,
            clampedHitRadius,
            hitBuffer,
            targetLayerMask,
            QueryTriggerInteraction.Collide);

        if (hitCount <= 0)
        {
            return false;
        }

        float bestLineDistanceSqr = float.MaxValue;
        float bestForwardDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            Zombie zombie = hit.GetComponentInParent<Zombie>();
            if (!IsValidTarget(zombie))
            {
                continue;
            }

            Vector3 candidateCenter = hit.bounds.center;
            Vector3 toCandidate = candidateCenter - shotOrigin;
            float forwardDistance = Vector3.Dot(toCandidate, shotDirection);
            if (forwardDistance < 0f || forwardDistance > clampedDistance)
            {
                continue;
            }

            Vector3 closestPointOnLine = shotOrigin + shotDirection * forwardDistance;
            Vector3 closestPointOnZombie = hit.ClosestPoint(closestPointOnLine);
            float lineDistanceSqr = (closestPointOnZombie - closestPointOnLine).sqrMagnitude;
            if (lineDistanceSqr > clampedHitRadius * clampedHitRadius)
            {
                continue;
            }

            bool isBetterHit = lineDistanceSqr < bestLineDistanceSqr ||
                (Mathf.Approximately(lineDistanceSqr, bestLineDistanceSqr) && forwardDistance < bestForwardDistance);

            if (!isBetterHit)
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

    private Vector3 GetZombieAimPoint(Zombie zombie)
    {
        if (zombie == null)
        {
            return transform.position;
        }

        Collider zombieCollider = zombie.GetComponentInChildren<Collider>();
        if (zombieCollider != null)
        {
            return zombieCollider.bounds.center;
        }

        return zombie.transform.position;
    }

    private bool CanAttack(WeaponData weaponData)
    {
        if (weaponData == null || !IsValidTarget(currentTarget))
        {
            return false;
        }

        if (!IsWithinRange(currentTarget, weaponData.attackRange))
        {
            return false;
        }

        return Time.time >= nextAttackTime;
    }

    private bool IsValidTarget(Zombie zombie)
    {
        return zombie != null && !zombie.IsDead;
    }

    private bool IsWithinRange(Zombie zombie, float attackRange)
    {
if (!IsValidTarget(zombie))
        {
            return false;
        }

        Vector3 myPos = transform.position;
        myPos.y = 0f;

        Vector3 targetPos = zombie.transform.position;
        targetPos.y = 0f;

        return (targetPos - myPos).sqrMagnitude <= attackRange * attackRange;
    }

    private void UpdateLookAtTarget()
    {
        if (combatLookRoot == null || !IsValidTarget(currentTarget))
        {
            return;
        }

        Vector3 toTarget = currentTarget.transform.position - combatLookRoot.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        combatLookRoot.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
    }

    private void TriggerAttackFeedback()
    {
        if (attackFeedbackRoot == null)
        {
            return;
        }

        if (!feedbackInitialized)
        {
            feedbackStartLocalPosition = attackFeedbackRoot.localPosition;
            feedbackInitialized = true;
        }

        isFeedbackAnimating = true;
        feedbackTimer = 0f;
    }

    private void UpdateAttackFeedback()
    {
        if (!isFeedbackAnimating || attackFeedbackRoot == null)
        {
            return;
        }

        float safeDuration = Mathf.Max(0.02f, feedbackDuration);
        feedbackTimer += Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(feedbackTimer / safeDuration);
        float punchCurve = normalizedTime < 0.5f
            ? normalizedTime / 0.5f
            : (1f - normalizedTime) / 0.5f;

        Vector3 offset = Vector3.forward * Mathf.Max(0f, feedbackDistance) * punchCurve;
        attackFeedbackRoot.localPosition = feedbackStartLocalPosition + offset;

        if (normalizedTime >= 1f)
        {
            attackFeedbackRoot.localPosition = feedbackStartLocalPosition;
            isFeedbackAnimating = false;
        }
    }

    private float GetCurrentAttackRangeForDebug()
    {
        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
            if (weaponController == null)
            {
                return -1f;
            }
        }

        WeaponData weaponData = weaponController.EquippedWeaponData;
        if (weaponData == null || weaponData.attackRange <= 0f)
        {
            return -1f;
        }

        return weaponData.attackRange;
    }

    private void OnDrawGizmosSelected()
    {
        float attackRange = GetCurrentAttackRangeForDebug();
        if (attackRange <= 0f)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (currentTarget != null && !currentTarget.IsDead)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}
