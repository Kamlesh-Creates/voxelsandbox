using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controls the Pause Menu and Settings system for Voxelsandbox.
///
/// Features:
/// - Press Escape during gameplay to pause the game, freeze time (timeScale = 0), unlock mouse, and display the Pause Menu.
/// - Resume button or Escape key resumes gameplay, restores timeScale = 1, and locks cursor back to first-person.
/// - Settings screen with live interactive sliders for Mouse Sensitivity, Master Volume, and Fullscreen toggle.
/// - PlayerPrefs persistence for all settings with safe clamping and startup initialization.
/// - Polished survival-sandbox UI aesthetic matching Hotbar and Minimap styling.
/// - Prevents duplicate frame transitions on Escape key press.
/// - Automatically disables player movement, camera look, block breaking/placement, and hotbar selection while paused.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public static bool IsPaused { get; private set; }
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

    // UI References
    private GameObject _canvasObj;
    private GameObject _overlayObj;
    private GameObject _pausePanel;
    private GameObject _settingsPanel;

    private Slider _fovSlider;
    private Slider _sensitivitySlider;
    private Slider _volumeSlider;
    private Toggle _fullscreenToggle;

    private Text _fovValueText;
    private Text _sensitivityValueText;
    private Text _volumeValueText;
    private Text _fullscreenStatusText;

    private Font _uiFont;
    private bool _initialized;

    public float CurrentSensitivity => currentSensitivity;
    public float CurrentVolume => currentVolume;
    public bool CurrentFullscreen => currentFullscreen;
    public float CurrentFOV => currentFOV;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        LoadSettings();
        InitializeUI();
    }

    private void Start()
    {
        ApplyAllSettings();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // If destroyed while paused, restore normal timeScale
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
            Instance = null;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            HandleEscapePress();
        }
    }

    private void HandleEscapePress()
    {
        // 0. Do not open or manipulate Pause Menu while player is dead (Death Screen is active)
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
        {
            return;
        }

        // If Quest panel is currently open or just closed this frame, consume Escape press (do not open Pause Menu)
        if (QuestUI.IsOpen || Time.frameCount == QuestUI.LastClosedFrame)
        {
            if (QuestUI.IsOpen && QuestUI.Instance != null)
            {
                QuestUI.Instance.CloseQuestPanel();
            }
            return;
        }

        // 1. If currently in Settings screen, return to Pause Menu (single transition)
        if (_settingsPanel != null && _settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }

        // 2. If in Pause Menu, resume gameplay
        if (IsPaused)
        {
            ResumeGame();
            return;
        }

        // 3. If in gameplay, open Pause Menu
        PauseGame();
    }

    public void PauseGame()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;

        // Unlock and display the mouse cursor for menu navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_canvasObj != null)
        {
            _canvasObj.SetActive(true);
        }

        if (_pausePanel != null) _pausePanel.SetActive(true);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        // Restore first-person cursor locking
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);

        if (_canvasObj != null)
        {
            _canvasObj.SetActive(false);
        }
    }

    public void OpenSettings()
    {
        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(true);

        RefreshSettingsUI();
    }

    public void CloseSettings()
    {
        SaveSettings();

        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_pausePanel != null) _pausePanel.SetActive(true);
    }

    public void QuitGame()
    {
        SaveSettings();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -----------------------------------------------------------------------
    // Settings Application & Persistence
    // -----------------------------------------------------------------------

    public void SetCameraFOV(float value)
    {
        currentFOV = Mathf.Clamp(value, MinFOV, MaxFOV);

        ApplyGameplayCameraFOV(currentFOV);

        if (_fovValueText != null)
        {
            _fovValueText.text = Mathf.RoundToInt(currentFOV) + "°";
        }

        PlayerPrefs.SetFloat(PrefKeyFOV, currentFOV);
    }

    public void ApplyGameplayCameraFOV(float fov)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.SetCameraFOV(fov);
        }
        else
        {
            GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (camObj != null)
            {
                Camera cam = camObj.GetComponent<Camera>();
                if (cam != null && !cam.name.Contains("Minimap") && !cam.name.Contains("Background"))
                {
                    cam.fieldOfView = Mathf.Clamp(fov, MinFOV, MaxFOV);
                }
            }
        }
    }

    public void SetMouseSensitivity(float value)
    {
        currentSensitivity = Mathf.Clamp(value, 0.02f, 0.50f);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.MouseSensitivity = currentSensitivity;
        }

        if (_sensitivityValueText != null)
        {
            _sensitivityValueText.text = currentSensitivity.ToString("F2");
        }

        PlayerPrefs.SetFloat(PrefKeySensitivity, currentSensitivity);
    }

    public void SetMasterVolume(float value)
    {
        currentVolume = Mathf.Clamp01(value);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.MasterVolume = currentVolume;
        }
        AudioListener.volume = currentVolume;

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

    // -----------------------------------------------------------------------
    // Procedural UI Construction
    // -----------------------------------------------------------------------

    private void InitializeUI()
    {
        if (_initialized) return;

        EnsureEventSystem();
        CreateCanvas();
        CreateDimmedOverlay();
        CreatePausePanel();
        CreateSettingsPanel();

        // Start hidden
        _pausePanel.SetActive(false);
        _settingsPanel.SetActive(false);
        _canvasObj.SetActive(false);

        _initialized = true;
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
        _canvasObj = new GameObject("PauseMenuCanvas");
        Canvas canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above Hotbar (20) and Minimap (15)

        CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObj.AddComponent<GraphicRaycaster>();

        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void CreateDimmedOverlay()
    {
        _overlayObj = new GameObject("DarkOverlay");
        _overlayObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform rt = _overlayObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image img = _overlayObj.AddComponent<Image>();
        img.color = new Color(0.02f, 0.04f, 0.07f, 0.82f);
    }

    private void CreatePausePanel()
    {
        _pausePanel = new GameObject("PausePanel");
        _pausePanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _pausePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 340f);

        Image panelBg = _pausePanel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);

        Outline outline = _pausePanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.35f, 0.45f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Header Title: "GAME PAUSED"
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(_pausePanel.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -22f);
        headerRect.sizeDelta = new Vector2(0f, 36f);

        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "GAME PAUSED";
        headerText.font = _uiFont;
        headerText.fontSize = 22;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(1f, 0.82f, 0.25f);
        headerText.alignment = TextAnchor.MiddleCenter;

        // Button Container
        GameObject btnContainer = new GameObject("ButtonContainer");
        btnContainer.transform.SetParent(_pausePanel.transform, false);
        RectTransform containerRect = btnContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.42f);
        containerRect.anchorMax = new Vector2(0.5f, 0.42f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(280f, 200f);

        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Buttons
        CreateButton(btnContainer.transform, "Resume", ResumeGame, new Vector2(260f, 46f));
        CreateButton(btnContainer.transform, "Settings", OpenSettings, new Vector2(260f, 46f));
        CreateButton(btnContainer.transform, "Quit Game", QuitGame, new Vector2(260f, 46f));
    }

    private void CreateSettingsPanel()
    {
        _settingsPanel = new GameObject("SettingsPanel");
        _settingsPanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _settingsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 490f);

        Image panelBg = _settingsPanel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);

        Outline outline = _settingsPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.35f, 0.45f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Header Title: "SETTINGS"
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(_settingsPanel.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -20f);
        headerRect.sizeDelta = new Vector2(0f, 36f);

        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "SETTINGS";
        headerText.font = _uiFont;
        headerText.fontSize = 22;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(1f, 0.82f, 0.25f);
        headerText.alignment = TextAnchor.MiddleCenter;

        // Content Container
        GameObject contentContainer = new GameObject("ContentContainer");
        contentContainer.transform.SetParent(_settingsPanel.transform, false);
        RectTransform containerRect = contentContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.46f);
        containerRect.anchorMax = new Vector2(0.5f, 0.46f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(380f, 320f);

        VerticalLayoutGroup vlg = contentContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 1. Camera FOV Slider Row (60° to 100°, default 80°)
        _fovSlider = CreateSliderRow(
            contentContainer.transform,
            "Field of View (FOV)",
            MinFOV, MaxFOV,
            currentFOV,
            "°",
            SetCameraFOV,
            out _fovValueText
        );

        // 2. Mouse Sensitivity Slider Row
        _sensitivitySlider = CreateSliderRow(
            contentContainer.transform,
            "Mouse Sensitivity",
            0.02f, 0.50f,
            currentSensitivity,
            null,
            SetMouseSensitivity,
            out _sensitivityValueText
        );

        // 3. Master Volume Slider Row
        _volumeSlider = CreateSliderRow(
            contentContainer.transform,
            "Master Volume",
            0.0f, 1.0f,
            currentVolume,
            "%",
            SetMasterVolume,
            out _volumeValueText
        );

        // 4. Fullscreen Toggle Row
        _fullscreenToggle = CreateToggleRow(
            contentContainer.transform,
            "Display Mode",
            currentFullscreen,
            SetFullscreen,
            out _fullscreenStatusText
        );

        // Back Button
        GameObject backBtnObj = new GameObject("BackContainer");
        backBtnObj.transform.SetParent(_settingsPanel.transform, false);
        RectTransform backRect = backBtnObj.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 24f);
        backRect.sizeDelta = new Vector2(240f, 44f);

        CreateButton(backBtnObj.transform, "Back", CloseSettings, new Vector2(240f, 44f));
    }

    private Slider CreateSliderRow(Transform parent, string titleText, float minValue, float maxValue, float initialValue, string formatSuffix, System.Action<float> onValueChanged, out Text valueLabel)
    {
        GameObject rowObj = new GameObject(titleText.Replace(" ", "") + "_Row");
        rowObj.transform.SetParent(parent, false);

        LayoutElement rowLayout = rowObj.AddComponent<LayoutElement>();
        rowLayout.minHeight = 58f;
        rowLayout.preferredHeight = 58f;

        // Title and Value Header
        GameObject headerObj = new GameObject("HeaderRow");
        headerObj.transform.SetParent(rowObj.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.55f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.sizeDelta = Vector2.zero;

        // Label (Left)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(headerObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.7f, 1f);
        labelRect.sizeDelta = Vector2.zero;
        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = titleText;
        labelText.font = _uiFont;
        labelText.fontSize = 14;
        labelText.color = new Color(0.85f, 0.88f, 0.94f);
        labelText.alignment = TextAnchor.MiddleLeft;

        // Value Badge (Right)
        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(headerObj.transform, false);
        RectTransform valRect = valObj.AddComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.7f, 0f);
        valRect.anchorMax = new Vector2(1f, 1f);
        valRect.sizeDelta = Vector2.zero;
        Text valText = valObj.AddComponent<Text>();
        valText.font = _uiFont;
        valText.fontSize = 14;
        valText.fontStyle = FontStyle.Bold;
        valText.color = new Color(1f, 0.82f, 0.25f);
        valText.alignment = TextAnchor.MiddleRight;
        if (formatSuffix == "%")
            valText.text = Mathf.RoundToInt(initialValue * 100f) + "%";
        else if (formatSuffix == "°")
            valText.text = Mathf.RoundToInt(initialValue) + "°";
        else
            valText.text = initialValue.ToString("F2");
        valueLabel = valText;

        // Slider
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(rowObj.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0.45f);
        sliderRect.sizeDelta = Vector2.zero;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = initialValue;

        // Track Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.15f, 0.20f, 1f);
        bgImage.raycastTarget = true;

        Outline bgOutline = bgObj.AddComponent<Outline>();
        bgOutline.effectColor = new Color(0.25f, 0.30f, 0.38f, 0.6f);
        bgOutline.effectDistance = new Vector2(1f, 1f);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
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
                if (formatSuffix == "%")
                    capturedLabel.text = Mathf.RoundToInt(val * 100f) + "%";
                else if (formatSuffix == "°")
                    capturedLabel.text = Mathf.RoundToInt(val) + "°";
                else
                    capturedLabel.text = val.ToString("F2");
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
        labelRect.sizeDelta = new Vector2(210f, 30f);
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
        statusRect.sizeDelta = new Vector2(110f, 30f);
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

    private Button CreateButton(Transform parent, string buttonText, System.Action onClick, Vector2 size)
    {
        GameObject btnObj = new GameObject("Button_" + buttonText.Replace(" ", ""));
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = size;

        LayoutElement layout = btnObj.AddComponent<LayoutElement>();
        layout.minWidth = size.x;
        layout.minHeight = size.y;
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.10f, 0.13f, 0.18f, 0.96f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.38f, 0.48f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = btnBg;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.10f, 0.13f, 0.18f, 0.96f);
        colors.highlightedColor = new Color(0.20f, 0.28f, 0.38f, 1f);
        colors.pressedColor = new Color(0.06f, 0.08f, 0.12f, 1f);
        colors.selectedColor = new Color(0.10f, 0.13f, 0.18f, 0.96f);
        colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.5f);
        button.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text label = textObj.AddComponent<Text>();
        label.text = buttonText;
        label.font = _uiFont;
        label.fontSize = 15;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.92f, 0.95f, 1.0f);
        label.alignment = TextAnchor.MiddleCenter;

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        return button;
    }
}
