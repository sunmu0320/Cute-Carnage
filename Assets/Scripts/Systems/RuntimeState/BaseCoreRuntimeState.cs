public class BaseCoreRuntimeState
{
    public string id;
    public float currentHp;
    public float maxHp;
    public float defense;
    public bool isDestroyed;

    public BaseCoreRuntimeState()
    {
        id = string.Empty;
        currentHp = 0f;
        maxHp = 0f;
        defense = 0f;
        isDestroyed = false;
    }
}
