using UnityEngine;

/// <summary>
/// Prototype hunger drain + starvation damage. Attach on the player (e.g. PlayerRoot).
/// Optional <see cref="playerHealth"/> for damage when hunger is empty.
/// Food / HUD can hook in later via <see cref="RestoreHunger"/> and public read-only state.
/// </summary>
public class HungerSystem : MonoBehaviour
{
    [Header("Hunger")]
    [Tooltip("Maximum hunger value.")]
    [SerializeField]
    private float maxHunger = 100f;

    [Tooltip("Hunger when the scene loads and after ResetToStartingHunger().")]
    [SerializeField]
    private float startingHunger = 100f;

    [Tooltip("Hunger lost per second (real time).")]
    [SerializeField]
    private float hungerDrainPerSecond = 1f;

    [Header("Starvation")]
    [Tooltip("If assigned, applies damage via PlayerHealth.TakeDamage while hunger is empty.")]
    [SerializeField]
    private PlayerHealth playerHealth;

    [Tooltip("HP lost per second while hunger is 0. Converted to whole HP over time (no per-frame spam).")]
    [SerializeField]
    private float healthDamagePerSecondWhenStarving = 5f;

    [Header("Runtime")]
    [Tooltip("When false, hunger does not drain and starvation damage is not applied.")]
    [SerializeField]
    private bool isActive = true;

    private float currentHunger;
    private float starvationDamageAccumulator;

    public float MaxHunger => Mathf.Max(0.0001f, maxHunger);
    public float CurrentHunger => Mathf.Clamp(currentHunger, 0f, MaxHunger);
    public float NormalizedHunger => MaxHunger <= 0f ? 0f : CurrentHunger / MaxHunger;
    public bool IsHungerEmpty => CurrentHunger <= 0f;

    private void Awake()
    {
        maxHunger = Mathf.Max(0.0001f, maxHunger);
        startingHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
        currentHunger = startingHunger;
        starvationDamageAccumulator = 0f;
    }

    private void Update()
    {
        if (!isActive)
            return;

        if (hungerDrainPerSecond > 0f)
            currentHunger -= hungerDrainPerSecond * Time.deltaTime;

        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);

        if (!IsHungerEmpty || healthDamagePerSecondWhenStarving <= 0f)
        {
            starvationDamageAccumulator = 0f;
            return;
        }

        if (playerHealth == null || playerHealth.IsDead)
            return;

        starvationDamageAccumulator += healthDamagePerSecondWhenStarving * Time.deltaTime;
        int wholeDamage = Mathf.FloorToInt(starvationDamageAccumulator);
        if (wholeDamage <= 0)
            return;

        playerHealth.TakeDamage(wholeDamage);
        starvationDamageAccumulator -= wholeDamage;
    }

    /// <summary>Increase hunger by amount (e.g. eating). Clamps to max.</summary>
    public void RestoreHunger(float amount)
    {
        if (amount <= 0f)
            return;

        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
    }

    /// <summary>Decrease hunger by amount (e.g. exertion). Clamps to 0.</summary>
    public void ReduceHunger(float amount)
    {
        if (amount <= 0f)
            return;

        currentHunger = Mathf.Clamp(currentHunger - amount, 0f, maxHunger);
    }

    public void ResetToStartingHunger()
    {
        currentHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
        starvationDamageAccumulator = 0f;
    }

    public void ResetToMaxHunger()
    {
        currentHunger = maxHunger;
        starvationDamageAccumulator = 0f;
    }

    public void SetActiveDrain(bool active)
    {
        isActive = active;
        if (!isActive)
            starvationDamageAccumulator = 0f;
    }
}
