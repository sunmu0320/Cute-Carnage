public class FenceRuntimeState
{
    public string id;
    public int level;
    public string upgradeId;
    public float currentHp;
    public float maxHp;
    public float defense;
    public bool isDestroyed;
    public bool isUnlocked;

    public FenceRuntimeState()
    {
        id = string.Empty;
        level = 1;
        upgradeId = string.Empty;
        currentHp = 0f;
        maxHp = 0f;
        defense = 0f;
        isDestroyed = false;
        isUnlocked = true;
    }
}
