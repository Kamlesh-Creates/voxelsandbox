using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the Main Menu / Start Screen for Voxelsandbox.
///
/// Features:
/// - Start Screen with Play, Settings, Achievements, and Quit options.
/// - Play loads "Mainscene.unity", resets timeScale to 1, and prepares first-person cursor locking.
/// - Settings panel matching PauseMenuController keys: "Voxel_MouseSensitivity", "Voxel_MasterVolume", "Voxel_Fullscreen".
/// - Achievements panel showcasing the 4 core achievements with clear explanation of session tracking.
/// - Standalone Application.Quit() with graceful in-editor notification banner.
/// - Clean procedural UI construction ensuring 100% reliable initialization without missing references.
/// - Responsive scaling for 2560x1440, 1920x1080, and various display ratios.
/// - Bottom-left credits block: Made By / Guided By.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string GameplaySceneName = "Mainscene";

    // PlayerPrefs keys shared with PauseMenuController
    private const string PrefKeySensitivity = "Voxel_MouseSensitivity";
    private const string PrefKeyVolume = "Voxel_MasterVolume";
    private const string PrefKeyFullscreen = "Voxel_Fullscreen";
    private const string PrefKeyFOV = "Voxel_CameraFOV";

    private const float DefaultSensitivity = 0.15f;
    private const float DefaultVolume = 1.0f;
    private const int DefaultFullscreen = 1;
    private const float DefaultFOV = 80f;
    private const float MinFOV = 60f;
    private const float MaxFOV = 100f;

    [Header("Current Settings")]
    [SerializeField] private float currentSensitivity = DefaultSensitivity;
    [SerializeField] private float currentVolume = DefaultVolume;
    [SerializeField] private bool currentFullscreen = true;
    [SerializeField] private float currentFOV = DefaultFOV;

    // UI Structure References
    private GameObject _canvasObj;
    private GameObject _mainPanel;
    private GameObject _settingsPanel;
    private GameObject _achievementsPanel;
    private GameObject _toastNotificationObj;
    private Text _toastNotificationText;

    private Slider _fovSlider;
    private Slider _sensitivitySlider;
    private Slider _volumeSlider;
    private Toggle _fullscreenToggle;

    private Text _fovValueText;
    private Text _sensitivityValueText;
    private Text _volumeValueText;
    private Text _fullscreenStatusText;

    public float CurrentFOV => currentFOV;

    private Button _playButton;
    private Text _playButtonText;

    private Font _uiFont;
    private bool _isLoadingScene;
    private Coroutine _toastCoroutine;

    private void Awake()
    {
        Debug.Log("[MainMenuController] Awake started in active scene: " + SceneManager.GetActiveScene().name);

        // 1. Ensure gameplay timescale is normal
        Time.timeScale = 1f;

        // 2. Unlock and display mouse cursor for menu navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Load user settings
        LoadSettings();

        // 4. Procedurally construct UI elements
        InitializeUI();

        Debug.Log("[MainMenuController] UI initialized successfully.");
    }

    private void Start()
    {
        ApplyAllSettings();

        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg == "-autoplay")
            {
                StartCoroutine(AutoPlayCoroutine());
                break;
            }
        }
    }

    private System.Collections.IEnumerator AutoPlayCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        OnPlayClicked();
    }

    private void Update()
    {
        // Allow Escape key to return to Main Menu from Settings or Achievements panel
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (_settingsPanel != null && _settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else if (_achievementsPanel != null && _achievementsPanel.activeSelf)
            {
                CloseAchievements();
            }
        }
    }

    // -----------------------------------------------------------------------
    // Button Action Handlers
    // -----------------------------------------------------------------------

    public void OnPlayClicked()
    {
        if (_isLoadingScene) return;
        _isLoadingScene = true;

        if (_playButton != null)
        {
            _playButton.interactable = false;
        }

        if (_playButtonText != null)
        {
            _playButtonText.text = "LOADING WORLD...";
            _playButtonText.color = new Color(0.7f, 0.95f, 0.7f);
        }

        // Reset timescale and prepare cursor state
        Time.timeScale = 1f;

        PlayUiClickSound();

        // Load existing gameplay scene
        SceneManager.LoadScene(GameplaySceneName);
    }

    public void OnSettingsClicked()
    {
        PlayUiClickSound();
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_achievementsPanel != null) _achievementsPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(true);

        RefreshSettingsUI();
    }

    public void CloseSettings()
    {
        PlayUiClickSound();
        SaveSettings();

        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_achievementsPanel != null) _achievementsPanel.SetActive(false);
        if (_mainPanel != null) _mainPanel.SetActive(true);
    }

    public void OnAchievementsClicked()
    {
        PlayUiClickSound();
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_achievementsPanel != null) _achievementsPanel.SetActive(true);
    }

    public void CloseAchievements()
    {
        PlayUiClickSound();
        if (_achievementsPanel != null) _achievementsPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_mainPanel != null) _mainPanel.SetActive(true);
    }

    public void OnQuitClicked()
    {
        PlayUiClickSound();
        SaveSettings();

#if UNITY_EDITOR
        ShowNotification("Quit is disabled in the Unity Editor.\nIn the standalone build, this exits to desktop.");
        Debug.Log("[MainMenuController] Application.Quit() requested (Safely bypassed in Editor).");
#else
        Application.Quit();
#endif
    }

    // -----------------------------------------------------------------------
    // Settings Logic & Persistence
    // -----------------------------------------------------------------------

    public void SetCameraFOV(float value)
    {
        currentFOV = Mathf.Clamp(value, MinFOV, MaxFOV);

        if (_fovValueText != null)
        {
            _fovValueText.text = Mathf.RoundToInt(currentFOV) + "°";
        }

        PlayerPrefs.SetFloat(PrefKeyFOV, currentFOV);
    }

    public void SetMouseSensitivity(float value)
    {
        currentSensitivity = Mathf.Clamp(value, 0.02f, 0.50f);

        if (_sensitivityValueText != null)
        {
            _sensitivityValueText.text = currentSensitivity.ToString("F2");
        }

        PlayerPrefs.SetFloat(PrefKeySensitivity, currentSensitivity);
    }

    public void SetMasterVolume(float value)
    {
        currentVolume = Mathf.Clamp01(value);
        AudioListener.volume = currentVolume;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.MasterVolume = currentVolume;
        }

        if (_volumeValueText != null)
        {
            _volumeValueText.text = Mathf.RoundToInt(currentVolume * 100f) + "%";
        }

        PlayerPrefs.SetFloat(PrefKeyVolume, currentVolume);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        currentFullscreen = isFullscreen;
        Screen.fullScreen = isFullscreen;

        if (_fullscreenStatusText != null)
        {
            _fullscreenStatusText.text = isFullscreen ? "Fullscreen" : "Windowed";
            _fullscreenStatusText.color = isFullscreen ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.6f, 0.65f, 0.72f);
        }

        PlayerPrefs.SetInt(PrefKeyFullscreen, isFullscreen ? 1 : 0);
    }

    private void LoadSettings()
    {
        currentSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeySensitivity, DefaultSensitivity), 0.02f, 0.50f);
        currentVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyVolume, DefaultVolume));
        currentFullscreen = PlayerPrefs.GetInt(PrefKeyFullscreen, Screen.fullScreen ? 1 : DefaultFullscreen) == 1;
        currentFOV = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyFOV, DefaultFOV), MinFOV, MaxFOV);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(PrefKeySensitivity, currentSensitivity);
        PlayerPrefs.SetFloat(PrefKeyVolume, currentVolume);
        PlayerPrefs.SetInt(PrefKeyFullscreen, currentFullscreen ? 1 : 0);
        PlayerPrefs.SetFloat(PrefKeyFOV, currentFOV);
        PlayerPrefs.Save();
    }

    private void ApplyAllSettings()
    {
        SetCameraFOV(currentFOV);
        SetMouseSensitivity(currentSensitivity);
        SetMasterVolume(currentVolume);
        SetFullscreen(currentFullscreen);
    }

    private void RefreshSettingsUI()
    {
        if (_fovSlider != null) _fovSlider.value = currentFOV;
        if (_sensitivitySlider != null) _sensitivitySlider.value = currentSensitivity;
        if (_volumeSlider != null) _volumeSlider.value = currentVolume;
        if (_fullscreenToggle != null) _fullscreenToggle.isOn = currentFullscreen;

        if (_fovValueText != null) _fovValueText.text = Mathf.RoundToInt(currentFOV) + "°";
        if (_sensitivityValueText != null) _sensitivityValueText.text = currentSensitivity.ToString("F2");
        if (_volumeValueText != null) _volumeValueText.text = Mathf.RoundToInt(currentVolume * 100f) + "%";
        if (_fullscreenStatusText != null)
        {
            _fullscreenStatusText.text = currentFullscreen ? "Fullscreen" : "Windowed";
            _fullscreenStatusText.color = currentFullscreen ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.6f, 0.65f, 0.72f);
        }
    }

    private void PlayUiClickSound()
    {
        // Safe UI click feedback
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = currentVolume * 0.7f;

        // Procedural click tone
        AudioClip click = AudioSynthesizer.CreateBlockPlaceClip(BlockType.Wood);
        if (click != null)
        {
            audioSource.PlayOneShot(click);
        }
    }

    private void ShowNotification(string message)
    {
        if (_toastNotificationObj == null || _toastNotificationText == null) return;

        if (_toastCoroutine != null)
        {
            StopCoroutine(_toastCoroutine);
        }

        _toastNotificationText.text = message;
        _toastCoroutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        _toastNotificationObj.SetActive(true);
        yield return new WaitForSeconds(3.0f);
        _toastNotificationObj.SetActive(false);
        _toastCoroutine = null;
    }

    // -----------------------------------------------------------------------
    // Procedural UI Construction
    // -----------------------------------------------------------------------

    private void InitializeUI()
    {
        EnsureEventSystem();
        CreateCanvas();
        CreateDarkVignette();
        CreateMainPanel();
        CreateCreditsBlock();
        CreateSettingsPanel();
        CreateAchievementsPanel();
        CreateToastNotification();

        // Initial visibility
        _mainPanel.SetActive(true);
        _settingsPanel.SetActive(false);
        _achievementsPanel.SetActive(false);
        _toastNotificationObj.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    private void CreateCanvas()
    {
        _canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObj.AddComponent<GraphicRaycaster>();

        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_uiFont == null)
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts != null && fonts.Length > 0) _uiFont = fonts[0];
        }
        if (_uiFont == null)
        {
            _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
    }

    private void CreateDarkVignette()
    {
        // Soft gradient overlay on the left side to maximize readability while preserving 3D view
        GameObject overlayObj = new GameObject("AtmosphericVignette");
        overlayObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform rt = overlayObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0.55f, 1f);
        rt.sizeDelta = Vector2.zero;

        Image img = overlayObj.AddComponent<Image>();
        img.color = new Color(0.04f, 0.06f, 0.10f, 0.72f);
        img.raycastTarget = false;
    }

    private void CreateMainPanel()
    {
        _mainPanel = new GameObject("MainPanel");
        _mainPanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _mainPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(110f, 0f);
        panelRect.sizeDelta = new Vector2(500f, 680f);

        // 1. Large Game Title: "VOXEL SANDBOX"
        GameObject titleObj = new GameObject("GameTitle");
        titleObj.transform.SetParent(_mainPanel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(500f, 64f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "VOXEL SANDBOX";
        titleText.font = _uiFont;
        titleText.fontSize = 46;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(1f, 0.85f, 0.28f, 1f);
        titleText.alignment = TextAnchor.MiddleLeft;

        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.08f, 0.06f, 0.04f, 0.95f);
        titleOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // 2. Subtitle: "EXPLORE • BUILD • SURVIVE"
        GameObject subtitleObj = new GameObject("GameSubtitle");
        subtitleObj.transform.SetParent(_mainPanel.transform, false);
        RectTransform subRect = subtitleObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 1f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0f, 1f);
        subRect.anchoredPosition = new Vector2(4f, -86f);
        subRect.sizeDelta = new Vector2(500f, 30f);

        Text subText = subtitleObj.AddComponent<Text>();
        subText.text = "EXPLORE  •  BUILD  •  SURVIVE";
        subText.font = _uiFont;
        subText.fontSize = 17;
        subText.fontStyle = FontStyle.Bold;
        subText.color = new Color(0.72f, 0.84f, 0.96f, 0.95f);
        subText.alignment = TextAnchor.MiddleLeft;

        Outline subOutline = subtitleObj.AddComponent<Outline>();
        subOutline.effectColor = new Color(0.05f, 0.08f, 0.14f, 0.85f);
        subOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Decorative Accent Line
        GameObject dividerObj = new GameObject("Divider");
        dividerObj.transform.SetParent(_mainPanel.transform, false);
        RectTransform divRect = dividerObj.AddComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0f, 1f);
        divRect.anchorMax = new Vector2(1f, 1f);
        divRect.pivot = new Vector2(0f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -128f);
        divRect.sizeDelta = new Vector2(380f, 3f);

        Image divImg = dividerObj.AddComponent<Image>();
        divImg.color = new Color(1f, 0.85f, 0.28f, 0.5f);

        // 4. Vertical Button Container
        GameObject btnContainer = new GameObject("ButtonsContainer");
        btnContainer.transform.SetParent(_mainPanel.transform, false);
        RectTransform containerRect = btnContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.pivot = new Vector2(0f, 1f);
        containerRect.anchoredPosition = new Vector2(0f, -150f);
        containerRect.sizeDelta = new Vector2(340f, 340f);

        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Buttons
        _playButton = CreateStyledButton(btnContainer.transform, "PLAY", OnPlayClicked, new Vector2(340f, 54f), true, out _playButtonText);
        CreateStyledButton(btnContainer.transform, "SETTINGS", OnSettingsClicked, new Vector2(340f, 48f), false, out _);
        CreateStyledButton(btnContainer.transform, "ACHIEVEMENTS", OnAchievementsClicked, new Vector2(340f, 48f), false, out _);
        CreateStyledButton(btnContainer.transform, "QUIT", OnQuitClicked, new Vector2(340f, 48f), false, out _);

        // 5. Version Label (Bottom-Left) — shifted up to clear credits
        GameObject verObj = new GameObject("VersionLabel");
        verObj.transform.SetParent(_mainPanel.transform, false);
        RectTransform verRect = verObj.AddComponent<RectTransform>();
        verRect.anchorMin = new Vector2(0f, 0f);
        verRect.anchorMax = new Vector2(0f, 0f);
        verRect.pivot = new Vector2(0f, 0f);
        verRect.anchoredPosition = new Vector2(4f, 10f);
        verRect.sizeDelta = new Vector2(300f, 22f);

        Text verText = verObj.AddComponent<Text>();
        verText.text = "VERSION 1.0  •  UNITY 6";
        verText.font = _uiFont;
        verText.fontSize = 12;
        verText.fontStyle = FontStyle.Normal;
        verText.color = new Color(0.55f, 0.62f, 0.72f, 0.70f);
        verText.alignment = TextAnchor.MiddleLeft;

        // 6. Gameplay Controls Reference Tip
        GameObject tipObj = new GameObject("ControlsHint");
        tipObj.transform.SetParent(_mainPanel.transform, false);
        RectTransform tipRect = tipObj.AddComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0f, 0f);
        tipRect.anchorMax = new Vector2(0f, 0f);
        tipRect.pivot = new Vector2(0f, 0f);
        tipRect.anchoredPosition = new Vector2(4f, 34f);
        tipRect.sizeDelta = new Vector2(450f, 22f);

        Text tipText = tipObj.AddComponent<Text>();
        tipText.text = "[WASD] Move   [Space] Jump   [1-9] Hotbar   [Q] Quests   [Esc] Pause";
        tipText.font = _uiFont;
        tipText.fontSize = 12;
        tipText.color = new Color(0.42f, 0.50f, 0.62f, 0.75f);
        tipText.alignment = TextAnchor.MiddleLeft;
    }

    // -----------------------------------------------------------------------
    // Credits Block — bottom-left of the main panel (safe at all resolutions)
    // -----------------------------------------------------------------------

    private void CreateCreditsBlock()
    {
        // Parent to _mainPanel so it scales and positions with everything else.
        // Anchor bottom-left of the panel; position just above the controls hint (y=34+22=56).
        GameObject creditsRoot = new GameObject("CreditsBlock");
        creditsRoot.transform.SetParent(_mainPanel.transform, false);

        RectTransform rootRect = creditsRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot    = new Vector2(0f, 0f);
        // y=62: sits just above the controls hint row (which ends at ~y=56)
        rootRect.anchoredPosition = new Vector2(0f, 62f);
        // Height: 7 rows (~16-18px each) + 6 gaps (2px) + 4px sep + 12px padding = ~142px
        rootRect.sizeDelta        = new Vector2(320f, 142f);

        // ── Subtle background card ─────────────────────────────────────────
        Image cardBg = creditsRoot.AddComponent<Image>();
        cardBg.color = new Color(0.04f, 0.06f, 0.10f, 0.62f);
        cardBg.raycastTarget = false;

        // ── Left accent bar (thin amber vertical stripe) ───────────────────
        GameObject accentBar = new GameObject("AccentBar");
        accentBar.transform.SetParent(creditsRoot.transform, false);
        RectTransform accentRect = accentBar.AddComponent<RectTransform>();
        accentRect.anchorMin       = new Vector2(0f, 0f);
        accentRect.anchorMax       = new Vector2(0f, 1f);
        accentRect.pivot           = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(0f, 0f);
        accentRect.sizeDelta       = new Vector2(3f, 0f);
        Image accentImg = accentBar.AddComponent<Image>();
        accentImg.color = new Color(1f, 0.82f, 0.28f, 0.80f);
        accentImg.raycastTarget = false;

        // ── Inner padding container ────────────────────────────────────────
        GameObject inner = new GameObject("Inner");
        inner.transform.SetParent(creditsRoot.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(10f, 6f);
        innerRect.offsetMax = new Vector2(-6f, -6f);

        VerticalLayoutGroup vlg = inner.AddComponent<VerticalLayoutGroup>();
        vlg.spacing             = 2f;
        vlg.childAlignment      = TextAnchor.UpperLeft;
        vlg.childControlWidth   = true;
        vlg.childControlHeight  = true;   // MUST be true: sizes rows by LayoutElement, not default 100px RectTransform
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // ── Helper: add one text row ───────────────────────────────────────
        System.Action<string, int, Color, FontStyle> AddRow = (content, size, col, style) =>
        {
            GameObject rowObj = new GameObject("Row_" + content.TrimStart().Replace(" ", "_").Replace(".", ""));
            rowObj.transform.SetParent(inner.transform, false);

            LayoutElement le = rowObj.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = size + 5f;

            Text t = rowObj.AddComponent<Text>();
            t.text          = content;
            t.font          = _uiFont;
            t.fontSize      = size;
            t.fontStyle     = style;
            t.color         = col;
            t.alignment     = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
        };

        // Colour palette
        Color amber = new Color(1f,   0.82f, 0.28f, 0.95f); // headings
        Color white = new Color(0.88f,0.92f, 1f,   0.90f);  // names
        Color green = new Color(0.68f,0.90f, 0.72f,0.90f);  // guide name

        // ── MADE BY ───────────────────────────────────────────────────────
        AddRow("MADE BY",            11, amber, FontStyle.Bold);
        AddRow("  Kamlesh Satpute",  13, white, FontStyle.Normal);
        AddRow("  Saksham Joshi",    13, white, FontStyle.Normal);
        AddRow("  Omkar Kotkar",     13, white, FontStyle.Normal);

        // Thin gold separator
        GameObject sep = new GameObject("Separator");
        sep.transform.SetParent(inner.transform, false);
        sep.AddComponent<RectTransform>();
        LayoutElement sepLE = sep.AddComponent<LayoutElement>();
        sepLE.minHeight = sepLE.preferredHeight = 4f;
        Image sepImg = sep.AddComponent<Image>();
        sepImg.color = new Color(1f, 0.82f, 0.28f, 0.20f);
        sepImg.raycastTarget = false;

        // ── GUIDED BY ─────────────────────────────────────────────────────
        AddRow("GUIDED BY",              11, amber, FontStyle.Bold);
        AddRow("  Dr. Pravin Game Sir",  13, green, FontStyle.Italic);
    }

    private void CreateSettingsPanel()
    {
        _settingsPanel = new GameObject("SettingsPanel");
        _settingsPanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _settingsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 540f);

        Image panelBg = _settingsPanel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.08f, 0.13f, 0.96f);

        Outline outline = _settingsPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.32f, 0.42f, 0.55f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Header Title
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(_settingsPanel.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -22f);
        headerRect.sizeDelta = new Vector2(0f, 36f);

        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "SETTINGS";
        headerText.font = _uiFont;
        headerText.fontSize = 24;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(1f, 0.85f, 0.28f);
        headerText.alignment = TextAnchor.MiddleCenter;

        // Subtitle
        GameObject subHeaderObj = new GameObject("SubHeader");
        subHeaderObj.transform.SetParent(_settingsPanel.transform, false);
        RectTransform subHeaderRect = subHeaderObj.AddComponent<RectTransform>();
        subHeaderRect.anchorMin = new Vector2(0f, 1f);
        subHeaderRect.anchorMax = new Vector2(1f, 1f);
        subHeaderRect.pivot = new Vector2(0.5f, 1f);
        subHeaderRect.anchoredPosition = new Vector2(0f, -56f);
        subHeaderRect.sizeDelta = new Vector2(0f, 22f);

        Text subHeaderText = subHeaderObj.AddComponent<Text>();
        subHeaderText.text = "Adjust camera FOV, mouse look, master volume, and display mode.";
        subHeaderText.font = _uiFont;
        subHeaderText.fontSize = 13;
        subHeaderText.color = new Color(0.68f, 0.74f, 0.82f);
        subHeaderText.alignment = TextAnchor.MiddleCenter;

        // Rows Container
        GameObject rowsObj = new GameObject("RowsContainer");
        rowsObj.transform.SetParent(_settingsPanel.transform, false);
        RectTransform rowsRect = rowsObj.AddComponent<RectTransform>();
        rowsRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowsRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowsRect.pivot = new Vector2(0.5f, 0.5f);
        rowsRect.anchoredPosition = new Vector2(0f, -10f);
        rowsRect.sizeDelta = new Vector2(480f, 320f);

        VerticalLayoutGroup vlg = rowsObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 1. Camera FOV Slider Row (60° to 100°, default 80°)
        _fovSlider = CreateSliderRow(
            rowsObj.transform,
            "Field of View (FOV)",
            MinFOV,
            MaxFOV,
            currentFOV,
            "°",
            SetCameraFOV,
            out _fovValueText
        );

        // 2. Mouse Sensitivity Row
        _sensitivitySlider = CreateSliderRow(
            rowsObj.transform,
            "Mouse Sensitivity",
            0.02f,
            0.50f,
            currentSensitivity,
            null,
            SetMouseSensitivity,
            out _sensitivityValueText
        );

        // 3. Master Volume Row
        _volumeSlider = CreateSliderRow(
            rowsObj.transform,
            "Master Volume",
            0.0f,
            1.0f,
            currentVolume,
            "%",
            SetMasterVolume,
            out _volumeValueText
        );

        // 4. Fullscreen Toggle Row
        _fullscreenToggle = CreateToggleRow(
            rowsObj.transform,
            "Fullscreen Display",
            currentFullscreen,
            SetFullscreen,
            out _fullscreenStatusText
        );

        // Back Button
        GameObject footerObj = new GameObject("Footer");
        footerObj.transform.SetParent(_settingsPanel.transform, false);
        RectTransform footerRect = footerObj.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 22f);
        footerRect.sizeDelta = new Vector2(240f, 44f);

        CreateStyledButton(footerObj.transform, "SAVE & BACK", CloseSettings, new Vector2(240f, 44f), false, out _);
    }

    private void CreateAchievementsPanel()
    {
        _achievementsPanel = new GameObject("AchievementsPanel");
        _achievementsPanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _achievementsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(700f, 620f);

        Image panelBg = _achievementsPanel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.08f, 0.13f, 0.96f);

        Outline outline = _achievementsPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.32f, 0.42f, 0.55f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Header Title
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(_achievementsPanel.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -22f);
        headerRect.sizeDelta = new Vector2(0f, 36f);

        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "ACHIEVEMENTS";
        headerText.font = _uiFont;
        headerText.fontSize = 24;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(1f, 0.85f, 0.28f);
        headerText.alignment = TextAnchor.MiddleCenter;

        // Subtitle
        GameObject subHeaderObj = new GameObject("SubHeader");
        subHeaderObj.transform.SetParent(_achievementsPanel.transform, false);
        RectTransform subHeaderRect = subHeaderObj.AddComponent<RectTransform>();
        subHeaderRect.anchorMin = new Vector2(0f, 1f);
        subHeaderRect.anchorMax = new Vector2(1f, 1f);
        subHeaderRect.pivot = new Vector2(0.5f, 1f);
        subHeaderRect.anchoredPosition = new Vector2(0f, -56f);
        subHeaderRect.sizeDelta = new Vector2(0f, 22f);

        Text subHeaderText = subHeaderObj.AddComponent<Text>();
        subHeaderText.text = "Key survival milestones available to unlock during gameplay.";
        subHeaderText.font = _uiFont;
        subHeaderText.fontSize = 13;
        subHeaderText.color = new Color(0.68f, 0.74f, 0.82f);
        subHeaderText.alignment = TextAnchor.MiddleCenter;

        // Cards Container
        GameObject cardsObj = new GameObject("CardsContainer");
        cardsObj.transform.SetParent(_achievementsPanel.transform, false);
        RectTransform cardsRect = cardsObj.AddComponent<RectTransform>();
        cardsRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardsRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRect.pivot = new Vector2(0.5f, 0.5f);
        cardsRect.anchoredPosition = new Vector2(0f, 20f);
        cardsRect.sizeDelta = new Vector2(620f, 340f);

        VerticalLayoutGroup vlg = cardsObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 4 Official In-Game Achievements from QuestManager
        CreateAchievementCard(cardsObj.transform, "[ 1 ]", "First Break", "Break your first block in the voxel world.");
        CreateAchievementCard(cardsObj.transform, "[ 2 ]", "Zombie Slayer", "Defend yourself and slay your first night zombie.");
        CreateAchievementCard(cardsObj.transform, "[ 3 ]", "Level Up", "Gather experience and advance to Level 2 for the first time.");
        CreateAchievementCard(cardsObj.transform, "[ 4 ]", "Master Miner", "Mine and harvest 100 total voxel blocks.");

        // Clear Persistence Explanation Info Box
        GameObject infoObj = new GameObject("SessionNoteBox");
        infoObj.transform.SetParent(_achievementsPanel.transform, false);
        RectTransform infoRect = infoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0f);
        infoRect.anchorMax = new Vector2(0.5f, 0f);
        infoRect.pivot = new Vector2(0.5f, 0f);
        infoRect.anchoredPosition = new Vector2(0f, 85f);
        infoRect.sizeDelta = new Vector2(620f, 60f);

        Image infoBg = infoObj.AddComponent<Image>();
        infoBg.color = new Color(0.10f, 0.14f, 0.20f, 0.85f);

        Outline infoOutline = infoObj.AddComponent<Outline>();
        infoOutline.effectColor = new Color(0.24f, 0.32f, 0.44f, 0.75f);
        infoOutline.effectDistance = new Vector2(1f, 1f);

        GameObject infoTextObj = new GameObject("InfoText");
        infoTextObj.transform.SetParent(infoObj.transform, false);
        RectTransform infoTextRect = infoTextObj.AddComponent<RectTransform>();
        infoTextRect.anchorMin = Vector2.zero;
        infoTextRect.anchorMax = Vector2.one;
        infoTextRect.offsetMin = new Vector2(16f, 8f);
        infoTextRect.offsetMax = new Vector2(-16f, -8f);

        Text infoText = infoTextObj.AddComponent<Text>();
        infoText.text = "Session Tracking Note: Quests and achievements are actively tracked per survival session in-game. Press [Q] during gameplay to view real-time quest objectives and claim XP rewards.";
        infoText.font = _uiFont;
        infoText.fontSize = 12;
        infoText.color = new Color(0.75f, 0.82f, 0.92f);
        infoText.alignment = TextAnchor.MiddleCenter;

        // Back Button
        GameObject footerObj = new GameObject("Footer");
        footerObj.transform.SetParent(_achievementsPanel.transform, false);
        RectTransform footerRect = footerObj.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 22f);
        footerRect.sizeDelta = new Vector2(240f, 44f);

        CreateStyledButton(footerObj.transform, "BACK", CloseAchievements, new Vector2(240f, 44f), false, out _);
    }

    private void CreateAchievementCard(Transform parent, string tag, string title, string description)
    {
        GameObject cardObj = new GameObject("Card_" + title.Replace(" ", ""));
        cardObj.transform.SetParent(parent, false);

        LayoutElement le = cardObj.AddComponent<LayoutElement>();
        le.minHeight = 70f;
        le.preferredHeight = 70f;

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.10f, 0.13f, 0.19f, 0.95f);

        Outline cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.24f, 0.32f, 0.44f, 0.8f);
        cardOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // Tag / Badge
        GameObject badgeObj = new GameObject("Badge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(18f, 0f);
        badgeRect.sizeDelta = new Vector2(50f, 30f);

        Text badgeText = badgeObj.AddComponent<Text>();
        badgeText.text = tag;
        badgeText.font = _uiFont;
        badgeText.fontSize = 15;
        badgeText.fontStyle = FontStyle.Bold;
        badgeText.color = new Color(1f, 0.82f, 0.28f);
        badgeText.alignment = TextAnchor.MiddleCenter;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(80f, 12f);
        titleRect.sizeDelta = new Vector2(-96f, 24f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = title;
        titleText.font = _uiFont;
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.95f, 0.97f, 1f);
        titleText.alignment = TextAnchor.MiddleLeft;

        // Description
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(cardObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0.5f);
        descRect.anchorMax = new Vector2(1f, 0.5f);
        descRect.pivot = new Vector2(0f, 0.5f);
        descRect.anchoredPosition = new Vector2(80f, -12f);
        descRect.sizeDelta = new Vector2(-96f, 22f);

        Text descText = descObj.AddComponent<Text>();
        descText.text = description;
        descText.font = _uiFont;
        descText.fontSize = 13;
        descText.color = new Color(0.68f, 0.74f, 0.82f);
        descText.alignment = TextAnchor.MiddleLeft;
    }

    private void CreateToastNotification()
    {
        _toastNotificationObj = new GameObject("ToastNotification");
        _toastNotificationObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform rt = _toastNotificationObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -30f);
        rt.sizeDelta = new Vector2(520f, 64f);

        Image img = _toastNotificationObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);

        Outline outline = _toastNotificationObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.35f, 0.35f, 1f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject textObj = new GameObject("ToastText");
        textObj.transform.SetParent(_toastNotificationObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        _toastNotificationText = textObj.AddComponent<Text>();
        _toastNotificationText.text = "";
        _toastNotificationText.font = _uiFont;
        _toastNotificationText.fontSize = 14;
        _toastNotificationText.color = new Color(0.95f, 0.95f, 0.95f);
        _toastNotificationText.alignment = TextAnchor.MiddleCenter;
    }

    // -----------------------------------------------------------------------
    // UI Helpers: Button, Slider, Toggle
    // -----------------------------------------------------------------------

    private Button CreateStyledButton(Transform parent, string label, System.Action onClick, Vector2 size, bool isHeroPlay, out Text btnLabelText)
    {
        GameObject btnObj = new GameObject("Btn_" + label.Replace(" ", ""));
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = size;

        LayoutElement layout = btnObj.AddComponent<LayoutElement>();
        layout.minWidth = size.x;
        layout.minHeight = size.y;
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = isHeroPlay ? new Color(0.12f, 0.20f, 0.16f, 0.96f) : new Color(0.10f, 0.13f, 0.18f, 0.96f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = isHeroPlay ? new Color(0.32f, 0.72f, 0.42f, 0.9f) : new Color(0.28f, 0.36f, 0.46f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = btnBg;

        ColorBlock colors = button.colors;
        if (isHeroPlay)
        {
            colors.normalColor = new Color(0.12f, 0.22f, 0.16f, 0.96f);
            colors.highlightedColor = new Color(0.20f, 0.45f, 0.28f, 1f);
            colors.pressedColor = new Color(0.08f, 0.14f, 0.10f, 1f);
        }
        else
        {
            colors.normalColor = new Color(0.10f, 0.13f, 0.18f, 0.96f);
            colors.highlightedColor = new Color(0.20f, 0.28f, 0.38f, 1f);
            colors.pressedColor = new Color(0.06f, 0.08f, 0.12f, 1f);
        }
        colors.disabledColor = new Color(0.06f, 0.08f, 0.10f, 0.6f);
        button.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = _uiFont;
        text.fontSize = isHeroPlay ? 18 : 15;
        text.fontStyle = FontStyle.Bold;
        text.color = isHeroPlay ? new Color(0.92f, 1f, 0.92f) : new Color(0.88f, 0.91f, 0.96f);
        text.alignment = TextAnchor.MiddleCenter;

        button.onClick.AddListener(() => onClick?.Invoke());

        btnLabelText = text;
        return button;
    }

    private Slider CreateSliderRow(Transform parent, string titleText, float minVal, float maxVal, float initialVal, string formatSuffix, System.Action<float> onValueChanged, out Text valueLabel)
    {
        GameObject rowObj = new GameObject(titleText.Replace(" ", "") + "_Row");
        rowObj.transform.SetParent(parent, false);

        LayoutElement rowLayout = rowObj.AddComponent<LayoutElement>();
        rowLayout.minHeight = 56f;
        rowLayout.preferredHeight = 56f;

        VerticalLayoutGroup rowVlg = rowObj.AddComponent<VerticalLayoutGroup>();
        rowVlg.spacing = 6f;
        rowVlg.childAlignment = TextAnchor.UpperLeft;
        rowVlg.childControlWidth = true;
        rowVlg.childControlHeight = false;
        rowVlg.childForceExpandWidth = true;
        rowVlg.childForceExpandHeight = false;

        // Label Header Row
        GameObject labelRow = new GameObject("LabelRow");
        labelRow.transform.SetParent(rowObj.transform, false);

        HorizontalLayoutGroup hlg = labelRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(labelRow.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(320f, 22f);
        Text titleLbl = titleObj.AddComponent<Text>();
        titleLbl.text = titleText;
        titleLbl.font = _uiFont;
        titleLbl.fontSize = 14;
        titleLbl.color = new Color(0.85f, 0.88f, 0.94f);
        titleLbl.alignment = TextAnchor.MiddleLeft;

        System.Func<float, string> formatValue = (v) =>
        {
            if (formatSuffix == "%")
            {
                return Mathf.RoundToInt(v * 100f) + "%";
            }
            if (!string.IsNullOrEmpty(formatSuffix))
            {
                return Mathf.RoundToInt(v) + formatSuffix;
            }
            return v.ToString("F2");
        };

        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(labelRow.transform, false);
        RectTransform valRect = valObj.AddComponent<RectTransform>();
        valRect.sizeDelta = new Vector2(140f, 22f);
        Text valText = valObj.AddComponent<Text>();
        valText.text = formatValue(initialVal);
        valText.font = _uiFont;
        valText.fontSize = 14;
        valText.fontStyle = FontStyle.Bold;
        valText.color = new Color(1f, 0.82f, 0.25f);
        valText.alignment = TextAnchor.MiddleRight;
        valueLabel = valText;

        // Slider Component
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(rowObj.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(460f, 24f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = minVal;
        slider.maxValue = maxVal;
        slider.value = initialVal;

        // Background Track
        GameObject bgTrack = new GameObject("Background");
        bgTrack.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgTrack.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.35f);
        bgRect.anchorMax = new Vector2(1f, 0.65f);
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgTrack.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.15f, 0.20f, 1f);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRect.sizeDelta = new Vector2(-16f, 0f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.28f, 0.58f, 0.88f, 1f);
        fillImage.raycastTarget = false;

        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.sizeDelta = new Vector2(-16f, 0f);

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);
        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 0f);
        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;
        handleImage.raycastTarget = true;

        Outline handleOutline = handleObj.AddComponent<Outline>();
        handleOutline.effectColor = new Color(0.1f, 0.12f, 0.16f, 1f);
        handleOutline.effectDistance = new Vector2(1f, 1f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;

        Text capturedLabel = valText;
        slider.onValueChanged.AddListener((val) =>
        {
            if (capturedLabel != null)
            {
                capturedLabel.text = formatValue(val);
            }
            onValueChanged?.Invoke(val);
        });

        return slider;
    }

    private Toggle CreateToggleRow(Transform parent, string titleText, bool initialValue, System.Action<bool> onValueChanged, out Text statusLabel)
    {
        GameObject rowObj = new GameObject(titleText.Replace(" ", "") + "_Row");
        rowObj.transform.SetParent(parent, false);

        LayoutElement rowLayout = rowObj.AddComponent<LayoutElement>();
        rowLayout.minHeight = 44f;
        rowLayout.preferredHeight = 44f;

        HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 16f;

        // Toggle Box
        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(rowObj.transform, false);
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(26f, 26f);

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        Image toggleBg = toggleObj.AddComponent<Image>();
        toggleBg.color = new Color(0.12f, 0.15f, 0.20f, 1f);
        toggleBg.raycastTarget = true;

        Outline bgOutline = toggleObj.AddComponent<Outline>();
        bgOutline.effectColor = new Color(0.35f, 0.42f, 0.52f, 1f);
        bgOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // Checkmark
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(toggleObj.transform, false);
        RectTransform checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkRect.sizeDelta = Vector2.zero;
        Image checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(1f, 0.82f, 0.25f, 1f);
        checkImage.raycastTarget = false;

        toggle.graphic = checkImage;
        toggle.targetGraphic = toggleBg;
        toggle.isOn = initialValue;

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(240f, 30f);
        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = titleText;
        labelText.font = _uiFont;
        labelText.fontSize = 14;
        labelText.color = new Color(0.85f, 0.88f, 0.94f);
        labelText.alignment = TextAnchor.MiddleLeft;

        // Status Badge
        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(rowObj.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(140f, 30f);
        Text statText = statusObj.AddComponent<Text>();
        statText.font = _uiFont;
        statText.fontSize = 14;
        statText.fontStyle = FontStyle.Bold;
        statText.color = initialValue ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.6f, 0.65f, 0.72f);
        statText.text = initialValue ? "Fullscreen" : "Windowed";
        statText.alignment = TextAnchor.MiddleRight;
        statusLabel = statText;

        Text capturedStat = statText;
        toggle.onValueChanged.AddListener((isOn) =>
        {
            if (capturedStat != null)
            {
                capturedStat.text = isOn ? "Fullscreen" : "Windowed";
                capturedStat.color = isOn ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.6f, 0.65f, 0.72f);
            }
            onValueChanged?.Invoke(isOn);
        });

        return toggle;
    }
}
