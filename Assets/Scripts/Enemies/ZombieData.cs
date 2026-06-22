using UnityEngine;

public enum ZombieType
{
    Basic,
    Runner,
    Breaker,
    Tank
}

public enum ZombieTargetPreference
{
    Normal,
    PreferStructures,
    PreferPlayer,
    PreferBaseCore
}

/// <summary>
/// Data-driven configuration for a zombie. One shared <see cref="Zombie"/> script reads its
/// stats/type from an assigned ZombieData asset, allowing multiple zombie types (Basic, Runner,
/// Breaker, Tank) without duplicating the behavior script.
/// </summary>
[CreateAssetMenu(fileName = "ZombieData", menuName = "Cute Carnage/Zombie Data")]
public class ZombieData : ScriptableObject
{
    [Header("Identity")]
    public string zombieId = "basic";
    public ZombieType zombieType = ZombieType.Basic;
    public ZombieTargetPreference targetPreference = ZombieTargetPreference.Normal;

    [Header("Stats")]
    public float maxHp = 30f;
    public float moveSpeed = 2f;
    public float attackDamage = 8f;
    public float attackRange = 1.4f;
    public float attackInterval = 1f;
    public float targetRefreshInterval = 1f;

    [Header("Detection")]
    public float forwardDetectRange = 1.6f;
    public float forwardDetectRadius = 0.6f;

    [Header("Animation (optional, used later)")]
    public RuntimeAnimatorController animatorController;
}
