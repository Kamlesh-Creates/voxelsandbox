using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Real-time 2D player-centered, rotating navigation minimap.
/// The player stays locked at the center while the terrain smoothly translates and rotates underneath,
/// always keeping the player's facing direction toward the top of the minimap.
/// Updates instantly upon voxel block destruction and placement.
/// </summary>
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

    [Header("Minimap UI Settings")]
    [SerializeField] private Vector2 minimapSize = new Vector2(220f, 220f);
    [SerializeField] private Vector2 mapPadding = new Vector2(16f, 16f);
    [SerializeField] private float mapZoomScale = 4.0f; // Pixels per voxel block for clear radar view

    [Header("Colors")]
    [SerializeField] private Color grassColor = new Color(0.32f, 0.68f, 0.22f);
    [SerializeField] private Color dirtColor = new Color(0.48f, 0.30f, 0.16f);
    [SerializeField] private Color stoneColor = new Color(0.52f, 0.54f, 0.56f);
    [SerializeField] private Color woodColor = new Color(0.46f, 0.28f, 0.14f);
    [SerializeField] private Color sandColor = new Color(0.86f, 0.78f, 0.52f);
    [SerializeField] private Color leavesColor = new Color(0.20f, 0.48f, 0.14f);
    [SerializeField] private Color waterColor = new Color(0.18f, 0.52f, 0.86f);
    [SerializeField] private Color emptyColor = new Color(0.08f, 0.09f, 0.12f);

    private VoxelWorld _world;
    private Transform _playerTransform;
    private Texture2D _mapTexture;
    private RawImage _mapImage;
    private RectTransform _mapContentRect;
    private RectTransform _mapImageRect;
    private RectTransform _playerMarkerRect;
    private Text _coordText;
    private bool _initialized;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        InitializeMinimap();
    }

    private void OnDestroy()
    {
        if (_world != null)
        {
            _world.OnBlockChanged -= HandleBlockChanged;
        }

        if (_mapTexture != null)
        {
            Destroy(_mapTexture);
        }
    }

    public void InitializeMinimap()
    {
        if (_initialized) return;

        _world = FindFirstObjectByType<VoxelWorld>();
        FindPlayer();

        CreateMinimapUI();
        GenerateInitialMap();

        if (_world != null)
        {
            _world.OnBlockChanged += HandleBlockChanged;
        }

        _initialized = true;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            return;
        }

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            _playerTransform = pc.transform;
        }
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            FindPlayer();
            if (_playerTransform == null) return;
        }

        UpdatePlayerCenteredMap();
    }

    private void CreateMinimapUI()
    {
        // 1. Screen-Space Canvas
        GameObject canvasObj = new GameObject("MinimapCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 2. Fixed Minimap Container Panel (Top-Right)
        GameObject panelObj = new GameObject("MinimapPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-16f, -16f);
        panelRect.sizeDelta = minimapSize;

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.04f, 0.06f, 0.08f, 0.90f);

        Outline outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.40f, 0.48f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // 3. Fixed Header Label ("MINIMAP")
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(panelObj.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -4f);
        headerRect.sizeDelta = new Vector2(0f, 14f);
        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "MINIMAP";
        headerText.font = uiFont;
        headerText.fontSize = 9;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(0.85f, 0.88f, 0.94f);
        headerText.alignment = TextAnchor.MiddleCenter;

        // 4. Map Viewport (Clips/Masks rotating map contents outside the viewport window)
        Vector2 viewportSize = minimapSize - mapPadding - new Vector2(0f, 8f);
        GameObject viewportObj = new GameObject("MapViewport");
        viewportObj.transform.SetParent(panelObj.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.45f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.45f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.sizeDelta = viewportSize;

        // Viewport background & clipping mask
        Image viewportBg = viewportObj.AddComponent<Image>();
        viewportBg.color = emptyColor;
        viewportObj.AddComponent<RectMask2D>();

        // 5. Rotating Map Pivot Content (Rotates with player yaw)
        GameObject contentObj = new GameObject("MapContent");
        contentObj.transform.SetParent(viewportObj.transform, false);
        _mapContentRect = contentObj.AddComponent<RectTransform>();
        _mapContentRect.anchorMin = new Vector2(0.5f, 0.5f);
        _mapContentRect.anchorMax = new Vector2(0.5f, 0.5f);
        _mapContentRect.pivot = new Vector2(0.5f, 0.5f);
        _mapContentRect.anchoredPosition = Vector2.zero;
        _mapContentRect.sizeDelta = Vector2.zero;

        // 6. Translating Map Texture Image (Translates underneath player)
        int worldWidth = _world != null ? _world.WorldWidth : 128;
        int worldDepth = _world != null ? _world.WorldDepth : 128;
        Vector2 mapPixelSize = new Vector2(worldWidth * mapZoomScale, worldDepth * mapZoomScale);

        GameObject mapImgObj = new GameObject("MapImage");
        mapImgObj.transform.SetParent(contentObj.transform, false);
        _mapImageRect = mapImgObj.AddComponent<RectTransform>();
        _mapImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        _mapImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        _mapImageRect.pivot = new Vector2(0.5f, 0.5f);
        _mapImageRect.sizeDelta = mapPixelSize;

        _mapImage = mapImgObj.AddComponent<RawImage>();

        _mapTexture = new Texture2D(worldWidth, worldDepth, TextureFormat.RGBA32, false)
        {
            name = "Realtime_Minimap_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _mapImage.texture = _mapTexture;

        // 7. Fixed Player Marker (Stays centered, pointing UP in player's forward direction)
        GameObject markerObj = new GameObject("PlayerMarker");
        markerObj.transform.SetParent(panelObj.transform, false);
        _playerMarkerRect = markerObj.AddComponent<RectTransform>();
        _playerMarkerRect.anchorMin = new Vector2(0.5f, 0.45f);
        _playerMarkerRect.anchorMax = new Vector2(0.5f, 0.45f);
        _playerMarkerRect.pivot = new Vector2(0.5f, 0.5f);
        _playerMarkerRect.anchoredPosition = Vector2.zero;
        _playerMarkerRect.sizeDelta = new Vector2(14f, 14f);

        // Directional arrow icon (points UP)
        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(markerObj.transform, false);
        RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
        arrowRect.sizeDelta = new Vector2(8f, 12f);
        arrowRect.anchoredPosition = new Vector2(0f, 1f);
        Image arrowImg = arrowObj.AddComponent<Image>();
        arrowImg.color = new Color(1f, 0.82f, 0.15f, 1f);

        Outline arrowOutline = arrowObj.AddComponent<Outline>();
        arrowOutline.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        arrowOutline.effectDistance = new Vector2(1f, 1f);

        // Center dot core
        GameObject coreObj = new GameObject("Core");
        coreObj.transform.SetParent(markerObj.transform, false);
        RectTransform coreRect = coreObj.AddComponent<RectTransform>();
        coreRect.sizeDelta = new Vector2(4f, 4f);
        Image coreImg = coreObj.AddComponent<Image>();
        coreImg.color = Color.white;

        // 8. Fixed Coordinates Badge (Bottom-Center)
        GameObject coordObj = new GameObject("Coordinates");
        coordObj.transform.SetParent(panelObj.transform, false);
        RectTransform coordRect = coordObj.AddComponent<RectTransform>();
        coordRect.anchorMin = new Vector2(0f, 0f);
        coordRect.anchorMax = new Vector2(1f, 0f);
        coordRect.pivot = new Vector2(0.5f, 0f);
        coordRect.anchoredPosition = new Vector2(0f, 3f);
        coordRect.sizeDelta = new Vector2(0f, 12f);
        _coordText = coordObj.AddComponent<Text>();
        _coordText.text = "X: 64  Z: 64";
        _coordText.font = uiFont;
        _coordText.fontSize = 8;
        _coordText.color = new Color(0.7f, 0.75f, 0.8f);
        _coordText.alignment = TextAnchor.MiddleCenter;
    }

    private void GenerateInitialMap()
    {
        if (_mapTexture == null || _world == null) return;

        int width = _world.WorldWidth;
        int depth = _world.WorldDepth;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                Color pixelColor = GetTopBlockColor(x, z);
                _mapTexture.SetPixel(x, z, pixelColor);
            }
        }

        _mapTexture.Apply();
    }

    private void HandleBlockChanged(Vector3Int gridPosition)
    {
        if (_mapTexture == null || _world == null) return;

        // Re-sample the affected column (and immediate neighbors for tree canopies/roofs)
        int startX = Mathf.Max(0, gridPosition.x - 1);
        int endX = Mathf.Min(_world.WorldWidth - 1, gridPosition.x + 1);
        int startZ = Mathf.Max(0, gridPosition.z - 1);
        int endZ = Mathf.Min(_world.WorldDepth - 1, gridPosition.z + 1);

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                Color pixelColor = GetTopBlockColor(x, z);
                _mapTexture.SetPixel(x, z, pixelColor);
            }
        }

        _mapTexture.Apply();
    }

    private Color GetTopBlockColor(int x, int z)
    {
        if (_world == null) return emptyColor;

        int maxHeight = _world.MaximumTerrainHeight * 3;
        for (int y = maxHeight; y >= 0; y--)
        {
            VoxelBlock block = _world.GetBlock(new Vector3Int(x, y, z));
            if (block != null && block.Type != BlockType.Air)
            {
                Color baseColor;
                switch (block.Type)
                {
                    case BlockType.Grass: baseColor = grassColor; break;
                    case BlockType.Dirt: baseColor = dirtColor; break;
                    case BlockType.Stone: baseColor = stoneColor; break;
                    case BlockType.Wood: baseColor = woodColor; break;
                    case BlockType.Sand: baseColor = sandColor; break;
                    case BlockType.Leaves: baseColor = leavesColor; break;
                    case BlockType.Water: baseColor = waterColor; break;
                    default: baseColor = emptyColor; break;
                }

                // Elevation shading for topographic relief depth
                float elevationShade = Mathf.Clamp(0.85f + (y - 3) * 0.04f, 0.75f, 1.25f);
                return baseColor * elevationShade;
            }
        }

        return emptyColor;
    }

    private void UpdatePlayerCenteredMap()
    {
        if (_playerTransform == null || _world == null || _mapImageRect == null || _mapContentRect == null) return;

        Vector3 playerPos = _playerTransform.position;
        int worldWidth = _world.WorldWidth;
        int worldDepth = _world.WorldDepth;

        // 1. Calculate map image offset so the player's world position is centered at (0, 0) of MapContent
        float localX = ((worldWidth * 0.5f) - playerPos.x) * mapZoomScale;
        float localZ = ((worldDepth * 0.5f) - playerPos.z) * mapZoomScale;
        _mapImageRect.anchoredPosition = new Vector2(localX, localZ);

        // 2. Rotate MapContent with player yaw so the facing direction is ALWAYS pointing toward the TOP of the minimap
        float playerYaw = _playerTransform.eulerAngles.y;
        _mapContentRect.localRotation = Quaternion.Euler(0f, 0f, playerYaw);

        // 3. Update live world coordinates
        if (_coordText != null)
        {
            _coordText.text = $"X: {Mathf.RoundToInt(playerPos.x)}  Z: {Mathf.RoundToInt(playerPos.z)}";
        }
    }
}
