using UnityEngine;

public enum ZombieType
{
    Basic,
    Runner,
    Breaker,
    Tank,
    Boss
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
    [Tooltip("Damage to fences is multiplied by this (Breaker = 3).")]
    [Min(0f)] public float fenceDamageMultiplier = 1f;

    [Header("Detection")]
    public float forwardDetectRange = 1.6f;
    public float forwardDetectRadius = 0.6f;

    [Header("Spawning")]
    [Tooltip("Prefab the spawner instantiates for this type; empty uses the spawner's default prefab.")]
    public GameObject prefab;

    [Header("Look")]
    [Tooltip("Uniform scale of the whole zombie (model and collider).")]
    [Min(0.1f)] public float bodyScale = 1f;
    [Tooltip("Scales how far hits push this zombie back (0 = never knocked back).")]
    [Min(0f)] public float knockbackMultiplier = 1f;
    [Tooltip("Multiplied into the model's base color; white = unchanged.")]
    public Color tint = Color.white;

    [Header("Animation")]
    public RuntimeAnimatorController animatorController;
    [Tooltip("Move speed (m/s) at which the walk clip plays at 1x (at bodyScale 1; scaled with it); the Walk state is sped up by moveSpeed / this.")]
    [Min(0.01f)] public float walkAnimReferenceSpeed = 0.65f;

    [Header("Audio")]
    [Tooltip("Played once when an emerging zombie starts its scream (random variant); empty = silent.")]
    public AudioClip[] emergeScreams;
    [Range(0f, 1f)] public float emergeScreamVolume = 0.8f;
    [Tooltip("Impact on every hit (slingshot, arrow, tower), including the killing one; empty = silent.")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitVolume = 0.6f;
    [Tooltip("Pain groans: a hit only groans when the shared cooldown (Zombie.GroanGapSeconds) has passed, so they come now and then, not every hit. Random variant; empty = silent.")]
    public AudioClip[] groanSounds;
    [Range(0f, 1f)] public float groanVolume = 0.7f;
    [Tooltip("Zombie dies; empty = silent.")]
    public AudioClip deathSound;
    [Range(0f, 1f)] public float deathVolume = 0.8f;
    [Tooltip("Zombie's attack lands on a fence/tower/player/base core (random variant); empty = silent.")]
    public AudioClip[] attackSounds;
    [Range(0f, 1f)] public float attackImpactVolume = 0.7f;
}
