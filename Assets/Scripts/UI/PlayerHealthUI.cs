using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the player health HUD, damage feedback flash, and death screen.
///
/// Features:
/// - Health HUD at bottom-left showing current/max HP and a responsive, color-coded health bar.
///   (Green when healthy, Amber when wounded, Red with warning pulse when critical).
/// - Damage feedback: subtle red screen flash that smoothly fades out.
/// - Death Screen: "YOU DIED" modal panel with clickable "RESPAWN" button.
/// - EventSystem self-verification for new Input System compatibility.
/// - Preserves hotbar and minimap layouts without overlap.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    public static PlayerHealthUI Instance { get; private set; }

    private GameObject _canvasObj;
    private GameObject _hudPanel;
    private RectTransform _healthBarFillRect;
    private Image _healthBarFillImage;
    private Text _healthValueText;

    private Image _damageFlashImage;
    private float _flashAlpha;

    private GameObject _deathScreenObj;
    private Font _uiFont;
    private bool _initialized;

    private readonly Color _colorHealthy = new Color(0.22f, 0.78f, 0.32f);
    private readonly Color _colorWounded = new Color(0.95f, 0.68f, 0.15f);
    private readonly Color _colorCritical = new Color(0.90f, 0.18f, 0.18f);

    private const float HealthBarTotalWidth = 206f;

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
    }

    private void Start()
    {
        HookPlayerHealthEvents();
    }

    private void OnDestroy()
    {
        UnhookPlayerHealthEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // 1. Smoothly fade out the damage flash overlay using unscaledDeltaTime
        if (_flashAlpha > 0f)
        {
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, Time.unscaledDeltaTime * 1.6f);
            if (_damageFlashImage != null)
            {
                _damageFlashImage.color = new Color(0.85f, 0.08f, 0.08f, _flashAlpha);
            }
        }

        // 2. Pulse critical health bar when below 25%
        if (PlayerHealth.Instance != null && !PlayerHealth.Instance.IsDead && PlayerHealth.Instance.HealthFraction <= 0.25f)
        {
            if (_healthBarFillImage != null)
            {
                float pulse = 0.75f + Mathf.PingPong(Time.unscaledTime * 3f, 0.25f);
                _healthBarFillImage.color = new Color(_colorCritical.r * pulse, _colorCritical.g, _colorCritical.b, 1f);
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

    private void HookPlayerHealthEvents()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnHealthChanged += HandleHealthChanged;
            PlayerHealth.Instance.OnDamageTaken += HandleDamageTaken;
            PlayerHealth.Instance.OnPlayerDied += HandlePlayerDied;
            PlayerHealth.Instance.OnPlayerRespawned += HandlePlayerRespawned;

            // Sync current state
            HandleHealthChanged(PlayerHealth.Instance.CurrentHealth, PlayerHealth.Instance.MaxHealth);
        }
    }

    private void UnhookPlayerHealthEvents()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnHealthChanged -= HandleHealthChanged;
            PlayerHealth.Instance.OnDamageTaken -= HandleDamageTaken;
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
        CreateDamageFlash();
        CreateHealthHUD();
        CreateDeathScreen();

        _initialized = true;
    }

    private void CreateCanvas()
    {
        _canvasObj = new GameObject("PlayerHealthCanvas");
        Canvas canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25; // Above Hotbar (20) and Minimap (15), below Pause Menu (100)

        CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObj.AddComponent<GraphicRaycaster>();
    }

    private void CreateDamageFlash()
    {
        GameObject flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform rt = flashObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        _damageFlashImage = flashObj.AddComponent<Image>();
        _damageFlashImage.color = new Color(0.85f, 0.08f, 0.08f, 0f);
        _damageFlashImage.raycastTarget = false; // Never block mouse interaction
    }

    private void CreateHealthHUD()
    {
        // HUD Container Panel (Bottom-Left)
        _hudPanel = new GameObject("HealthHUDPanel");
        _hudPanel.transform.SetParent(_canvasObj.transform, false);

        RectTransform panelRect = _hudPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(24f, 28f);
        panelRect.sizeDelta = new Vector2(230f, 68f);

        Image panelBg = _hudPanel.AddComponent<Image>();
        panelBg.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);

        Outline outline = _hudPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.32f, 0.38f, 1f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Header Title: "HEALTH"
        GameObject labelObj = new GameObject("HealthLabel");
        labelObj.transform.SetParent(_hudPanel.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(12f, -10f);
        labelRect.sizeDelta = new Vector2(100f, 22f);

        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = "+ HEALTH";
        labelText.font = _uiFont;
        labelText.fontSize = 12;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = new Color(0.95f, 0.95f, 0.95f);
        labelText.alignment = TextAnchor.MiddleLeft;

        // Numerical Value: "100 / 100"
        GameObject valueObj = new GameObject("HealthValue");
        valueObj.transform.SetParent(_hudPanel.transform, false);
        RectTransform valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(1f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = new Vector2(-12f, -10f);
        valueRect.sizeDelta = new Vector2(100f, 22f);

        _healthValueText = valueObj.AddComponent<Text>();
        _healthValueText.text = "100 / 100";
        _healthValueText.font = _uiFont;
        _healthValueText.fontSize = 13;
        _healthValueText.fontStyle = FontStyle.Bold;
        _healthValueText.color = new Color(1f, 0.84f, 0.25f);
        _healthValueText.alignment = TextAnchor.MiddleRight;

        // Health Bar Track (Dark background)
        GameObject trackObj = new GameObject("HealthBarTrack");
        trackObj.transform.SetParent(_hudPanel.transform, false);
        RectTransform trackRect = trackObj.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0f);
        trackRect.anchorMax = new Vector2(0.5f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.anchoredPosition = new Vector2(0f, 12f);
        trackRect.sizeDelta = new Vector2(HealthBarTotalWidth, 18f);

        Image trackBg = trackObj.AddComponent<Image>();
        trackBg.color = new Color(0.12f, 0.05f, 0.05f, 0.95f);

        Outline trackOutline = trackObj.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        trackOutline.effectDistance = new Vector2(1f, 1f);

        // Health Bar Fill
        GameObject fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(trackObj.transform, false);
        _healthBarFillRect = fillObj.AddComponent<RectTransform>();
        _healthBarFillRect.anchorMin = new Vector2(0f, 0f);
        _healthBarFillRect.anchorMax = new Vector2(0f, 1f);
        _healthBarFillRect.pivot = new Vector2(0f, 0.5f);
        _healthBarFillRect.anchoredPosition = Vector2.zero;
        _healthBarFillRect.sizeDelta = new Vector2(HealthBarTotalWidth, 0f);

        _healthBarFillImage = fillObj.AddComponent<Image>();
        _healthBarFillImage.color = _colorHealthy;
    }

    private void CreateDeathScreen()
    {
        _deathScreenObj = new GameObject("DeathScreen");
        _deathScreenObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform rootRect = _deathScreenObj.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Dark red/black tinted full-screen backdrop
        Image backdrop = _deathScreenObj.AddComponent<Image>();
        backdrop.color = new Color(0.08f, 0.02f, 0.02f, 0.88f);

        // Central Modal Box
        GameObject dialogBox = new GameObject("DeathDialog");
        dialogBox.transform.SetParent(_deathScreenObj.transform, false);
        RectTransform dialogRect = dialogBox.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(420f, 260f);

        Image dialogBg = dialogBox.AddComponent<Image>();
        dialogBg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

        Outline dialogOutline = dialogBox.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.70f, 0.18f, 0.18f, 1f);
        dialogOutline.effectDistance = new Vector2(2f, 2f);

        // Header Title: "YOU DIED"
        GameObject titleObj = new GameObject("DeathTitle");
        titleObj.transform.SetParent(dialogBox.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(0f, 48f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "YOU DIED";
        titleText.font = _uiFont;
        titleText.fontSize = 42;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.92f, 0.18f, 0.18f);
        titleText.alignment = TextAnchor.MiddleCenter;

        // Subtitle: "You were slain by a Zombie."
        GameObject subtitleObj = new GameObject("DeathSubtitle");
        subtitleObj.transform.SetParent(dialogBox.transform, false);
        RectTransform subRect = subtitleObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 1f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -80f);
        subRect.sizeDelta = new Vector2(0f, 28f);

        Text subText = subtitleObj.AddComponent<Text>();
        subText.text = "You were slain by a creature of the night.";
        subText.font = _uiFont;
        subText.fontSize = 15;
        subText.color = new Color(0.75f, 0.78f, 0.82f);
        subText.alignment = TextAnchor.MiddleCenter;

        // RESPAWN Button
        CreateRespawnButton(dialogBox.transform);

        // Hide death screen initially
        _deathScreenObj.SetActive(false);
    }

    private void CreateRespawnButton(Transform parent)
    {
        GameObject btnObj = new GameObject("RespawnButton");
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 34f);
        btnRect.sizeDelta = new Vector2(180f, 46f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.14f, 0.18f, 0.24f, 1f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.42f, 0.52f, 1f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.14f, 0.18f, 0.24f, 1f);
        colors.highlightedColor = new Color(0.24f, 0.32f, 0.44f, 1f);
        colors.pressedColor = new Color(0.10f, 0.12f, 0.16f, 1f);
        colors.selectedColor = new Color(0.18f, 0.24f, 0.32f, 1f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = "RESPAWN";
        btnText.font = _uiFont;
        btnText.fontSize = 16;
        btnText.fontStyle = FontStyle.Bold;
        btnText.color = new Color(1f, 0.84f, 0.25f);
        btnText.alignment = TextAnchor.MiddleCenter;

        btn.onClick.AddListener(OnRespawnClicked);
    }

    private void OnRespawnClicked()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.Respawn();
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        float fraction = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        // 1. Update text
        if (_healthValueText != null)
        {
            _healthValueText.text = current + " / " + max;
        }

        // 2. Update health bar width
        if (_healthBarFillRect != null)
        {
            _healthBarFillRect.sizeDelta = new Vector2(HealthBarTotalWidth * fraction, 0f);
        }

        // 3. Update health bar color
        if (_healthBarFillImage != null)
        {
            if (fraction > 0.50f)
            {
                _healthBarFillImage.color = _colorHealthy;
            }
            else if (fraction > 0.25f)
            {
                _healthBarFillImage.color = _colorWounded;
            }
            else
            {
                _healthBarFillImage.color = _colorCritical;
            }
        }
    }

    private void HandleDamageTaken(int amount)
    {
        // Trigger red screen flash
        _flashAlpha = 0.36f;
        if (_damageFlashImage != null)
        {
            _damageFlashImage.color = new Color(0.85f, 0.08f, 0.08f, _flashAlpha);
        }
    }

    private void HandlePlayerDied()
    {
        // Display death screen and hide HUD
        if (_deathScreenObj != null) _deathScreenObj.SetActive(true);
        if (_hudPanel != null) _hudPanel.SetActive(false);
    }

    private void HandlePlayerRespawned()
    {
        // Hide death screen, restore HUD, clear flash
        if (_deathScreenObj != null) _deathScreenObj.SetActive(false);
        if (_hudPanel != null) _hudPanel.SetActive(true);
        _flashAlpha = 0f;
        if (_damageFlashImage != null)
        {
            _damageFlashImage.color = new Color(0.85f, 0.08f, 0.08f, 0f);
        }
    }
}
