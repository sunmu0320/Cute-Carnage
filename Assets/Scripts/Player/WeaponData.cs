using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "CuteCarnage/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id used to reference this weapon in systems later.")]
    public string weaponId;

    [Tooltip("Display name shown to players.")]
    public string weaponDisplayName;

    [Header("Visual")]
    [Tooltip("Prefab instantiated at the player's WeaponHoldPoint.")]
    public GameObject weaponPrefab;

    [Header("Prototype Combat Stats")]
    [Tooltip("Distance this weapon can reach.")]
    public float attackRange = 2f;

    [Tooltip("Time between attacks in seconds.")]
    public float attackInterval = 1f;

    [Tooltip("Base damage per hit.")]
    public float damage = 10f;
}
