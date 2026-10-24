using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the player XP and Level HUD in the bottom-right corner of the screen,
/// including a progress bar and animated Level-Up banner notification.
///
/// Features:
/// - Fixed HUD in bottom-right corner: displays current level (e.g. "LEVEL 1"),
///   current XP and XP required for next level (e.g. "XP 25 / 100"), and an animated fill bar.
/// - Matches project theme: dark semi-transparent card with crisp border and vibrant emerald bar.
/// - Positioned above the hotbar in the bottom-right corner with a clear vertical gap, zero overlap.
/// - Responsive CanvasScaler (1280x720 reference) keeping HUD inside screen bounds at all aspect ratios.
/// - Animated "LEVEL UP!" toast notification with procedural celebratory chime that auto-dismisses.
/// - Hides during player death screen and restores upon respawn.
/// </summary>
public class PlayerExperienceUI : MonoBehaviour
{
    public static PlayerExperienceUI Instance { get; private set; }

    private GameObject _canvasObj;
    private GameObject _hudPanel;
    private Text _levelText;
    private Text _xpText;
    private RectTransform _xpBarFillRect;
    private Image _xpBarFillImage;

    // Level-Up Notification Banner
    private GameObject _levelUpBannerObj;
    private CanvasGroup _levelUpBannerCanvasGroup;
    private Text _levelUpTitleText;
    private Text _levelUpSubtitleText;
    private float _notificationTimer;
    private const float NotificationDuration = 2.8f;

    // Audio chime for level-up celebration
    private AudioSource _audioSource;
    private AudioClip _chimeClip;

    private Font _uiFont;
    private bool _initialized;
    private const float XPBarTotalWidth = 206f;

    private readonly Color _barColor = new Color(0.20f, 0.88f, 0.38f); // Vibrant Emerald Green

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

        EnsureEventSystem();
        InitializeUI();
        CreateChimeClip();
    }

    private void Start()
    {
        HookEvents();
    }

    private void OnDestroy()
    {
        UnhookEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // Animate Level-Up Banner fade-in and fade-out using unscaledDeltaTime
        if (_notificationTimer > 0f)
        {
            _notificationTimer -= Time.unscaledDeltaTime;
            float elapsed = NotificationDuration - _notificationTimer;

            float alpha = 1f;
            if (elapsed < 0.25f)
            {
                alpha = Mathf.Clamp01(elapsed / 0.25f);
            }
            else if (_notificationTimer < 0.6f)
            {
                alpha = Mathf.Clamp01(_notificationTimer / 0.6f);
            }

            if (_levelUpBannerCanvasGroup != null)
            {
                _levelUpBannerCanvasGroup.alpha = alpha;
            }

            if (_notificationTimer <= 0f && _levelUpBannerObj != null)
            {
                _levelUpBannerObj.SetActive(false);
            }
        }
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

    private void HookEvents()
    {
        if (PlayerExperience.Instance != null)
        {
            PlayerExperience.Instance.OnXPChanged += HandleXPChanged;
            PlayerExperience.Instance.OnLevelUp += HandleLevelUp;

            // Sync current state immediately
            HandleXPChanged(PlayerExperience.Instance.CurrentXP, PlayerExperience.Instance.XPForNextLevel, PlayerExperience.Instance.CurrentLevel);
        }

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnPlayerDied += HandlePlayerDied;
            PlayerHealth.Instance.OnPlayerRespawned += HandlePlayerRespawned;
        }
    }

    private void UnhookEvents()
    {
        if (PlayerExperience.Instance != null)
        {
            PlayerExperience.Instance.OnXPChanged -= HandleXPChanged;
            PlayerExperience.Instance.OnLevelUp -= HandleLevelUp;
        }

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnPlayerDied -= HandlePlayerDied;
            PlayerHealth.Instance.OnPlayerRespawned -= HandlePlayerRespawned;
        }
    }

    private void InitializeUI()
    {
        if (_initialized) return;

        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        CreateCanvas();
        CreateExperienceHUD();
        CreateLevelUpBanner();

        _initialized = true;
    }

    private void CreateCanvas()
    {
        _canvasObj = new GameObject("PlayerExperienceCanvas");
        Canvas canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25; // Matches PlayerHealthCanvas (25), above Hotbar (20) and Minimap (15)

        CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObj.AddComponent<GraphicRaycaster>();

        _audioSource = _canvasObj.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D UI sound
    }

    private void CreateExperienceHUD()
    {
        // HUD Container Panel (Bottom-Right Corner)
        _hudPanel = new GameObject("ExperienceHUDPanel");
        _hudPanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _hudPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 108f); // Positioned cleanly above hotbar (top at 96) with 12-unit vertical gap
        panelRect.sizeDelta = new Vector2(230f, 68f);

        Image panelBg = _hudPanel.AddComponent<Image>();
        panelBg.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);

        Outline outline = _hudPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.35f, 0.45f, 1f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Header Title (Left): "LEVEL 1"
        GameObject levelObj = new GameObject("LevelText");
        levelObj.transform.SetParent(_hudPanel.transform, false);
        RectTransform levelRect = levelObj.AddComponent<RectTransform>();
        levelRect.anchorMin = new Vector2(0f, 1f);
        levelRect.anchorMax = new Vector2(0f, 1f);
        levelRect.pivot = new Vector2(0f, 1f);
        levelRect.anchoredPosition = new Vector2(12f, -10f);
        levelRect.sizeDelta = new Vector2(105f, 22f);

        _levelText = levelObj.AddComponent<Text>();
        _levelText.text = "LEVEL 1";
        _levelText.font = _uiFont;
        _levelText.fontSize = 12;
        _levelText.fontStyle = FontStyle.Bold;
        _levelText.color = new Color(0.35f, 0.85f, 1.0f); // Bright Cyan
        _levelText.alignment = TextAnchor.MiddleLeft;

        // Numerical Value (Right): "XP 0 / 100"
        GameObject xpObj = new GameObject("XPText");
        xpObj.transform.SetParent(_hudPanel.transform, false);
        RectTransform xpRect = xpObj.AddComponent<RectTransform>();
        xpRect.anchorMin = new Vector2(1f, 1f);
        xpRect.anchorMax = new Vector2(1f, 1f);
        xpRect.pivot = new Vector2(1f, 1f);
        xpRect.anchoredPosition = new Vector2(-12f, -10f);
        xpRect.sizeDelta = new Vector2(105f, 22f);

        _xpText = xpObj.AddComponent<Text>();
        _xpText.text = "XP 0 / 100";
        _xpText.font = _uiFont;
        _xpText.fontSize = 12;
        _xpText.fontStyle = FontStyle.Bold;
        _xpText.color = new Color(1f, 0.85f, 0.30f); // Warm Gold
        _xpText.alignment = TextAnchor.MiddleRight;

        // Progress Bar Track (Dark background)
        GameObject trackObj = new GameObject("XPBarTrack");
        trackObj.transform.SetParent(_hudPanel.transform, false);
        RectTransform trackRect = trackObj.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0f);
        trackRect.anchorMax = new Vector2(0.5f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.anchoredPosition = new Vector2(0f, 12f);
        trackRect.sizeDelta = new Vector2(XPBarTotalWidth, 18f);

        Image trackBg = trackObj.AddComponent<Image>();
        trackBg.color = new Color(0.06f, 0.09f, 0.07f, 0.95f);

        Outline trackOutline = trackObj.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.18f, 0.24f, 0.22f, 1f);
        trackOutline.effectDistance = new Vector2(1f, 1f);

        // Progress Bar Fill
        GameObject fillObj = new GameObject("XPBarFill");
        fillObj.transform.SetParent(trackObj.transform, false);
        _xpBarFillRect = fillObj.AddComponent<RectTransform>();
        _xpBarFillRect.anchorMin = new Vector2(0f, 0f);
        _xpBarFillRect.anchorMax = new Vector2(0f, 1f);
        _xpBarFillRect.pivot = new Vector2(0f, 0.5f);
        _xpBarFillRect.anchoredPosition = Vector2.zero;
        _xpBarFillRect.sizeDelta = new Vector2(0f, 0f);

        _xpBarFillImage = fillObj.AddComponent<Image>();
        _xpBarFillImage.color = _barColor;
    }

    private void CreateLevelUpBanner()
    {
        _levelUpBannerObj = new GameObject("LevelUpNotification");
        _levelUpBannerObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform bannerRect = _levelUpBannerObj.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.anchoredPosition = new Vector2(0f, 130f);
        bannerRect.sizeDelta = new Vector2(320f, 74f);

        Image bannerBg = _levelUpBannerObj.AddComponent<Image>();
        bannerBg.color = new Color(0.04f, 0.07f, 0.10f, 0.94f);
        bannerBg.raycastTarget = false;

        Outline bannerOutline = _levelUpBannerObj.AddComponent<Outline>();
        bannerOutline.effectColor = new Color(1f, 0.82f, 0.20f, 1f); // Radiant Gold
        bannerOutline.effectDistance = new Vector2(2f, 2f);

        _levelUpBannerCanvasGroup = _levelUpBannerObj.AddComponent<CanvasGroup>();
        _levelUpBannerCanvasGroup.blocksRaycasts = false;
        _levelUpBannerCanvasGroup.interactable = false;

        // Title: "LEVEL UP!"
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_levelUpBannerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);
        titleRect.sizeDelta = new Vector2(0f, 30f);

        _levelUpTitleText = titleObj.AddComponent<Text>();
        _levelUpTitleText.text = "LEVEL UP!";
        _levelUpTitleText.font = _uiFont;
        _levelUpTitleText.fontSize = 22;
        _levelUpTitleText.fontStyle = FontStyle.Bold;
        _levelUpTitleText.color = new Color(1f, 0.85f, 0.20f);
        _levelUpTitleText.alignment = TextAnchor.MiddleCenter;
        _levelUpTitleText.raycastTarget = false;

        // Subtitle: "Reached Level 2"
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(_levelUpBannerObj.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0f);
        subRect.pivot = new Vector2(0.5f, 0f);
        subRect.anchoredPosition = new Vector2(0f, 10f);
        subRect.sizeDelta = new Vector2(0f, 24f);

        _levelUpSubtitleText = subObj.AddComponent<Text>();
        _levelUpSubtitleText.text = "Reached Level 2";
        _levelUpSubtitleText.font = _uiFont;
        _levelUpSubtitleText.fontSize = 13;
        _levelUpSubtitleText.fontStyle = FontStyle.Bold;
        _levelUpSubtitleText.color = new Color(0.40f, 0.90f, 1.0f);
        _levelUpSubtitleText.alignment = TextAnchor.MiddleCenter;
        _levelUpSubtitleText.raycastTarget = false;

        _levelUpBannerObj.SetActive(false);
    }

    private void CreateChimeClip()
    {
        int sampleRate = 44100;
        float duration = 0.55f;
        int sampleCount = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // Ascending C5-E5-G5 triad chord with smooth exponential decay
            float envelope = Mathf.Exp(-t * 5.5f);
            float note1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t);
            float note2 = t > 0.08f ? Mathf.Sin(2f * Mathf.PI * 659.25f * (t - 0.08f)) * Mathf.Exp(-(t - 0.08f) * 5.0f) : 0f;
            float note3 = t > 0.16f ? Mathf.Sin(2f * Mathf.PI * 783.99f * (t - 0.16f)) * Mathf.Exp(-(t - 0.16f) * 4.5f) : 0f;

            samples[i] = (note1 * 0.4f * envelope + note2 * 0.45f + note3 * 0.55f) * 0.35f;
        }

        _chimeClip = AudioClip.Create("LevelUpChime", sampleCount, 1, sampleRate, false);
        _chimeClip.SetData(samples, 0);
    }

    private void HandleXPChanged(int currentXP, int xpForNextLevel, int currentLevel)
    {
        float fraction = xpForNextLevel > 0 ? Mathf.Clamp01((float)currentXP / xpForNextLevel) : 0f;

        if (_levelText != null)
        {
            _levelText.text = "LEVEL " + currentLevel;
        }

        if (_xpText != null)
        {
            _xpText.text = "XP " + currentXP + " / " + xpForNextLevel;
        }

        if (_xpBarFillRect != null)
        {
            _xpBarFillRect.sizeDelta = new Vector2(XPBarTotalWidth * fraction, 0f);
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        if (_levelUpSubtitleText != null)
        {
            _levelUpSubtitleText.text = "Reached Level " + newLevel;
        }

        if (_levelUpBannerObj != null)
        {
            _levelUpBannerObj.SetActive(true);
            _notificationTimer = NotificationDuration;
            if (_levelUpBannerCanvasGroup != null)
            {
                _levelUpBannerCanvasGroup.alpha = 0f;
            }
        }

        if (_audioSource != null && _chimeClip != null)
        {
            _audioSource.PlayOneShot(_chimeClip, 0.75f);
        }
    }

    private void HandlePlayerDied()
    {
        if (_hudPanel != null) _hudPanel.SetActive(false);
        if (_levelUpBannerObj != null) _levelUpBannerObj.SetActive(false);
    }

    private void HandlePlayerRespawned()
    {
        if (_hudPanel != null) _hudPanel.SetActive(true);
    }
}
