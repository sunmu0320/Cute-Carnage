using UnityEngine;

[CreateAssetMenu(fileName = "TowerData", menuName = "Cute Carnage/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("Stable identifier used to reference this tower in runtime save data. Do not change it after release.")]
    private string towerId;

    [SerializeField, Tooltip("Display name shown to players.")]
    private string displayName;

    [SerializeField, Min(1), Tooltip("One-based tier number for this tower type.")]
    private int tierNumber = 1;

    [Header("Presentation")]
    [SerializeField, Tooltip("Icon used when this tower is shown in the UI.")]
    private Sprite uiIcon;

    [SerializeField, Tooltip("Prefab instantiated when this tower type is built.")]
    private GameObject towerPrefab;

    [Header("Stats")]
    [SerializeField, Min(1f), Tooltip("Maximum hit points for this tower type.")]
    private float maxHp = 80f;

    [SerializeField, Min(0f), Tooltip("Damage dealt per hit.")]
    private float damage = 5f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between attacks.")]
    private float attackInterval = 1f;

    [SerializeField, Min(0.01f), Tooltip("Attack range in world units.")]
    private float attackRange = 6f;

    [Header("Install")]
    [SerializeField, Min(0), Tooltip("Wood required to build this tower type.")]
    private int installWoodCost = 1;

    [SerializeField, Min(0), Tooltip("Scrap required to build this tower type.")]
    private int installScrapCost = 1;

    [Header("Repair")]
    [SerializeField, Min(0f), Tooltip("Hit points restored by one repair action.")]
    private float repairAmount = 20f;

    [SerializeField, Min(0), Tooltip("Wood required for one repair action.")]
    private int repairWoodCost = 1;

    [SerializeField, Min(0), Tooltip("Scrap required for one repair action.")]
    private int repairScrapCost;

    [Header("Upgrade")]
    [SerializeField, Tooltip("Tower data installed by an upgrade. Leave null when this is the maximum tier.")]
    private TowerData nextTower;

    [SerializeField, Min(0), Tooltip("Wood required to upgrade to Next Tower.")]
    private int upgradeWoodCost;

    [SerializeField, Min(0), Tooltip("Scrap required to upgrade to Next Tower.")]
    private int upgradeScrapCost;

    public string TowerId => towerId;
    public string DisplayName => displayName;
    public int TierNumber => tierNumber;
    public Sprite UiIcon => uiIcon;
    public GameObject TowerPrefab => towerPrefab;
    public float MaxHp => maxHp;
    public float Damage => damage;
    public float AttackInterval => attackInterval;
    public float AttackRange => attackRange;
    public int InstallWoodCost => installWoodCost;
    public int InstallScrapCost => installScrapCost;
    public float RepairAmount => repairAmount;
    public int RepairWoodCost => repairWoodCost;
    public int RepairScrapCost => repairScrapCost;
    public TowerData NextTower => nextTower;
    public int UpgradeWoodCost => upgradeWoodCost;
    public int UpgradeScrapCost => upgradeScrapCost;
    public bool IsMaximumTier => nextTower == null;
}
