using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Raycast-Based Block Highlighting Controller for enhanced navigation and interaction.
/// Displays a crisp, thin, bright white wireframe outline around the exact voxel block
/// currently targeted by the player's center-screen raycast in BlockInteractionController.
///
/// Stability and Performance Optimizations:
/// - Reuses BlockInteractionController's camera-center raycast; zero duplicate raycasts.
/// - Exact voxel grid coordinate alignment matching VoxelWorld bounds.
/// - Non-overlapping 12-prism 3D mesh geometry to eliminate coplanar face z-fighting.
/// - Dedicated cube padding (2.5mm) to prevent depth fighting with the voxel surface.
/// - Unlit bright white material rendered consistently under daylight, shadows, and night.
/// - Placed on the 'Ignore Raycast' layer with zero colliders to prevent physics interference.
/// - State-tracked visibility: zero redundant SetActive or transform updates while aiming at a stationary block.
/// - Smooth transition when moving across adjacent blocks without blinking.
/// </summary>
public class BlockHighlightController : MonoBehaviour
{
    [Header("Targeting Reference")]
    [Tooltip("The interaction controller providing the target voxel block. Automatically found if null.")]
    [SerializeField] private BlockInteractionController interactionController;

    [Header("Outline Appearance")]
    [Tooltip("Color of the wireframe outline. Defaults to bright white.")]
    [SerializeField] private Color outlineColor = Color.white;

    [Tooltip("Thickness of each wireframe edge prism.")]
    [Range(0.005f, 0.05f)]
    [SerializeField] private float lineThickness = 0.018f;

    [Tooltip("Slight outward expansion to prevent z-fighting with the block faces.")]
    [Range(0.001f, 0.01f)]
    [SerializeField] private float cubePadding = 0.0025f;

    private GameObject _highlightObject;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _wireframeMesh;
    private Material _highlightMaterial;
    private bool _isInitialized;

    // Visibility and target tracking state to prevent rapid toggling
    private Vector3Int _currentGridPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    private bool _isHighlightVisible;

    public bool IsVisible => _isHighlightVisible;
    public Vector3 CurrentHighlightPosition => _highlightObject != null ? _highlightObject.transform.position : Vector3.zero;

    private void Awake()
    {
        InitializeHighlightObject();
    }

    private void Start()
    {
        FindInteractionController();
    }

    private void OnEnable()
    {
        FindInteractionController();
        if (interactionController != null)
        {
            interactionController.OnTargetBlockChanged += HandleTargetBlockChanged;
        }

        VoxelWorld world = FindFirstObjectByType<VoxelWorld>();
        if (world != null)
        {
            world.OnBlockChanged += HandleWorldBlockChanged;
        }

        UpdateHighlightState();
    }

    private void OnDisable()
    {
        if (interactionController != null)
        {
            interactionController.OnTargetBlockChanged -= HandleTargetBlockChanged;
        }

        VoxelWorld world = FindFirstObjectByType<VoxelWorld>();
        if (world != null)
        {
            world.OnBlockChanged -= HandleWorldBlockChanged;
        }

        HideHighlight();
    }

    private void OnDestroy()
    {
        if (_highlightObject != null)
        {
            Destroy(_highlightObject);
        }

        if (_wireframeMesh != null)
        {
            Destroy(_wireframeMesh);
        }

        if (_highlightMaterial != null)
        {
            Destroy(_highlightMaterial);
        }
    }

    private void LateUpdate()
    {
        // Continuous verification ensures immediate response to camera motion and block changes
        UpdateHighlightState();
    }

    private void FindInteractionController()
    {
        if (interactionController == null)
        {
            interactionController = GetComponent<BlockInteractionController>();
            if (interactionController == null)
            {
                interactionController = FindFirstObjectByType<BlockInteractionController>();
            }
        }
    }

    private void InitializeHighlightObject()
    {
        if (_isInitialized) return;

        // 1. Create persistent root GameObject for the outline
        _highlightObject = new GameObject("BlockHighlight_Wireframe");
        _highlightObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        if (_highlightObject.layer < 0) _highlightObject.layer = 2; // Layer 2 is built-in Ignore Raycast

        // Ensure no colliders exist on this object
        Collider col = _highlightObject.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 2. Build procedural wireframe cube mesh (12 non-overlapping edge prisms)
        _meshFilter = _highlightObject.AddComponent<MeshFilter>();
        _wireframeMesh = BuildWireframeMesh(1.0f, lineThickness, cubePadding);
        _meshFilter.sharedMesh = _wireframeMesh;

        // 3. Create unlit bright white material
        _meshRenderer = _highlightObject.AddComponent<MeshRenderer>();
        _highlightMaterial = CreateHighlightMaterial(outlineColor);
        _meshRenderer.sharedMaterial = _highlightMaterial;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // Renderer disabled initially until a valid block is targeted
        _meshRenderer.enabled = false;
        _isHighlightVisible = false;
        _highlightObject.SetActive(true);
        _isInitialized = true;
    }

    private void HandleTargetBlockChanged(bool hasTarget, Vector3Int gridPos, BlockType blockType)
    {
        UpdateHighlightState();
    }

    private void HandleWorldBlockChanged(Vector3Int changedPos)
    {
        UpdateHighlightState();
    }

    private void UpdateHighlightState()
    {
        if (_highlightObject == null)
        {
            InitializeHighlightObject();
        }

        if (interactionController == null)
        {
            FindInteractionController();
            if (interactionController == null)
            {
                HideHighlight();
                return;
            }
        }

        if (interactionController.HasTarget)
        {
            Vector3Int targetPos = interactionController.TargetGridPosition;

            // Update transform only if the target position changed or transitioning from hidden
            if (!_isHighlightVisible || _currentGridPosition != targetPos)
            {
                _currentGridPosition = targetPos;
                Vector3 worldCenter = new Vector3(targetPos.x + 0.5f, targetPos.y + 0.5f, targetPos.z + 0.5f);
                _highlightObject.transform.position = worldCenter;
                _highlightObject.transform.rotation = Quaternion.identity;
            }

            // Keep the outline enabled while the target remains valid
            if (!_isHighlightVisible)
            {
                _isHighlightVisible = true;
                _meshRenderer.enabled = true;
            }
        }
        else
        {
            // Hide immediately when there is genuinely no valid target
            HideHighlight();
        }
    }

    private void HideHighlight()
    {
        if (_isHighlightVisible)
        {
            _isHighlightVisible = false;
            _currentGridPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
        }
    }

    /// <summary>
    /// Constructs a single lightweight 3D mesh consisting of 12 non-overlapping rectangular prisms
    /// forming a complete wireframe cube surrounding a unit block [ -0.5, +0.5 ]^3.
    /// Non-overlapping geometry prevents coplanar face z-fighting at the 8 cube corners.
    /// </summary>
    private Mesh BuildWireframeMesh(float blockSize, float thickness, float padding)
    {
        Mesh mesh = new Mesh
        {
            name = "BlockHighlight_WireframeMesh"
        };

        List<Vector3> verts = new List<Vector3>(96);
        List<int> tris = new List<int>(432);

        float inner = (blockSize * 0.5f) + padding;
        float outer = inner + thickness;

        // 1. 4 Horizontal Beams along X axis (span X from -outer to +outer, establishing full-width corner caps)
        // Top-Front (+Y, +Z)
        AddBox(new Vector3(-outer, inner, inner), new Vector3(outer, outer, outer), verts, tris);
        // Top-Back (+Y, -Z)
        AddBox(new Vector3(-outer, inner, -outer), new Vector3(outer, outer, -inner), verts, tris);
        // Bottom-Front (-Y, +Z)
        AddBox(new Vector3(-outer, -outer, inner), new Vector3(outer, -inner, outer), verts, tris);
        // Bottom-Back (-Y, -Z)
        AddBox(new Vector3(-outer, -outer, -outer), new Vector3(outer, -inner, -inner), verts, tris);

        // 2. 4 Vertical Pillars along Y axis (span Y from -inner to +inner, cleanly meeting top and bottom beams without overlap)
        // Front-Right (+X, +Z)
        AddBox(new Vector3(inner, -inner, inner), new Vector3(outer, inner, outer), verts, tris);
        // Front-Left (-X, +Z)
        AddBox(new Vector3(-outer, -inner, inner), new Vector3(-inner, inner, outer), verts, tris);
        // Back-Right (+X, -Z)
        AddBox(new Vector3(inner, -inner, -outer), new Vector3(outer, inner, -inner), verts, tris);
        // Back-Left (-X, -Z)
        AddBox(new Vector3(-outer, -inner, -outer), new Vector3(-inner, inner, -inner), verts, tris);

        // 3. 4 Horizontal Side Bars along Z axis (span Z from -inner to +inner, cleanly meeting front and back beams without overlap)
        // Top-Right (+X, +Y)
        AddBox(new Vector3(inner, inner, -inner), new Vector3(outer, outer, inner), verts, tris);
        // Top-Left (-X, +Y)
        AddBox(new Vector3(-outer, inner, -inner), new Vector3(-inner, outer, inner), verts, tris);
        // Bottom-Right (+X, -Y)
        AddBox(new Vector3(inner, -outer, -inner), new Vector3(outer, -inner, inner), verts, tris);
        // Bottom-Left (-X, -Y)
        AddBox(new Vector3(-outer, -outer, -inner), new Vector3(-inner, -inner, inner), verts, tris);

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void AddBox(Vector3 min, Vector3 max, List<Vector3> verts, List<int> tris)
    {
        int start = verts.Count;

        // 8 vertices of the box
        verts.Add(new Vector3(min.x, min.y, min.z)); // 0: ---
        verts.Add(new Vector3(max.x, min.y, min.z)); // 1: +--
        verts.Add(new Vector3(max.x, min.y, max.z)); // 2: +-+
        verts.Add(new Vector3(min.x, min.y, max.z)); // 3: --+
        verts.Add(new Vector3(min.x, max.y, min.z)); // 4: -+-
        verts.Add(new Vector3(max.x, max.y, min.z)); // 5: ++-
        verts.Add(new Vector3(max.x, max.y, max.z)); // 6: +++
        verts.Add(new Vector3(min.x, max.y, max.z)); // 7: -++

        // 6 faces (12 triangles) with outward clockwise winding
        // Bottom (Y-)
        AddQuad(tris, start, 3, 2, 1, 0);
        // Top (Y+)
        AddQuad(tris, start, 4, 5, 6, 7);
        // Front (Z+)
        AddQuad(tris, start, 7, 6, 2, 3);
        // Back (Z-)
        AddQuad(tris, start, 5, 4, 0, 1);
        // Left (X-)
        AddQuad(tris, start, 4, 7, 3, 0);
        // Right (X+)
        AddQuad(tris, start, 6, 5, 1, 2);
    }

    private void AddQuad(List<int> tris, int start, int a, int b, int c, int d)
    {
        tris.Add(start + a);
        tris.Add(start + b);
        tris.Add(start + c);

        tris.Add(start + a);
        tris.Add(start + c);
        tris.Add(start + d);
    }

    private Material CreateHighlightMaterial(Color color)
    {
        // Try Universal Render Pipeline/Unlit first, with standard fallbacks
        Shader shader = ShaderUtils.GetUnlitShader();
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = ShaderUtils.GetLitShader();
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader)
        {
            name = "BlockHighlight_Outline_Material"
        };

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color);
        }

        // Standard backface culling since every prism is closed with outward normals
        if (mat.HasProperty("_Cull"))
        {
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
        }

        // Render queue slightly after opaque geometry so depth test works cleanly
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 10;

        return mat;
    }
}
