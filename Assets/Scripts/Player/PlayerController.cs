using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person player controller.
/// Uses Unity's CharacterController for movement and physics,
/// and the New Input System keyboard and mouse devices for input.
///
/// Attach to the Player GameObject.
/// Assign the child Camera to the 'playerCamera' field.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector-configurable fields
    // -----------------------------------------------------------------------

    [Header("Movement")]
    [Tooltip("Walking speed in units per second.")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Jumping & Gravity")]
    [Tooltip("Initial vertical velocity applied when the player jumps.")]
    [SerializeField] private float jumpHeight = 1.5f;

    [Tooltip("Downward acceleration applied every second (positive value).")]
    [SerializeField] private float gravity = 20f;

    [Header("Mouse Look")]
    [Tooltip("Horizontal and vertical mouse sensitivity.")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    public float MouseSensitivity
    {
        get => mouseSensitivity;
        set => mouseSensitivity = Mathf.Clamp(value, 0.02f, 0.60f);
    }

    [Tooltip("Maximum angle (degrees) the camera can look up or down.")]
    [SerializeField] private float maxLookAngle = 89f;

    [Header("Audio")]
    [Tooltip("Time interval between footsteps while walking.")]
    [SerializeField] private float stepInterval = 0.42f;

    [Header("Player Dimensions & Camera")]
    public const float MinecraftEyeHeight = 1.76f;
    public const float MinecraftCharacterHeight = 1.90f;
    public const float MinecraftCharacterRadius = 0.30f;

    public float EyeHeight => MinecraftEyeHeight;
    public float CharacterHeight => MinecraftCharacterHeight;
    public float CharacterRadius => MinecraftCharacterRadius;

    public Camera PlayerCamera => playerCamera;

    public void SetCameraFOV(float fov)
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Clamp(fov, 60f, 100f);
        }
    }

    [Header("References")]
    [Tooltip("The first-person camera that is a child of this GameObject.")]
    [SerializeField] private Camera playerCamera;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private CharacterController _controller;
    private float _stepTimer;
    private VoxelWorld _world;

    // Accumulated vertical velocity (gravity + jump impulse)
    private float _verticalVelocity;

    // Current camera pitch (rotation around X-axis), clamped to maxLookAngle
    private float _cameraPitch;

    // Accumulated player yaw, allowing continuous rotation through 360 degrees
    private float _playerYaw;

    private bool _wasQuestOpen;
    private bool _skipNextMouseDelta;
    private bool _loggedGrounded;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        ApplyCharacterDimensions();
        _playerYaw = transform.localEulerAngles.y;

        if (GetComponent<PlayerHealth>() == null)
        {
            gameObject.AddComponent<PlayerHealth>();
        }

        if (GetComponent<PlayerExperience>() == null)
        {
            gameObject.AddComponent<PlayerExperience>();
        }

        if (GetComponent<QuestManager>() == null && FindFirstObjectByType<QuestManager>() == null)
        {
            gameObject.AddComponent<QuestManager>();
        }
    }

    private void Start()
    {
        ApplyCharacterDimensions();

        float savedFOV = PlayerPrefs.GetFloat("Voxel_CameraFOV", 80f);
        SetCameraFOV(savedFOV);
    }

    public void ApplyCharacterDimensions()
    {
        if (_controller == null)
        {
            _controller = GetComponent<CharacterController>();
        }

        if (_controller != null)
        {
            _controller.height = MinecraftCharacterHeight;
            _controller.center = new Vector3(0f, MinecraftCharacterHeight * 0.5f, 0f);
            _controller.radius = MinecraftCharacterRadius;
            _controller.skinWidth = 0.02f;
            _controller.minMoveDistance = 0.001f;
            _controller.stepOffset = 0.3f;
        }

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = new Vector3(0f, MinecraftEyeHeight, 0f);
        }
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        // 1. When game is paused or player is dead, completely freeze
        if (PauseMenuController.IsPaused || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead))
        {
            return;
        }

        // 2. When viewing quests, freeze all player controls and maintain stable vertical velocity
        if (QuestUI.IsOpen)
        {
            _wasQuestOpen = true;
            _verticalVelocity = 0f;
            _stepTimer = 0f;

            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        // 3. First frame after Quest panel closed: discard accumulated mouse delta and movement impulses
        if (_wasQuestOpen)
        {
            _wasQuestOpen = false;
            _skipNextMouseDelta = true;
            _verticalVelocity = -2f; // Stable grounded gravity
            return; // Skip this frame so accumulated mouse movement and button clicks are cleanly discarded
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;

        Vector2 lookDelta = Vector2.zero;
        if (mouse != null)
        {
            if (_skipNextMouseDelta)
            {
                _skipNextMouseDelta = false;
            }
            else
            {
                lookDelta = mouse.delta.ReadValue();
            }
        }

        HandleCursorLock(keyboard, mouse);
        HandleMouseLook(lookDelta, cursorWasLocked);
        HandleMovement(keyboard);
    }

    private void HandleCursorLock(Keyboard keyboard, Mouse mouse)
    {
        if (PauseMenuController.IsPaused || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) || QuestUI.IsOpen) return;

        // Note: Escape key pause/unpause is handled by PauseMenuController
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // -----------------------------------------------------------------------
    // Movement & Physics
    // -----------------------------------------------------------------------

    private void HandleMovement(Keyboard keyboard)
    {
        Vector2 moveInput = Vector2.zero;

        if (keyboard != null)
        {
            moveInput.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            moveInput.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        }

        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        // Horizontal movement relative to the player's current facing direction
        Vector3 move = (transform.right * moveInput.x) + (transform.forward * moveInput.y);
        move *= moveSpeed;

        // --- Gravity & Jump ---
        if (_controller.isGrounded)
        {
            // Small constant downward force keeps isGrounded stable on slopes without compressing into the block
            if (_verticalVelocity < 0f)
            {
                _verticalVelocity = -1.0f;
            }

            // Jump: v = sqrt(2 * g * h) gives a physically accurate jump arc
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                _verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
        }

        // Apply gravity every frame
        _verticalVelocity -= gravity * Time.deltaTime;

        move.y = _verticalVelocity;

        // CharacterController.Move handles collision resolution automatically
        _controller.Move(move * Time.deltaTime);

        if (_controller.isGrounded && !_loggedGrounded)
        {
            _loggedGrounded = true;
            Debug.Log($"[HEIGHT_DIAG_GROUNDED] Player transform.position={transform.position:F3}, Cam worldPos={(playerCamera != null ? playerCamera.transform.position.ToString("F3") : "null")}, Cam localPos={(playerCamera != null ? playerCamera.transform.localPosition.ToString("F3") : "null")}, diff={(playerCamera != null ? (playerCamera.transform.position.y - transform.position.y).ToString("F3") : "0")}");
        }

        // --- Footstep Audio ---
        if (_controller.isGrounded && moveInput.sqrMagnitude > 0.01f)
        {
            _stepTimer += Time.deltaTime;
            if (_stepTimer >= stepInterval)
            {
                _stepTimer = 0f;
                PlayFootstepAudio();
            }
        }
        else if (moveInput.sqrMagnitude <= 0.01f || !_controller.isGrounded)
        {
            // Reset timer when stopped or airborne — no phantom steps on landing
            _stepTimer = 0f;
        }
    }

    private void PlayFootstepAudio()
    {
        if (AudioManager.Instance == null) return;
        if (_world == null) _world = FindFirstObjectByType<VoxelWorld>();

        // Default to Grass when world is unavailable
        BlockType surfaceType = BlockType.Grass;

        if (_world != null)
        {
            Vector3 pos = transform.position;
            int gx = Mathf.FloorToInt(pos.x);
            int gz = Mathf.FloorToInt(pos.z);

            // Sample at foot level first, then one block lower.
            // This handles both standing on top of a block (foot = block surface)
            // and being inside a Water block.
            for (int dy = 0; dy <= 1; dy++)
            {
                int gy = Mathf.FloorToInt(pos.y - 0.15f) - dy;
                VoxelBlock block = _world.GetBlock(new Vector3Int(gx, gy, gz));
                if (block != null)
                {
                    surfaceType = block.Type;
                    break; // Use first (highest) solid block found
                }
            }
        }

        AudioManager.Instance.PlayFootstep(surfaceType, transform.position);
    }

    // -----------------------------------------------------------------------
    // Mouse Look
    // -----------------------------------------------------------------------

    private void HandleMouseLook(Vector2 lookDelta, bool cursorWasLocked)
    {
        if (playerCamera == null || !cursorWasLocked || Cursor.lockState != CursorLockMode.Locked) return;

        // Yaw: rotate only the player body around its Y axis.
        _playerYaw += lookDelta.x * mouseSensitivity;
        transform.localRotation = Quaternion.Euler(0f, _playerYaw, 0f);

        // Pitch: rotate only the child camera around its local X axis.
        _cameraPitch -= lookDelta.y * mouseSensitivity;
        float pitchLimit = Mathf.Clamp(maxLookAngle, 0f, 89f);
        _cameraPitch = Mathf.Clamp(_cameraPitch, -pitchLimit, pitchLimit);

        playerCamera.transform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    /// <summary>
    /// Resets player position, velocity, and camera angles (used for clean respawning).
    /// </summary>
    public void ResetToPosition(Vector3 newPosition)
    {
        _verticalVelocity = 0f;
        _stepTimer = 0f;
        _cameraPitch = 0f;

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.transform.localPosition = new Vector3(0f, MinecraftEyeHeight, 0f);
        }

        if (_controller != null)
        {
            _controller.enabled = false;
            transform.position = newPosition;
            _controller.enabled = true;
        }
        else
        {
            transform.position = newPosition;
        }
    }
}
