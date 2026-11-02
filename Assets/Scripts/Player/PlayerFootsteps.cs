using UnityEngine;

/// <summary>Footstep sounds from animation events on the Walking/Running clips (foot-contact frames, set in
/// the FBX import settings). Lives on the Animator's object so the events reach it.</summary>
public class PlayerFootsteps : MonoBehaviour
{
    [Tooltip("A random one plays every step; empty = silent.")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.35f;

    public void AnimationEvent_Footstep(AnimationEvent e)
    {
        // Walk and Run share a blend tree and both fire events; only the dominant clip counts.
        if (e.animatorClipInfo.weight > 0.5f)
        {
            SfxPlayer.Play(footstepClips, transform.position, footstepVolume, 0.05f);
        }
    }
}
