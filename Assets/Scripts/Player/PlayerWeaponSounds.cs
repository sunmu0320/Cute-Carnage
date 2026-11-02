using UnityEngine;

/// <summary>Draw sound from the animation event on the bow draw clip (set in the FBX import settings); the clip
/// comes from the equipped WeaponData. Lives on the Animator's object so the event reaches it.</summary>
public class PlayerWeaponSounds : MonoBehaviour
{
    private PlayerWeaponController weaponController;

    private void Awake()
    {
        weaponController = GetComponentInParent<PlayerWeaponController>();
    }

    public void AnimationEvent_Draw()
    {
        WeaponData weapon = weaponController != null ? weaponController.EquippedWeaponData : null;
        if (weapon != null)
        {
            SfxPlayer.Play(weapon.drawSound, transform.position, weapon.drawVolume);
        }
    }
}
