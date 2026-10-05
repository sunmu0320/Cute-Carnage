using System.Collections.Generic;

/// <summary>In-memory snapshot of everything GameManager.RetryCurrentDay() restores: captured at the start
/// of each Day (Start()/TransitionToDay()), restored when the player continues from GameOver. Not persisted
/// to disk - a same-session checkpoint only.</summary>
public class DayCheckpoint
{
    public float baseCoreHp;
    public ResourceRuntimeState resourceState = new ResourceRuntimeState();
    public float playerHp;
    public float playerHunger;
    public Dictionary<string, FenceCheckpoint> fenceSlots = new Dictionary<string, FenceCheckpoint>();
    public Dictionary<string, TowerCheckpoint> towerSlots = new Dictionary<string, TowerCheckpoint>();
    /// <summary>Depleted ResourceNodes (PersistentId -> respawnDay), captured after that Day's regrowth.</summary>
    public Dictionary<string, int> depletedResourceNodes = new Dictionary<string, int>();
}

public class FenceCheckpoint
{
    public bool hasFence;
    public FenceData fenceData;
    public float currentHp;
}

public class TowerCheckpoint
{
    public bool hasTower;
    public float currentHp;
}
