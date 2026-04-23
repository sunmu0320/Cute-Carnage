public class TowerSlotRuntimeState
{
    public string id;
    public bool isUnlocked;
    public bool hasTower;
    public string towerTypeId;
    public int level;
    public string upgradeId;
    public float currentHp;
    public float maxHp;
    public float damage;
    public float attackRate;
    public float attackRange;
    public bool isDestroyed;

    public TowerSlotRuntimeState()
    {
        id = string.Empty;
        isUnlocked = true;
        hasTower = false;
        towerTypeId = string.Empty;
        level = 1;
        upgradeId = string.Empty;
        currentHp = 0f;
        maxHp = 0f;
        damage = 0f;
        attackRate = 0f;
        attackRange = 0f;
        isDestroyed = false;
    }
}
