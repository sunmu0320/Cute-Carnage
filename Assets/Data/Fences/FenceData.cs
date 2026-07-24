using UnityEngine;

[CreateAssetMenu(fileName = "FenceData", menuName = "Cute Carnage/Fence Data")]
public class FenceData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("Stable identifier used to reference this fence in runtime save data. Do not change it after release.")]
    private string fenceId;

    [SerializeField, Tooltip("Display name shown to players.")]
    private string displayName;

    [SerializeField, Min(1), Tooltip("One-based tier number for this fence type.")]
    private int tierNumber = 1;

    [Header("Presentation")]
    [SerializeField, Tooltip("Icon used when this fence is shown in the UI.")]
    private Sprite uiIcon;

    [SerializeField, Tooltip("Prefab instantiated when this fence type is installed.")]
    private GameObject fencePrefab;

    [Header("Stats")]
    [SerializeField, Min(1f), Tooltip("Maximum hit points for this fence type.")]
    private float maxHp = 100f;

    [Header("Install")]
    [SerializeField, Min(0), Tooltip("Wood required to install this fence type.")]
    private int installWoodCost = 1;

    [SerializeField, Min(0), Tooltip("Scrap required to install this fence type.")]
    private int installScrapCost;

    [Header("Repair")]
    [SerializeField, Min(0f), Tooltip("Hit points restored by one repair action.")]
    private float repairAmount = 30f;

    [SerializeField, Min(0), Tooltip("Wood required for one repair action.")]
    private int repairWoodCost = 1;

    [SerializeField, Min(0), Tooltip("Scrap required for one repair action.")]
    private int repairScrapCost;

    [Header("Upgrade")]
    [SerializeField, Tooltip("Fence data installed by an upgrade. Leave null when this is the maximum tier.")]
    private FenceData nextFence;

    [SerializeField, Min(0), Tooltip("Wood required to upgrade to Next Fence.")]
    private int upgradeWoodCost;

    [SerializeField, Min(0), Tooltip("Scrap required to upgrade to Next Fence.")]
    private int upgradeScrapCost;

    public string FenceId => fenceId;
    public string DisplayName => displayName;
    public int TierNumber => tierNumber;
    public Sprite UiIcon => uiIcon;
    public GameObject FencePrefab => fencePrefab;
    public float MaxHp => maxHp;
    public int InstallWoodCost => installWoodCost;
    public int InstallScrapCost => installScrapCost;
    public float RepairAmount => repairAmount;
    public int RepairWoodCost => repairWoodCost;
    public int RepairScrapCost => repairScrapCost;
    public FenceData NextFence => nextFence;
    public int UpgradeWoodCost => upgradeWoodCost;
    public int UpgradeScrapCost => upgradeScrapCost;
    public bool IsMaximumTier => nextFence == null;
}
