using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the first-person arm, hand, and equipped item (Axe / Blocks).
/// Configured for a subtle 10-15% screen presence in the lower-right corner,
/// clear crosshair visibility, natural hand grip, smooth movement bobbing, and a
/// convincing two-phase chop swing animation (fast strike, smooth return).
///
/// Swing Animation Design:
///   Phase 1 - Strike  (~0.12 s): _viewRoot moves forward-down and rotates downward quickly.
///   Phase 2 - Recover (~0.25 s): _viewRoot smoothly lerps back to original resting pose.
/// The hand and axe move together because they both live under _viewRoot.
/// Bob is applied additively on top of the swing so the two motions never conflict.
/// </summary>
public class EquippedItemController : MonoBehaviour
{
    [SerializeField] private GameObject axePrefab;

    // -----------------------------------------------------------------------
    // Hierarchy references
    // -----------------------------------------------------------------------
    private Transform  _viewRoot;       // FirstPersonView - the swing target
    private Transform  _heldItemRoot;
    private GameObject _heldObject;

    // -----------------------------------------------------------------------
    // Materials
    // -----------------------------------------------------------------------
    private Material _handMaterial;
    private Material _woodMaterial;
    private Material _metalMaterial;
    private Material _blockMaterial;

    // -----------------------------------------------------------------------
    // Resting pose (captured once after CreateHandHierarchy)
    // -----------------------------------------------------------------------
    private Vector3    _restLocalPosition;
    private Quaternion _restLocalRotation;

    // -----------------------------------------------------------------------
    // Swing animation state machine
    // -----------------------------------------------------------------------
    private enum SwingPhase { Idle, Striking, Recovering }
    private SwingPhase _swingPhase = SwingPhase.Idle;
    private float      _swingTimer;        // counts UP from 0

    private const float StrikeDuration  = 0.12f;
    private const float RecoverDuration = 0.25f;

    // Strike offsets applied on top of rest pose  (forward-down chop)
    private static readonly Vector3    SwingPositionOffset = new Vector3(-0.06f, -0.10f, 0.08f);
    private static readonly Quaternion SwingRotationOffset = Quaternion.Euler(38f, 8f, -6f);

    // Current animated values (lerped each frame)
    private Vector3    _animLocalPosition;
    private Quaternion _animLocalRotation;

    // -----------------------------------------------------------------------
    // Idle bob (accumulated phase so it is not affected by pause)
    // -----------------------------------------------------------------------
    private float _bobPhase;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        CreateHandHierarchy();
        CreateMaterials();
        EquipAxe();

        // Capture resting pose right after hierarchy is built
        _restLocalPosition = _viewRoot.localPosition;
        _restLocalRotation = _viewRoot.localRotation;

        // Start animation values at rest
        _animLocalPosition = _restLocalPosition;
        _animLocalRotation = _restLocalRotation;
    }

    private void OnDestroy()
    {
        if (_handMaterial  != null) Destroy(_handMaterial);
        if (_woodMaterial  != null) Destroy(_woodMaterial);
        if (_metalMaterial != null) Destroy(_metalMaterial);
        if (_blockMaterial != null) Destroy(_blockMaterial);
    }

    private void Update()
    {
        // Freeze arm at rest when paused, dead, or viewing quests - never leave mid-swing
        if (PauseMenuController.IsPaused || Time.timeScale <= 0f ||
            (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) || QuestUI.IsOpen)
        {
            _swingPhase        = SwingPhase.Idle;
            _swingTimer        = 0f;
            _animLocalPosition = _restLocalPosition;
            _animLocalRotation = _restLocalRotation;
            if (_viewRoot != null)
            {
                _viewRoot.localPosition = _restLocalPosition;
                _viewRoot.localRotation = _restLocalRotation;
            }
            return;
        }

        UpdateSwingAnimation();
        UpdateBobAndApplyPose();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Triggers one full chop swing. Safe to call while already swinging -
    /// restarts the strike phase cleanly so the arm never drifts.
    /// </summary>
    public void TriggerSwing()
    {
        _swingPhase = SwingPhase.Striking;
        _swingTimer = 0f;
    }

    public void EquipAxe()
    {
        ReplaceEquippedObject();

        if (axePrefab != null)
        {
            _heldObject = Instantiate(axePrefab, _heldItemRoot);
            _heldObject.name = "FirstPersonAxe";
            _heldObject.transform.localPosition = Vector3.zero;
            _heldObject.transform.localRotation = Quaternion.identity;
            _heldObject.transform.localScale    = Vector3.one * 1.25f;

            ApplyAxeMaterials(_heldObject.transform);
            return;
        }

        Debug.LogError("EquippedItemController needs Axe.prefab assigned.", this);
    }

    public void EquipBlock(BlockType blockType)
    {
        ReplaceEquippedObject();

        _heldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _heldObject.name = blockType + " Held Block";
        _heldObject.transform.SetParent(_heldItemRoot, false);
        _heldObject.transform.localPosition = new Vector3(0f, 0.06f, 0.02f);
        _heldObject.transform.localRotation = Quaternion.Euler(15f, -20f, 10f);
        _heldObject.transform.localScale    = Vector3.one * 0.35f;

        Destroy(_heldObject.GetComponent<Collider>());

        Color color = GetBlockColor(blockType);
        _blockMaterial.SetColor("_BaseColor", color);
        _heldObject.GetComponent<Renderer>().sharedMaterial = _blockMaterial;
    }

    // -----------------------------------------------------------------------
    // Swing state machine
    // -----------------------------------------------------------------------

    private void UpdateSwingAnimation()
    {
        switch (_swingPhase)
        {
            case SwingPhase.Idle:
                // Gently drift back to exact rest in case of floating-point accumulation
                _animLocalPosition = Vector3.Lerp(_animLocalPosition, _restLocalPosition, Time.deltaTime * 18f);
                _animLocalRotation = Quaternion.Slerp(_animLocalRotation, _restLocalRotation, Time.deltaTime * 18f);
                break;

            case SwingPhase.Striking:
            {
                _swingTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_swingTimer / StrikeDuration);

                // Ease-in then ease-out for a snappy-but-smooth strike
                float tEased = t < 0.5f
                    ? 2f * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

                Vector3    strikePos = _restLocalPosition + SwingPositionOffset;
                Quaternion strikeRot = _restLocalRotation * SwingRotationOffset;

                _animLocalPosition = Vector3.Lerp(_restLocalPosition, strikePos, tEased);
                _animLocalRotation = Quaternion.Slerp(_restLocalRotation, strikeRot, tEased);

                if (_swingTimer >= StrikeDuration)
                {
                    // Snap exactly to full strike so recovery starts clean
                    _animLocalPosition = strikePos;
                    _animLocalRotation = strikeRot;
                    _swingPhase = SwingPhase.Recovering;
                    _swingTimer = 0f;
                }
                break;
            }

            case SwingPhase.Recovering:
            {
                _swingTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_swingTimer / RecoverDuration);

                // Cubic ease-out for a natural deceleration back to rest
                float tEased = 1f - Mathf.Pow(1f - t, 3f);

                Vector3    strikePos = _restLocalPosition + SwingPositionOffset;
                Quaternion strikeRot = _restLocalRotation * SwingRotationOffset;

                _animLocalPosition = Vector3.Lerp(strikePos, _restLocalPosition, tEased);
                _animLocalRotation = Quaternion.Slerp(strikeRot, _restLocalRotation, tEased);

                if (_swingTimer >= RecoverDuration)
                {
                    _animLocalPosition = _restLocalPosition;
                    _animLocalRotation = _restLocalRotation;
                    _swingPhase        = SwingPhase.Idle;
                    _swingTimer        = 0f;
                }
                break;
            }
        }
    }

    /// <summary>
    /// Computes the idle bob offset and writes the combined (animation + bob) pose to _viewRoot.
    /// </summary>
    private void UpdateBobAndApplyPose()
    {
        Keyboard keyboard = Keyboard.current;
        bool moving = keyboard != null &&
                      (keyboard.wKey.isPressed || keyboard.aKey.isPressed ||
                       keyboard.sKey.isPressed || keyboard.dKey.isPressed);

        float bobAmount = moving ? 0.012f : 0.003f;
        float bobSpeed  = moving ? 6.5f   : 1.5f;

        _bobPhase += Time.deltaTime * bobSpeed;

        float bobY = Mathf.Sin(_bobPhase) * bobAmount;
        float bobX = Mathf.Cos(_bobPhase * 0.5f) * (bobAmount * 0.4f);
        float bobZ = Mathf.Abs(bobY) * 0.15f;

        if (_viewRoot != null)
        {
            _viewRoot.localPosition = _animLocalPosition + new Vector3(bobX, bobY, bobZ);
            _viewRoot.localRotation = _animLocalRotation;
        }
    }

    // -----------------------------------------------------------------------
    // Hierarchy construction (unchanged from original)
    // -----------------------------------------------------------------------

    private void CreateHandHierarchy()
    {
        // 1. Root Container under PlayerCamera - lower-right quadrant with clean Minecraft proportions
        GameObject viewObj = new GameObject("FirstPersonView");
        _viewRoot = viewObj.transform;
        _viewRoot.SetParent(transform, false);

        _viewRoot.localPosition = new Vector3(0.26f, -0.24f, 0.42f);
        _viewRoot.localRotation = Quaternion.identity;

        // 2. Upper Arm (enters smoothly from bottom-right corner)
        GameObject armObj = new GameObject("Arm");
        Transform arm = armObj.transform;
        arm.SetParent(_viewRoot, false);
        arm.localPosition = new Vector3(0.12f, -0.08f, -0.06f);
        arm.localRotation = Quaternion.Euler(-24f, -10f, 18f);

        CreateArmMeshPart("UpperArmMesh", arm, new Vector3(0f, -0.02f, 0.08f), new Vector3(0.18f, 0.18f, 0.28f));

        // 3. Forearm (sturdy voxel forearm matching Minecraft block proportions)
        GameObject forearmObj = new GameObject("Forearm");
        Transform forearm = forearmObj.transform;
        forearm.SetParent(arm, false);
        forearm.localPosition = new Vector3(0f, -0.05f, 0.20f);
        forearm.localRotation = Quaternion.Euler(20f, 10f, -8f);

        CreateArmMeshPart("ForearmMesh", forearm, new Vector3(0f, 0.015f, 0.10f), new Vector3(0.16f, 0.16f, 0.26f));

        // 4. Hand (solid voxel hand grasping handle)
        GameObject handObj = new GameObject("Hand");
        Transform hand = handObj.transform;
        hand.SetParent(forearm, false);
        hand.localPosition = new Vector3(0f, 0.02f, 0.22f);
        hand.localRotation = Quaternion.Euler(14f, -16f, 20f);

        // Palm block centered on handle grip
        CreateArmMeshPart("HandPalm",    hand, Vector3.zero,                         new Vector3(0.16f, 0.15f, 0.16f));
        // Thumb wrap
        CreateArmMeshPart("HandThumb",   hand, new Vector3(-0.06f, 0.02f, 0.015f),  new Vector3(0.05f, 0.08f, 0.09f));
        // Fingers wrap
        CreateArmMeshPart("HandFingers", hand, new Vector3(0.05f,  0.02f, 0.04f),   new Vector3(0.06f, 0.07f, 0.10f));

        // 5. HeldItem Pivot (attached at the center of the palm)
        GameObject heldItemObj = new GameObject("HeldItem");
        _heldItemRoot = heldItemObj.transform;
        _heldItemRoot.SetParent(hand, false);
        _heldItemRoot.localPosition = new Vector3(0f, 0.01f, 0.02f);
        _heldItemRoot.localRotation = Quaternion.Euler(10f, 20f, -38f);
    }

    private void CreateArmMeshPart(string partName, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale    = localScale;
        Destroy(part.GetComponent<Collider>());
    }

    // -----------------------------------------------------------------------
    // Material helpers (unchanged from original)
    // -----------------------------------------------------------------------

    private void CreateMaterials()
    {
        Shader shader = ShaderUtils.GetLitShader();
        if (shader == null)
        {
            Debug.LogError("EquippedItemController: Could not find URP/Lit shader, using basic fallback.", this);
            shader = Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Standard")
                  ?? Shader.Find("Sprites/Default");
        }

        _handMaterial  = CreateMaterial("HandSkin",      new Color(0.78f, 0.54f, 0.40f), shader);
        _woodMaterial  = CreateMaterial("AxeHandleWood", new Color(0.44f, 0.26f, 0.12f), shader);
        _metalMaterial = CreateMaterial("AxeBladeMetal", new Color(0.55f, 0.58f, 0.62f), shader);
        _blockMaterial = CreateMaterial("HeldBlockMat",  Color.white,                    shader);

        Renderer[] armRenderers = _viewRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in armRenderers)
        {
            r.sharedMaterial = _handMaterial;
        }
    }

    private void ApplyAxeMaterials(Transform axeRoot)
    {
        Renderer[] renderers = axeRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            string n = r.gameObject.name;
            if (n.Contains("Head") || n.Contains("Blade") || n.Contains("Metal") || n.Contains("Poll"))
            {
                if (_metalMaterial != null) r.sharedMaterial = _metalMaterial;
            }
            else
            {
                if (_woodMaterial != null) r.sharedMaterial = _woodMaterial;
            }
        }
    }

    private void ReplaceEquippedObject()
    {
        if (_heldObject != null)
        {
            Destroy(_heldObject);
            _heldObject = null;
        }
    }

    private Material CreateMaterial(string materialName, Color color, Shader shader)
    {
        if (shader == null) shader = ShaderUtils.GetLitShader();
        Material material = new Material(shader) { name = materialName + " Runtime Material" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))     material.SetColor("_Color",     color);
        return material;
    }

    private Color GetBlockColor(BlockType blockType)
    {
        switch (blockType)
        {
            case BlockType.Grass:  return new Color(0.32f, 0.68f, 0.22f);
            case BlockType.Dirt:   return new Color(0.48f, 0.30f, 0.16f);
            case BlockType.Stone:  return new Color(0.52f, 0.54f, 0.56f);
            case BlockType.Wood:   return new Color(0.46f, 0.28f, 0.14f);
            case BlockType.Sand:   return new Color(0.86f, 0.78f, 0.52f);
            default:               return Color.white;
        }
    }
}
