using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to PlayerRoot.
/// Other scripts should change HP through TakeDamage() / Heal().
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Maximum hit points.")]
    [SerializeField] private int maxHealth = 100;

    [Tooltip("Current hit points.")]
    [SerializeField] private int currentHealth = 100;

    [Header("Testing (Inspector)")]
    [Tooltip("When true, Awake() resets currentHealth to maxHealth.")]
    [SerializeField] private bool resetToMaxHealthOnAwake = true;

    [Tooltip("Enable keyboard damage test in Play Mode.")]
    [SerializeField] private bool enableDebugDamageKey = false;

    [Tooltip("Press this key in Play Mode to take debug damage.")]
    [SerializeField] private KeyCode debugDamageKey = KeyCode.F;

    [Tooltip("How much damage to take when pressing the debug key.")]
    [SerializeField] private int debugDamageAmount = 10;

    [Header("Events")]
    [Tooltip("Invoked whenever health changes.")]
    public UnityEvent<int, int> onHealthChanged;

    [Tooltip("Invoked when health reaches zero.")]
    public UnityEvent onDeath;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);

        if (resetToMaxHealthOnAwake)
        {
            currentHealth = maxHealth;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        NotifyHealthChanged();
    }

    private void Update()
    {
        if (!enableDebugDamageKey)
            return;

        if (Input.GetKeyDown(debugDamageKey))
        {
            TakeDamage(debugDamageAmount);
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        SetHealth(currentHealth - amount);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        SetHealth(currentHealth + amount);
    }

    public void SetMaxHealth(int newMaxHealth, bool refillCurrentHealth = false)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);

        if (refillCurrentHealth)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        NotifyHealthChanged();
    }

    private void SetHealth(int newHealth)
    {
        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);

        if (currentHealth == previousHealth)
            return;

        NotifyHealthChanged();

        if (currentHealth <= 0)
        {
            onDeath?.Invoke();
        }
    }

    private void NotifyHealthChanged()
    {
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}