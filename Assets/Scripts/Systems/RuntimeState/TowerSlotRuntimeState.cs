public class TowerSlotRuntimeState
{
    public string id;
    public bool hasTower;
    public string towerId;
    public int level;
    public float currentHp;
    public bool isDestroyed;

    public TowerSlotRuntimeState()
    {
        id = string.Empty;
        hasTower = false;
        towerId = string.Empty;
        level = 1;
        currentHp = 0f;
        isDestroyed = false;
    }
}
