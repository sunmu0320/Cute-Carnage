using System.Collections.Generic;

public class BaseRuntimeState
{
    public int baseLevel;
    public string baseUpgradeId;
    public BaseCoreRuntimeState baseCore;
    public List<FenceRuntimeState> fences;
    public List<FenceSlotRuntimeState> fenceSlots;
    public List<TowerSlotRuntimeState> towerSlots;

    public BaseRuntimeState()
    {
        baseLevel = 1;
        baseUpgradeId = string.Empty;
        baseCore = new BaseCoreRuntimeState();
        fences = new List<FenceRuntimeState>();
        fenceSlots = new List<FenceSlotRuntimeState>();
        towerSlots = new List<TowerSlotRuntimeState>();
    }
}
