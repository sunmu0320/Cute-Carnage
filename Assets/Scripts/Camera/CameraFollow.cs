using UnityEngine;

/// <summary>
/// Top-down camera follow that ONLY follows the target's position.
/// It keeps the camera rotation fixed by re-applying the initial rotation every frame.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Assign the player root to follow.

    [Header("Follow Offset (world space)")]
    public Vector3 offset = new Vector3(0f, 1f, -10f);

    private Quaternion fixedRotation;

    void Awake()
    {
        // Store the camera's starting rotation.
        // Whatever rotation you set in the editor is what we will keep forever.
        fixedRotation = transform.rotation;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // Follow position only.
        transform.position = target.position + offset;

        // Force fixed rotation so player rotation cannot affect the camera.
        transform.rotation = fixedRotation;
    }
}

