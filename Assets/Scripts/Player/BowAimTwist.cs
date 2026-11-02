using UnityEngine;

/// <summary>
/// The longbow clips aim with the hips turned ~90° right; the shoot layer takes hips from walk/idle, so the bow
/// arm ends up pointing ~80° left. After the Animator poses the body, this twists the spine (around world up,
/// split over Spine/Chest/UpperChest) so the bow arm (left shoulder -> left hand) points along the character's
/// forward, which PlayerMovement keeps on the target. Scaled by the shoot layer's weight. Lives on the Animator.
/// </summary>
public class BowAimTwist : MonoBehaviour
{
    [SerializeField] private string shootLayerName = "UpperBodyShoot";
    [SerializeField, Range(0f, 180f)] private float maxTwist = 120f;
    [Tooltip("How fast the correction follows the pose (higher = stiffer, keeps less of the recoil sway).")]
    [SerializeField] private float followSpeed = 12f;

    private Animator animator;
    private int shootLayer = -1;
    private Transform[] spine;
    private Transform shoulder, hand, root;
    private float twist;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        shootLayer = animator.GetLayerIndex(shootLayerName);
        spine = System.Array.FindAll(new[]
        {
            animator.GetBoneTransform(HumanBodyBones.Spine),
            animator.GetBoneTransform(HumanBodyBones.Chest),
            animator.GetBoneTransform(HumanBodyBones.UpperChest),
        }, t => t != null);
        shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        root = animator.transform;
        if (shootLayer < 0 || spine.Length == 0 || shoulder == null || hand == null)
        {
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        float weight = animator.GetLayerWeight(shootLayer);
        if (weight <= 0f)
        {
            twist = 0f;
            return;
        }

        Vector3 arm = hand.position - shoulder.position;
        arm.y = 0f;
        float needed = arm.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(arm, root.forward, Vector3.up) : 0f;
        twist = Mathf.Lerp(twist, Mathf.Clamp(needed, -maxTwist, maxTwist), Mathf.Clamp01(followSpeed * Time.deltaTime));

        float perBone = twist * weight / spine.Length;
        foreach (Transform bone in spine)
        {
            bone.rotation = Quaternion.AngleAxis(perBone, Vector3.up) * bone.rotation;
        }
    }
}
