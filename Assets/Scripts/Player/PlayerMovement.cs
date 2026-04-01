using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public enum RotationMode
    {
        Smooth,
        Instant
    }

    [Header("Movement")]
    [Tooltip("Units per second the player moves.")]
    public float moveSpeed = 5f;
    [Tooltip("Multiplier applied to move speed while sprint key is held.")]
    public float sprintMultiplier = 1.5f;

    [Header("Rotation")]
    [Tooltip("How fast the player rotates toward the move direction.")]
    public float rotationSpeed = 10f;
    [Tooltip("Choose smooth interpolation or instant snap for yaw rotation.")]
    public RotationMode rotationMode = RotationMode.Smooth;

    [Header("Animation")]
    [Tooltip("Animator that receives the MoveBlend parameter. Auto-found in children if left empty.")]
    public Animator animator;
    [Tooltip("Animator float parameter used by the locomotion blend tree.")]
    public string moveBlendParameter = "MoveBlend";
    [Tooltip("Smoothing time for MoveBlend changes.")]
    public float moveBlendSmoothTime = 0.1f;
    [Tooltip("Blend value used when moving via keyboard.")]
    public float walkBlendValue = 0.5f;
    [Tooltip("Blend value used when running via keyboard.")]
    public float runBlendValue = 1f;
    [Tooltip("Hold this key while moving to request run blend on keyboard.")]
    public KeyCode runKey = KeyCode.LeftShift;

    float currentMoveBlend;
    float moveBlendVelocity;
    bool isMovementLocked;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isMovementLocked)
        {
            UpdateAnimation(Vector2.zero, false, false, false);
            return;
        }

        // Read input axes.
        // - "Horizontal" is typically mapped to A/D or Left/Right arrows.
        // - "Vertical" is typically mapped to W/S or Up/Down arrows.
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        // Convert the 2D input into a 3D direction on the XZ plane.
        // (We keep Y at 0 so the player doesn't move up/down.)
        Vector2 input = new Vector2(inputX, inputY);

        // Normalize so diagonal movement isn't faster than straight movement.
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        bool isMoving = moveDir.sqrMagnitude >= 0.0001f;

        bool keyboardMovePressed =
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
        bool runPressed = Input.GetKey(runKey);

        UpdateAnimation(input, isMoving, keyboardMovePressed, runPressed);

        // Keep root rotation constrained to yaw only (no forward/backward tilt or roll).
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        // If there's no input, do nothing.
        if (!isMoving)
            return;

        float currentSpeed = runPressed ? moveSpeed * sprintMultiplier : moveSpeed;
        Move(moveDir, currentSpeed);
        Rotate(moveDir);
    }

    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
    }

    void UpdateAnimation(Vector2 input, bool isMoving, bool keyboardMovePressed, bool runPressed)
    {
        float targetMoveBlend;
        if (!isMoving)
        {
            targetMoveBlend = 0f;
        }
        else if (keyboardMovePressed)
        {
            targetMoveBlend = runPressed ? runBlendValue : walkBlendValue;
        }
        else
        {
            // Joystick/analog movement maps proportionally from center (0) to full input (100).
            targetMoveBlend = Mathf.Clamp01(input.magnitude) * runBlendValue;
            if (runPressed)
                targetMoveBlend = runBlendValue;
        }

        currentMoveBlend = Mathf.SmoothDamp(currentMoveBlend, targetMoveBlend, ref moveBlendVelocity, moveBlendSmoothTime);
        if (animator != null && !string.IsNullOrEmpty(moveBlendParameter))
            animator.SetFloat(moveBlendParameter, currentMoveBlend);
    }

    void Move(Vector3 moveDir, float currentSpeed)
    {
        // Move using transform position (no Rigidbody / no CharacterController).
        transform.position += moveDir * currentSpeed * Time.deltaTime;
    }

    void Rotate(Vector3 moveDir)
    {
        // Rotate the player root toward movement using yaw only.
        // X/Z are intentionally locked so model orientation offset stays on CharacterVisual.
        Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
        Quaternion yawOnlyTarget = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);

        if (rotationMode == RotationMode.Instant)
        {
            transform.rotation = yawOnlyTarget;
        }
        else
        {
            // Smoothly interpolate current rotation toward the target yaw.
            float t = Mathf.Clamp01(rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, yawOnlyTarget, t);
        }
    }
}
