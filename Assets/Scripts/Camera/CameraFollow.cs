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

    [Header("Structure Hit Shake")]
    [Tooltip("Shake strength per hit (0-1 trauma) by what was hit; trauma adds up to 1.")]
    [SerializeField, Range(0f, 1f)] private float baseCoreShake = 0.6f;
    [SerializeField, Range(0f, 1f)] private float towerShake = 0.45f;
    [SerializeField, Range(0f, 1f)] private float fenceShake = 0.35f;
    [Tooltip("Hits within the first distance (m) from the target shake fully, beyond the second not at all.")]
    [SerializeField] private Vector2 shakeDistance = new Vector2(10f, 30f);
    [Tooltip("One structure shakes the camera at most once per this many seconds, however many zombies hit it.")]
    [SerializeField] private float shakeCooldownPerStructure = 0.5f;
    [Tooltip("Camera offset (m) at full trauma; the actual offset is trauma squared times this.")]
    [SerializeField] private float shakeMaxOffset = 0.25f;
    [Tooltip("Trauma lost per second (3 = a 0.6 hit fades in 0.2 s).")]
    [SerializeField] private float shakeDecayPerSecond = 3f;
    [SerializeField] private float shakeFrequency = 25f;

    /// <summary>Settings toggle; read from PlayerPrefs on Awake, set by SettingsPanelUI.</summary>
    public static bool ShakeEnabled = true;

    private float trauma;
    private readonly System.Collections.Generic.Dictionary<int, float> nextShakeTime = new System.Collections.Generic.Dictionary<int, float>();

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
        ShakeEnabled = SettingsPanelUI.SavedScreenShake;
    }

    void OnEnable() => StructureDamageEvents.Damaged += OnStructureDamaged;
    void OnDisable() => StructureDamageEvents.Damaged -= OnStructureDamaged;

    private void OnStructureDamaged(Component structure)
    {
        if (!ShakeEnabled || target == null || structure == null)
            return;

        int id = structure.GetInstanceID();
        if (nextShakeTime.TryGetValue(id, out float next) && Time.time < next)
            return;
        nextShakeTime[id] = Time.time + shakeCooldownPerStructure;

        float strength = structure is BaseCore ? baseCoreShake : structure is ArrowTower ? towerShake : fenceShake;
        float distance = Vector3.Distance(structure.transform.position, target.position);
        strength *= 1f - Mathf.InverseLerp(shakeDistance.x, shakeDistance.y, distance);
        trauma = Mathf.Min(1f, trauma + strength);
    }

    // Decorative offset while trauma lasts (rule 6 exception like the HUD clouds: no state polling).
    private Vector3 ShakeOffset()
    {
        if (trauma <= 0f || Time.timeScale == 0f)
            return Vector3.zero;

        trauma = Mathf.Max(0f, trauma - shakeDecayPerSecond * Time.deltaTime);
        float t = Time.time * shakeFrequency;
        float amount = trauma * trauma * shakeMaxOffset;
        return fixedRotation * new Vector3(
            (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * amount,
            (Mathf.PerlinNoise(0f, t) * 2f - 1f) * amount,
            0f);
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
        transform.position = target.position + offset + ShakeOffset();

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
