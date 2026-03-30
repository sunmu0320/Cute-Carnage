using UnityEngine;

/// <summary>
/// Attach to PlayerRoot. Other scripts can call <see cref="TakeDamage"/> or use UnityEvents in the Inspector.
/// Example: GetComponent&lt;PlayerHealth&gt;().TakeDamage(10);
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Maximum hit points.")]
    public int maxHealth = 100;

    [Tooltip("Current hit points (initialized from maxHealth in Awake).")]
    public int currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Reduces health by amount, clamped so it never goes below zero.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;

        currentHealth -= amount;
        if (currentHealth < 0)
            currentHealth = 0;
    }
}
