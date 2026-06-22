using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
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

    private Rigidbody rb;
    private float currentMoveBlend;
    private float moveBlendVelocity;
    private bool isMovementLocked;

    // Cached physics input variables for FixedUpdate
    private Vector3 movementInput;
    private float movementSpeed;
    private bool hasMovementInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Ensure Rigidbody is set up correctly for physics movement
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isMovementLocked)
        {
            UpdateAnimation(Vector2.zero, false, false, false);
            movementInput = Vector3.zero;
            hasMovementInput = false;
            return;
        }

        // Read input axes.
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        // Convert the 2D input into a 3D direction on the XZ plane.
        Vector2 input = new Vector2(inputX, inputY);

        // Normalize so diagonal movement isn't faster than straight movement.
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        hasMovementInput = moveDir.sqrMagnitude >= 0.0001f;

        bool keyboardMovePressed =
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
        bool runPressed = Input.GetKey(runKey);

        UpdateAnimation(input, hasMovementInput, keyboardMovePressed, runPressed);

        if (hasMovementInput)
        {
            movementInput = moveDir;
            movementSpeed = runPressed ? moveSpeed * sprintMultiplier : moveSpeed;
            Rotate(moveDir);
        }
        else
        {
            movementInput = Vector3.zero;
        }
    }

    void FixedUpdate()
    {
        if (hasMovementInput && !isMovementLocked)
        {
            // Move using physics sweeps so walls block movement
            Vector3 targetPosition = rb.position + movementInput * movementSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
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
        if (targetMoveBlend == 0f)
            moveBlendVelocity = 0f;

        // Prevent tiny floating-point drift from making MoveBlend never fully reach 0.
        if (Mathf.Abs(currentMoveBlend) < 0.001f)
            currentMoveBlend = 0f;
        if (animator != null && !string.IsNullOrEmpty(moveBlendParameter))
            animator.SetFloat(moveBlendParameter, currentMoveBlend);
    }

    void Rotate(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f)
            return;

        // Rotate the player root toward movement using yaw only.
        Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
        Quaternion yawOnlyTarget = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);

        if (rotationMode == RotationMode.Instant)
        {
            transform.rotation = yawOnlyTarget;
            rb.rotation = yawOnlyTarget;
        }
        else
        {
            // Smoothly interpolate current transform rotation toward the target yaw.
            // Using transform.rotation ensures we update every rendering frame smoothly without stutter.
            float t = Mathf.Clamp01(rotationSpeed * Time.deltaTime);
            Quaternion newRotation = Quaternion.Slerp(transform.rotation, yawOnlyTarget, t);
            transform.rotation = newRotation;
            rb.rotation = newRotation; // Sync to Rigidbody immediately so physics and rendering are in step
        }
    }
}
