using UnityEngine;

/// <summary>
/// Prototype input bridge for consuming stored food to restore player hunger.
/// </summary>
public class PlayerConsume : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ResourceManager resourceManager;

    [SerializeField]
    private HungerSystem hungerSystem;

    [Header("Consume Input")]
    [SerializeField]
    private KeyCode consumeKey = KeyCode.Q;

    [SerializeField]
    private float hungerRestoreAmount = 20f;

    [SerializeField]
    private float consumeCooldownSeconds = 0.5f;

    [Header("Debug")]
    [SerializeField]
    private bool logWhenNoFood = true;

    private float nextAllowedConsumeTime;

    private void Update()
    {
        if (!Input.GetKeyDown(consumeKey))
            return;

        if (Time.time < nextAllowedConsumeTime)
            return;

        if (resourceManager == null || hungerSystem == null)
        {
            Debug.LogWarning("PlayerConsume is missing ResourceManager or HungerSystem reference.");
            return;
        }

        if (!resourceManager.HasResource(ResourceType.Food, 1))
        {
            if (logWhenNoFood)
                Debug.Log("No food available to consume.");
            return;
        }

        if (!resourceManager.TrySpendResource(ResourceType.Food, 1))
            return;

        hungerSystem.RestoreHunger(hungerRestoreAmount);
        nextAllowedConsumeTime = Time.time + Mathf.Max(0f, consumeCooldownSeconds);
    }
}
