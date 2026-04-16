using UnityEngine;

public class PlayerAutoCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private Transform combatLookRoot;
    [SerializeField] private SimpleProjectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;

    [Header("Targeting")]
    [SerializeField] private float targetRefreshInterval = 0.2f;

    [Header("Attack Feedback (Prototype)")]
    [SerializeField] private Transform attackFeedbackRoot;
    [SerializeField] private float feedbackDistance = 0.05f;
    [SerializeField] private float feedbackDuration = 0.08f;
    [SerializeField] private bool logAttacksWhenNoFeedbackRoot = true;

    private BasicZombie currentTarget;
    private float targetRefreshTimer;
    private float nextAttackTime;

    private Vector3 feedbackStartLocalPosition;
    private bool feedbackInitialized;
    private bool isFeedbackAnimating;
    private float feedbackTimer;

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

    private BasicZombie FindNearestZombieInRange(float attackRange)
    {
        BasicZombie[] zombies = FindObjectsByType<BasicZombie>(FindObjectsSortMode.None);
        if (zombies == null || zombies.Length == 0)
        {
            return null;
        }

        Vector3 myPos = transform.position;
        myPos.y = 0f;

        float attackRangeSqr = attackRange * attackRange;
        float nearestDistanceSqr = float.MaxValue;
        BasicZombie nearestZombie = null;

        for (int i = 0; i < zombies.Length; i++)
        {
            BasicZombie zombie = zombies[i];
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
        TriggerAttackFeedback();

        if (!feedbackInitialized && logAttacksWhenNoFeedbackRoot)
        {
            Debug.Log($"[PlayerAutoCombat] Fired projectile at {currentTarget.name} for {weaponData.damage}", this);
        }
    }

    private bool TrySpawnProjectileAttack(WeaponData weaponData)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[PlayerAutoCombat] Missing projectilePrefab reference.", this);
            return false;
        }

        if (!IsValidTarget(currentTarget))
        {
            return false;
        }

        Transform spawnRoot = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 spawnPosition = spawnRoot.position;

        Vector3 targetPosition = currentTarget.transform.position;
        Vector3 direction = targetPosition - spawnPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = spawnRoot.forward;
        }

        SimpleProjectile projectileInstance = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction.normalized, Vector3.up));
        if (projectileInstance == null)
        {
            Debug.LogWarning("[PlayerAutoCombat] Failed to instantiate projectile.", this);
            return false;
        }

        projectileInstance.Initialize(direction, weaponData.damage, weaponData.attackRange);
        return true;
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

    private bool IsValidTarget(BasicZombie zombie)
    {
        return zombie != null && !zombie.IsDead;
    }

    private bool IsWithinRange(BasicZombie zombie, float attackRange)
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
