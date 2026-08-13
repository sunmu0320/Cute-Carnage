using System.Collections.Generic;

public class BaseRuntimeState
{
    public int baseLevel;
    public string baseUpgradeId;
    public List<FenceSlotRuntimeState> fenceSlots;
    public List<TowerSlotRuntimeState> towerSlots;

    public BaseRuntimeState()
    {
        baseLevel = 1;
        baseUpgradeId = string.Empty;
        fenceSlots = new List<FenceSlotRuntimeState>();
        towerSlots = new List<TowerSlotRuntimeState>();
    }
}
