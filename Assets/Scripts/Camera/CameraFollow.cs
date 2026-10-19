using UnityEngine;

/// <summary>
/// Top-down camera follow that ONLY follows the target's position.
/// It keeps the camera rotation fixed by re-applying the initial rotation every frame.
/// Recording helper: freeCamKey toggles a free camera that still follows the target but lets the mouse
/// zoom (wheel), orbit (right drag) and pan (middle drag). Toggling off restores the fixed view.
/// Uses unscaled time/mouse deltas, so it also works while the pause menu holds timeScale at 0.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Assign the player root to follow.

    [Header("Follow Offset (world space)")]
    public Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("Free Camera (recording)")]
    [SerializeField] private KeyCode freeCamKey = KeyCode.F8;
    [SerializeField] private float zoomSpeed = 0.15f;
    [Tooltip("Seconds to ease toward the wheel's target distance (higher = smoother/slower).")]
    [SerializeField] private float zoomSmoothTime = 0.25f;
    [SerializeField] private float orbitSpeed = 0.25f;  // degrees per mouse pixel
    [SerializeField] private float panSpeed = 0.002f;   // world units per pixel per unit of distance
    [SerializeField] private Vector2 distanceRange = new Vector2(3f, 80f);

    private Quaternion fixedRotation;
    private bool freeCam;
    private Vector3 pivotOffset; // look-at point relative to the target
    private float distance, targetDistance, zoomVelocity, yaw, pitch;
    private Vector3 lastMouse;

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

        if (Input.GetKeyDown(freeCamKey))
            ToggleFreeCam();

        if (freeCam)
        {
            UpdateFreeCam();
            return;
        }

        // Follow position only.
        transform.position = target.position + offset;

        // Force fixed rotation so player rotation cannot affect the camera.
        transform.rotation = fixedRotation;
    }

    private void ToggleFreeCam()
    {
        freeCam = !freeCam;
        if (!freeCam)
            return;

        // Start from the current fixed view with no jump: orbit the point the camera is looking at.
        distance = targetDistance = Mathf.Clamp(offset.magnitude, distanceRange.x, distanceRange.y);
        zoomVelocity = 0f;
        pivotOffset = offset + fixedRotation * Vector3.forward * distance;
        Vector3 euler = fixedRotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        lastMouse = Input.mousePosition;
    }

    private void UpdateFreeCam()
    {
        Vector3 mouseDelta = Input.mousePosition - lastMouse;
        lastMouse = Input.mousePosition;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f)
            targetDistance = Mathf.Clamp(targetDistance * (1f - scroll * zoomSpeed), distanceRange.x, distanceRange.y);
        distance = Mathf.SmoothDamp(distance, targetDistance, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        if (Input.GetMouseButton(1))
        {
            yaw += mouseDelta.x * orbitSpeed;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * orbitSpeed, 5f, 89f);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        if (Input.GetMouseButton(2))
        {
            // Pan on the ground plane relative to the current view.
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            pivotOffset -= (right * mouseDelta.x + forward * mouseDelta.y) * panSpeed * distance;
        }

        Vector3 pivot = target.position + pivotOffset;
        transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
    }
}
