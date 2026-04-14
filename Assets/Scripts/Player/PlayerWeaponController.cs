using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Where the equipped weapon prefab will be attached (player hand hold point).")]
    [SerializeField] private Transform weaponHoldPoint;

    [Header("Prototype State")]
    [Tooltip("Currently selected weapon data for prototype testing.")]
    [SerializeField] private WeaponData equippedWeaponData;

    [Header("Weapon Visual Offsets")]
    [Tooltip("Local position offset applied to the equipped weapon instance.")]
    [SerializeField] private Vector3 weaponLocalPositionOffset = Vector3.zero;

    [Tooltip("Local rotation offset (Euler angles) applied to the equipped weapon instance.")]
    [SerializeField] private Vector3 weaponLocalRotationOffset = Vector3.zero;

    [Tooltip("Local scale applied to the equipped weapon instance.")]
    [SerializeField] private Vector3 weaponLocalScale = Vector3.one;

    private GameObject equippedWeaponInstance;

    public WeaponData EquippedWeaponData => equippedWeaponData;

    private void Start()
    {
        // Prototype only: this will be replaced by proper day/night flow integration.
        if (weaponHoldPoint != null && equippedWeaponData != null)
        {
            EquipWeapon(equippedWeaponData);
        }
    }

    public void EquipWeapon(WeaponData newWeaponData)
    {
        if (equippedWeaponInstance != null)
        {
            Destroy(equippedWeaponInstance);
            equippedWeaponInstance = null;
        }

        equippedWeaponData = newWeaponData;

        if (weaponHoldPoint == null)
        {
            Debug.LogWarning($"{nameof(PlayerWeaponController)} is missing {nameof(weaponHoldPoint)} on {name}.");
            return;
        }

        if (newWeaponData == null)
        {
            Debug.LogWarning($"{nameof(PlayerWeaponController)} received a null {nameof(WeaponData)} on {name}.");
            return;
        }

        if (newWeaponData.weaponPrefab == null)
        {
            Debug.LogWarning($"{nameof(PlayerWeaponController)} missing {nameof(WeaponData.weaponPrefab)} for {newWeaponData.name}.");
            return;
        }

        equippedWeaponInstance = Instantiate(newWeaponData.weaponPrefab, weaponHoldPoint);
        ApplyVisualOffsets();
    }

    public void RefreshEquippedWeaponVisual()
    {
        EquipWeapon(equippedWeaponData);
    }

    private void ApplyVisualOffsets()
    {
        if (equippedWeaponInstance == null)
        {
            return;
        }

        Transform weaponTransform = equippedWeaponInstance.transform;
        weaponTransform.localPosition = weaponLocalPositionOffset;
        weaponTransform.localRotation = Quaternion.Euler(weaponLocalRotationOffset);
        weaponTransform.localScale = weaponLocalScale;
    }
}
