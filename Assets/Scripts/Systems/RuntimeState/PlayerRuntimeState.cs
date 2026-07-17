[System.Serializable]
public class PlayerRuntimeState
{
    public float currentHp = 100f;
    public float currentHunger = 100f;

    /// <summary>
    /// Reads live components when present. Uses <paramref name="previous"/> for fields when a component is missing.
    /// Returns null only when both components are null and <paramref name="previous"/> is null.
    /// </summary>
    public static PlayerRuntimeState FromScene(PlayerHealth health, HungerSystem hunger, PlayerRuntimeState previous = null)
    {
        if (health == null && hunger == null && previous == null)
        {
            return null;
        }

        PlayerRuntimeState state = new PlayerRuntimeState();

        if (health != null)
        {
            state.currentHp = health.CurrentHealth;
        }
        else if (previous != null)
        {
            state.currentHp = previous.currentHp;
        }

        if (hunger != null)
        {
            state.currentHunger = hunger.CurrentHunger;
        }
        else if (previous != null)
        {
            state.currentHunger = previous.currentHunger;
        }

        return state;
    }
}
