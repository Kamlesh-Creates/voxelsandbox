using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controls the Quest & Achievement HUD and modal dialog panel.
/// 
/// Features:
/// - Modal dialog opened/closed with the 'J' key or 'ESC' key.
/// - Clearly distinguishes active and completed/claimed quests.
/// - Shows current progress (e.g. "7 / 10 Blocks Broken") and XP reward.
/// - Tabs between Quests and Achievements view.
/// - Unobtrusive, queued top-left notification toasts for achievement unlocks and quest completions.
/// - Synthesizes celebratory chimes when notifications display.
/// - Automatically closes and suppresses inputs when paused or during player death.
/// </summary>
public class QuestUI : MonoBehaviour
{
    public static QuestUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }
    public static int LastClosedFrame { get; private set; } = -1;

    private GameObject _canvasObj;
    private GameObject _questModalObj;
    private GameObject _questsViewObj;
    private GameObject _achievementsViewObj;

    // Tab buttons
    private Image _questsTabBg;
    private Image _achievementsTabBg;
    private Text _questsTabText;
    private Text _achievementsTabText;
    private int _currentTab = 0; // 0 = Quests, 1 = Achievements

    // Queued Notification Toast (Top-Left)
    private struct ToastData
    {
        public string Title;
        public string Subtitle;
        public string Description;
        public Color TitleColor;
        public Color BorderColor;
    }

    private readonly Queue<ToastData> _toastQueue = new Queue<ToastData>();
    private GameObject _toastObj;
    private CanvasGroup _toastCanvasGroup;
    private Outline _toastOutline;
    private Text _toastTitleText;
    private Text _toastSubtitleText;
    private Text _toastDescText;
    private float _toastTimer;
    private const float ToastDuration = 3.0f;

    // Audio
    private AudioSource _audioSource;
    private AudioClip _chimeClip;

    private Font _uiFont;
    private bool _initialized;

    private readonly List<GameObject> _questCardPool = new List<GameObject>();
    private readonly List<GameObject> _achievementCardPool = new List<GameObject>();

    private void Awake()
    {
        IsOpen = false;

        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeUI();
        CreateChimeClip();
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            CloseQuestPanel();
        }
    }

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnDataRefreshed += RefreshDisplay;
        }

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnPlayerDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnDataRefreshed -= RefreshDisplay;
        }

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnPlayerDied -= HandlePlayerDied;
        }

        if (Instance == this)
        {
            IsOpen = false;
            Instance = null;
        }
    }

    private void Update()
    {
        // 1. Process queued notification toasts
        UpdateToastAnimation();

        // 2. Keyboard inputs
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Escape key closes quest panel first if open (without opening pause menu)
        if (keyboard.escapeKey.wasPressedThisFrame && IsOpen)
        {
            CloseQuestPanel();
            return;
        }

        // 3. Keyboard toggle: J key opens/closes quest modal
        if (keyboard.jKey.wasPressedThisFrame)
        {
            // Do not open if game is paused or player is dead
            if (PauseMenuController.IsPaused || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead))
            {
                return;
            }

            if (IsOpen)
            {
                CloseQuestPanel();
            }
            else
            {
                OpenQuestPanel();
            }
        }
    }

    private void HandlePlayerDied()
    {
        if (IsOpen)
        {
            CloseQuestPanel();
        }
    }

    public void OpenQuestPanel()
    {
        if (PauseMenuController.IsPaused || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead))
        {
            return;
        }

        IsOpen = true;

        if (_questModalObj != null)
        {
            _questModalObj.SetActive(true);
        }

        // Unlock mouse cursor for UI navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshDisplay();
    }

    public void CloseQuestPanel()
    {
        IsOpen = false;
        LastClosedFrame = Time.frameCount;

        if (_questModalObj != null)
        {
            _questModalObj.SetActive(false);
        }

        // Restore first-person cursor locking if gameplay is active
        if (!PauseMenuController.IsPaused && (PlayerHealth.Instance == null || !PlayerHealth.Instance.IsDead))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void InitializeUI()
    {
        if (_initialized) return;

        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        CreateCanvas();
        CreateQuestModal();
        CreateToastUI();

        _initialized = true;
    }

    private void CreateCanvas()
    {
        _canvasObj = new GameObject("QuestCanvas");
        Canvas canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // Above normal HUDs (20-25), below Pause Menu (100)

        CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObj.AddComponent<GraphicRaycaster>();

        _audioSource = _canvasObj.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }

    private void CreateQuestModal()
    {
        // Fullscreen backdrop container that catches all clicks behind modal
        _questModalObj = new GameObject("QuestModalDialog");
        _questModalObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform backdropRect = _questModalObj.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.sizeDelta = Vector2.zero;

        Image backdropBg = _questModalObj.AddComponent<Image>();
        backdropBg.color = new Color(0f, 0f, 0f, 0.45f); // Subtle dark focus vignette
        backdropBg.raycastTarget = true;

        // Centered Dialog Card
        GameObject dialogCard = new GameObject("DialogCard");
        dialogCard.transform.SetParent(_questModalObj.transform, false);

        RectTransform modalRect = dialogCard.AddComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);
        modalRect.anchoredPosition = Vector2.zero;
        modalRect.sizeDelta = new Vector2(620f, 480f);

        Image modalBg = dialogCard.AddComponent<Image>();
        modalBg.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);

        Outline modalOutline = dialogCard.AddComponent<Outline>();
        modalOutline.effectColor = new Color(0.28f, 0.35f, 0.45f, 1f);
        modalOutline.effectDistance = new Vector2(2f, 2f);

        // Header Title: "QUESTS & ACHIEVEMENTS"
        GameObject headerObj = new GameObject("Title");
        headerObj.transform.SetParent(dialogCard.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -14f);
        headerRect.sizeDelta = new Vector2(-40f, 28f);

        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "QUESTS & ACHIEVEMENTS";
        headerText.font = _uiFont;
        headerText.fontSize = 18;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(0.40f, 0.88f, 1.0f); // Bright Cyan
        headerText.alignment = TextAnchor.MiddleLeft;

        // Subtitle / Controls Hint: "Press [J] or [ESC] to close"
        GameObject hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(dialogCard.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(0.5f, 1f);
        hintRect.anchoredPosition = new Vector2(0f, -42f);
        hintRect.sizeDelta = new Vector2(-40f, 18f);

        Text hintText = hintObj.AddComponent<Text>();
        hintText.text = "Press [J] or [ESC] to close panel";
        hintText.font = _uiFont;
        hintText.fontSize = 11;
        hintText.color = new Color(0.65f, 0.70f, 0.78f);
        hintText.alignment = TextAnchor.MiddleLeft;

        // Close Button [✕] in top-right
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(dialogCard.transform, false);
        RectTransform closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1f, 1f);
        closeBtnRect.anchorMax = new Vector2(1f, 1f);
        closeBtnRect.pivot = new Vector2(1f, 1f);
        closeBtnRect.anchoredPosition = new Vector2(-16f, -14f);
        closeBtnRect.sizeDelta = new Vector2(28f, 28f);

        Image closeBtnBg = closeBtnObj.AddComponent<Image>();
        closeBtnBg.color = new Color(0.12f, 0.16f, 0.22f, 1f);

        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseQuestPanel);

        GameObject closeTxtObj = new GameObject("CloseText");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTxtRect = closeTxtObj.AddComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;

        Text closeTxt = closeTxtObj.AddComponent<Text>();
        closeTxt.text = "✕";
        closeTxt.font = _uiFont;
        closeTxt.fontSize = 16;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.color = new Color(0.9f, 0.9f, 0.95f);
        closeTxt.alignment = TextAnchor.MiddleCenter;

        // Tab Navigation Container
        GameObject tabBarObj = new GameObject("TabBar");
        tabBarObj.transform.SetParent(dialogCard.transform, false);
        RectTransform tabRect = tabBarObj.AddComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 1f);
        tabRect.anchorMax = new Vector2(1f, 1f);
        tabRect.pivot = new Vector2(0.5f, 1f);
        tabRect.anchoredPosition = new Vector2(0f, -68f);
        tabRect.sizeDelta = new Vector2(-40f, 32f);

        // Tab 1: "QUESTS" Button
        GameObject tab1Obj = new GameObject("QuestsTab");
        tab1Obj.transform.SetParent(tabBarObj.transform, false);
        RectTransform tab1Rect = tab1Obj.AddComponent<RectTransform>();
        tab1Rect.anchorMin = new Vector2(0f, 0f);
        tab1Rect.anchorMax = new Vector2(0.5f, 1f);
        tab1Rect.offsetMin = new Vector2(0f, 0f);
        tab1Rect.offsetMax = new Vector2(-4f, 0f);

        _questsTabBg = tab1Obj.AddComponent<Image>();
        _questsTabBg.color = new Color(0.16f, 0.22f, 0.32f, 1f);

        Button tab1Btn = tab1Obj.AddComponent<Button>();
        tab1Btn.onClick.AddListener(() => SetTab(0));

        GameObject tab1TxtObj = new GameObject("Text");
        tab1TxtObj.transform.SetParent(tab1Obj.transform, false);
        RectTransform tab1TxtRect = tab1TxtObj.AddComponent<RectTransform>();
        tab1TxtRect.anchorMin = Vector2.zero;
        tab1TxtRect.anchorMax = Vector2.one;
        tab1TxtRect.sizeDelta = Vector2.zero;

        _questsTabText = tab1TxtObj.AddComponent<Text>();
        _questsTabText.text = "QUESTS (5)";
        _questsTabText.font = _uiFont;
        _questsTabText.fontSize = 13;
        _questsTabText.fontStyle = FontStyle.Bold;
        _questsTabText.color = Color.white;
        _questsTabText.alignment = TextAnchor.MiddleCenter;

        // Tab 2: "ACHIEVEMENTS" Button
        GameObject tab2Obj = new GameObject("AchievementsTab");
        tab2Obj.transform.SetParent(tabBarObj.transform, false);
        RectTransform tab2Rect = tab2Obj.AddComponent<RectTransform>();
        tab2Rect.anchorMin = new Vector2(0.5f, 0f);
        tab2Rect.anchorMax = new Vector2(1f, 1f);
        tab2Rect.offsetMin = new Vector2(4f, 0f);
        tab2Rect.offsetMax = new Vector2(0f, 0f);

        _achievementsTabBg = tab2Obj.AddComponent<Image>();
        _achievementsTabBg.color = new Color(0.08f, 0.10f, 0.14f, 1f);

        Button tab2Btn = tab2Obj.AddComponent<Button>();
        tab2Btn.onClick.AddListener(() => SetTab(1));

        GameObject tab2TxtObj = new GameObject("Text");
        tab2TxtObj.transform.SetParent(tab2Obj.transform, false);
        RectTransform tab2TxtRect = tab2TxtObj.AddComponent<RectTransform>();
        tab2TxtRect.anchorMin = Vector2.zero;
        tab2TxtRect.anchorMax = Vector2.one;
        tab2TxtRect.sizeDelta = Vector2.zero;

        _achievementsTabText = tab2TxtObj.AddComponent<Text>();
        _achievementsTabText.text = "ACHIEVEMENTS (4)";
        _achievementsTabText.font = _uiFont;
        _achievementsTabText.fontSize = 13;
        _achievementsTabText.fontStyle = FontStyle.Bold;
        _achievementsTabText.color = new Color(0.6f, 0.65f, 0.72f);
        _achievementsTabText.alignment = TextAnchor.MiddleCenter;

        // Content Area Container
        GameObject contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(dialogCard.transform, false);
        RectTransform contentRect = contentArea.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(20f, 20f);
        contentRect.offsetMax = new Vector2(-20f, -108f);

        // View 1: Quests List
        _questsViewObj = new GameObject("QuestsView");
        _questsViewObj.transform.SetParent(contentArea.transform, false);
        RectTransform qvRect = _questsViewObj.AddComponent<RectTransform>();
        qvRect.anchorMin = Vector2.zero;
        qvRect.anchorMax = Vector2.one;
        qvRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup qLayout = _questsViewObj.AddComponent<VerticalLayoutGroup>();
        qLayout.spacing = 6f;
        qLayout.childControlWidth = true;
        qLayout.childControlHeight = false;
        qLayout.childForceExpandWidth = true;
        qLayout.childForceExpandHeight = false;

        // View 2: Achievements List
        _achievementsViewObj = new GameObject("AchievementsView");
        _achievementsViewObj.transform.SetParent(contentArea.transform, false);
        RectTransform avRect = _achievementsViewObj.AddComponent<RectTransform>();
        avRect.anchorMin = Vector2.zero;
        avRect.anchorMax = Vector2.one;
        avRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup aLayout = _achievementsViewObj.AddComponent<VerticalLayoutGroup>();
        aLayout.spacing = 6f;
        aLayout.childControlWidth = true;
        aLayout.childControlHeight = false;
        aLayout.childForceExpandWidth = true;
        aLayout.childForceExpandHeight = false;

        _achievementsViewObj.SetActive(false);
        _questModalObj.SetActive(false);
    }

    private void SetTab(int tabIndex)
    {
        _currentTab = tabIndex;
        if (_currentTab == 0)
        {
            _questsViewObj.SetActive(true);
            _achievementsViewObj.SetActive(false);

            _questsTabBg.color = new Color(0.16f, 0.22f, 0.32f, 1f);
            _questsTabText.color = Color.white;

            _achievementsTabBg.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            _achievementsTabText.color = new Color(0.6f, 0.65f, 0.72f);
        }
        else
        {
            _questsViewObj.SetActive(false);
            _achievementsViewObj.SetActive(true);

            _achievementsTabBg.color = new Color(0.16f, 0.22f, 0.32f, 1f);
            _achievementsTabText.color = Color.white;

            _questsTabBg.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            _questsTabText.color = new Color(0.6f, 0.65f, 0.72f);
        }

        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (QuestManager.Instance == null) return;

        // 1. Refresh Quests
        IReadOnlyList<Quest> quests = QuestManager.Instance.Quests;
        for (int i = 0; i < quests.Count; i++)
        {
            Quest q = quests[i];
            GameObject cardObj;
            if (i < _questCardPool.Count)
            {
                cardObj = _questCardPool[i];
                cardObj.SetActive(true);
            }
            else
            {
                cardObj = CreateQuestCard(_questsViewObj.transform);
                _questCardPool.Add(cardObj);
            }

            UpdateQuestCard(cardObj, q);
        }

        for (int i = quests.Count; i < _questCardPool.Count; i++)
        {
            _questCardPool[i].SetActive(false);
        }

        // 2. Refresh Achievements
        IReadOnlyList<Achievement> achievements = QuestManager.Instance.Achievements;
        for (int i = 0; i < achievements.Count; i++)
        {
            Achievement ach = achievements[i];
            GameObject cardObj;
            if (i < _achievementCardPool.Count)
            {
                cardObj = _achievementCardPool[i];
                cardObj.SetActive(true);
            }
            else
            {
                cardObj = CreateAchievementCard(_achievementsViewObj.transform);
                _achievementCardPool.Add(cardObj);
            }

            UpdateAchievementCard(cardObj, ach);
        }

        for (int i = achievements.Count; i < _achievementCardPool.Count; i++)
        {
            _achievementCardPool[i].SetActive(false);
        }
    }

    private GameObject CreateQuestCard(Transform parent)
    {
        GameObject card = new GameObject("QuestCard");
        card.transform.SetParent(parent, false);

        LayoutElement le = card.AddComponent<LayoutElement>();
        le.minHeight = 62f;
        le.preferredHeight = 62f;

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        outline.effectDistance = new Vector2(1f, 1f);

        // Title Text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(card.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(12f, -8f);
        titleRect.sizeDelta = new Vector2(250f, 18f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = _uiFont;
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.92f, 0.95f, 1.0f);
        titleText.alignment = TextAnchor.MiddleLeft;

        // Reward Badge
        GameObject rewObj = new GameObject("RewardBadge");
        rewObj.transform.SetParent(card.transform, false);
        RectTransform rewRect = rewObj.AddComponent<RectTransform>();
        rewRect.anchorMin = new Vector2(0f, 1f);
        rewRect.anchorMax = new Vector2(0f, 1f);
        rewRect.pivot = new Vector2(0f, 1f);
        rewRect.anchoredPosition = new Vector2(270f, -8f);
        rewRect.sizeDelta = new Vector2(130f, 18f);

        Text rewText = rewObj.AddComponent<Text>();
        rewText.font = _uiFont;
        rewText.fontSize = 11;
        rewText.fontStyle = FontStyle.Bold;
        rewText.color = new Color(1f, 0.85f, 0.30f); // Warm Gold
        rewText.alignment = TextAnchor.MiddleLeft;

        // Description Text
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(card.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(0f, 0f);
        descRect.pivot = new Vector2(0f, 0f);
        descRect.anchoredPosition = new Vector2(12f, 10f);
        descRect.sizeDelta = new Vector2(360f, 18f);

        Text descText = descObj.AddComponent<Text>();
        descText.font = _uiFont;
        descText.fontSize = 11;
        descText.color = new Color(0.68f, 0.74f, 0.82f);
        descText.alignment = TextAnchor.MiddleLeft;

        // Status Badge (Top-Right)
        GameObject statusObj = new GameObject("StatusBadge");
        statusObj.transform.SetParent(card.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(1f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(1f, 1f);
        statusRect.anchoredPosition = new Vector2(-12f, -8f);
        statusRect.sizeDelta = new Vector2(130f, 18f);

        Text statusText = statusObj.AddComponent<Text>();
        statusText.font = _uiFont;
        statusText.fontSize = 11;
        statusText.fontStyle = FontStyle.Bold;
        statusText.alignment = TextAnchor.MiddleRight;

        // Progress Text (Bottom-Right)
        GameObject progObj = new GameObject("ProgressText");
        progObj.transform.SetParent(card.transform, false);
        RectTransform progRect = progObj.AddComponent<RectTransform>();
        progRect.anchorMin = new Vector2(1f, 0f);
        progRect.anchorMax = new Vector2(1f, 0f);
        progRect.pivot = new Vector2(1f, 0f);
        progRect.anchoredPosition = new Vector2(-12f, 10f);
        progRect.sizeDelta = new Vector2(160f, 18f);

        Text progText = progObj.AddComponent<Text>();
        progText.font = _uiFont;
        progText.fontSize = 11;
        progText.color = new Color(0.85f, 0.90f, 0.96f);
        progText.alignment = TextAnchor.MiddleRight;

        return card;
    }

    private void UpdateQuestCard(GameObject card, Quest quest)
    {
        Text titleText = card.transform.Find("Title").GetComponent<Text>();
        Text rewText = card.transform.Find("RewardBadge").GetComponent<Text>();
        Text descText = card.transform.Find("Description").GetComponent<Text>();
        Text statusText = card.transform.Find("StatusBadge").GetComponent<Text>();
        Text progText = card.transform.Find("ProgressText").GetComponent<Text>();

        titleText.text = quest.Title;
        descText.text = quest.Description;
        rewText.text = $"Reward: {quest.RewardXP} XP";
        progText.text = quest.ProgressText;

        Image cardBg = card.GetComponent<Image>();
        Outline outline = card.GetComponent<Outline>();

        if (quest.IsCompleted || quest.IsClaimed)
        {
            statusText.text = "✓ COMPLETED (CLAIMED)";
            statusText.color = new Color(0.20f, 0.92f, 0.45f); // Vibrant Emerald
            cardBg.color = new Color(0.04f, 0.08f, 0.06f, 0.96f);
            outline.effectColor = new Color(0.18f, 0.40f, 0.25f, 1f);
        }
        else
        {
            statusText.text = "IN PROGRESS";
            statusText.color = new Color(0.40f, 0.85f, 1.0f); // Cyan
            cardBg.color = new Color(0.06f, 0.08f, 0.11f, 0.96f);
            outline.effectColor = new Color(0.20f, 0.26f, 0.35f, 1f);
        }
    }

    private GameObject CreateAchievementCard(Transform parent)
    {
        GameObject card = new GameObject("AchievementCard");
        card.transform.SetParent(parent, false);

        LayoutElement le = card.AddComponent<LayoutElement>();
        le.minHeight = 62f;
        le.preferredHeight = 62f;

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        outline.effectDistance = new Vector2(1f, 1f);

        // Title Text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(card.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(12f, -10f);
        titleRect.sizeDelta = new Vector2(300f, 20f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = _uiFont;
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.92f, 0.95f, 1.0f);
        titleText.alignment = TextAnchor.MiddleLeft;

        // Description Text
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(card.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(0f, 0f);
        descRect.pivot = new Vector2(0f, 0f);
        descRect.anchoredPosition = new Vector2(12f, 10f);
        descRect.sizeDelta = new Vector2(380f, 20f);

        Text descText = descObj.AddComponent<Text>();
        descText.font = _uiFont;
        descText.fontSize = 11;
        descText.color = new Color(0.68f, 0.74f, 0.82f);
        descText.alignment = TextAnchor.MiddleLeft;

        // Status Badge (Right)
        GameObject statusObj = new GameObject("StatusBadge");
        statusObj.transform.SetParent(card.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(1f, 0.5f);
        statusRect.anchorMax = new Vector2(1f, 0.5f);
        statusRect.pivot = new Vector2(1f, 0.5f);
        statusRect.anchoredPosition = new Vector2(-16f, 0f);
        statusRect.sizeDelta = new Vector2(120f, 24f);

        Text statusText = statusObj.AddComponent<Text>();
        statusText.font = _uiFont;
        statusText.fontSize = 12;
        statusText.fontStyle = FontStyle.Bold;
        statusText.alignment = TextAnchor.MiddleRight;

        return card;
    }

    private void UpdateAchievementCard(GameObject card, Achievement ach)
    {
        Text titleText = card.transform.Find("Title").GetComponent<Text>();
        Text descText = card.transform.Find("Description").GetComponent<Text>();
        Text statusText = card.transform.Find("StatusBadge").GetComponent<Text>();

        titleText.text = "★ " + ach.Title;
        descText.text = ach.Description;

        Image cardBg = card.GetComponent<Image>();
        Outline outline = card.GetComponent<Outline>();

        if (ach.IsUnlocked)
        {
            statusText.text = "★ UNLOCKED";
            statusText.color = new Color(1f, 0.82f, 0.20f); // Radiant Gold
            cardBg.color = new Color(0.08f, 0.08f, 0.05f, 0.96f);
            outline.effectColor = new Color(0.45f, 0.38f, 0.15f, 1f);
        }
        else
        {
            statusText.text = "LOCKED";
            statusText.color = new Color(0.50f, 0.55f, 0.62f);
            cardBg.color = new Color(0.06f, 0.08f, 0.11f, 0.96f);
            outline.effectColor = new Color(0.20f, 0.24f, 0.30f, 1f);
        }
    }

    // =========================================================================
    // Queued Toast Notification System (Top-Left)
    // =========================================================================

    private void CreateToastUI()
    {
        _toastObj = new GameObject("ToastNotification");
        _toastObj.transform.SetParent(_canvasObj.transform, false);

        RectTransform toastRect = _toastObj.AddComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0f, 1f);
        toastRect.anchorMax = new Vector2(0f, 1f);
        toastRect.pivot = new Vector2(0f, 1f);
        toastRect.anchoredPosition = new Vector2(24f, -24f); // Top-left corner
        toastRect.sizeDelta = new Vector2(310f, 72f);

        Image toastBg = _toastObj.AddComponent<Image>();
        toastBg.color = new Color(0.04f, 0.07f, 0.11f, 0.96f);
        toastBg.raycastTarget = false;

        _toastOutline = _toastObj.AddComponent<Outline>();
        _toastOutline.effectColor = new Color(1f, 0.82f, 0.20f, 1f);
        _toastOutline.effectDistance = new Vector2(1.5f, 1.5f);

        _toastCanvasGroup = _toastObj.AddComponent<CanvasGroup>();
        _toastCanvasGroup.alpha = 0f;
        _toastCanvasGroup.blocksRaycasts = false;
        _toastCanvasGroup.interactable = false;

        // Title: "ACHIEVEMENT UNLOCKED!"
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_toastObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(12f, -8f);
        titleRect.sizeDelta = new Vector2(-24f, 18f);

        _toastTitleText = titleObj.AddComponent<Text>();
        _toastTitleText.font = _uiFont;
        _toastTitleText.fontSize = 11;
        _toastTitleText.fontStyle = FontStyle.Bold;
        _toastTitleText.color = new Color(1f, 0.82f, 0.20f);
        _toastTitleText.alignment = TextAnchor.MiddleLeft;
        _toastTitleText.raycastTarget = false;

        // Subtitle: "First Break"
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(_toastObj.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 1f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0f, 1f);
        subRect.anchoredPosition = new Vector2(12f, -28f);
        subRect.sizeDelta = new Vector2(-24f, 18f);

        _toastSubtitleText = subObj.AddComponent<Text>();
        _toastSubtitleText.font = _uiFont;
        _toastSubtitleText.fontSize = 13;
        _toastSubtitleText.fontStyle = FontStyle.Bold;
        _toastSubtitleText.color = Color.white;
        _toastSubtitleText.alignment = TextAnchor.MiddleLeft;
        _toastSubtitleText.raycastTarget = false;

        // Description: "Break your first block."
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(_toastObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 0f);
        descRect.pivot = new Vector2(0f, 0f);
        descRect.anchoredPosition = new Vector2(12f, 8f);
        descRect.sizeDelta = new Vector2(-24f, 16f);

        _toastDescText = descObj.AddComponent<Text>();
        _toastDescText.font = _uiFont;
        _toastDescText.fontSize = 10;
        _toastDescText.color = new Color(0.70f, 0.76f, 0.85f);
        _toastDescText.alignment = TextAnchor.MiddleLeft;
        _toastDescText.raycastTarget = false;

        _toastObj.SetActive(false);
    }

    public void QueueAchievementNotification(Achievement ach)
    {
        _toastQueue.Enqueue(new ToastData
        {
            Title = "★ ACHIEVEMENT UNLOCKED",
            Subtitle = ach.Title,
            Description = ach.Description,
            TitleColor = new Color(1f, 0.82f, 0.20f), // Radiant Gold
            BorderColor = new Color(1f, 0.82f, 0.20f, 1f)
        });
    }

    public void QueueQuestCompletedNotification(Quest quest)
    {
        _toastQueue.Enqueue(new ToastData
        {
            Title = "✓ QUEST COMPLETED!",
            Subtitle = $"{quest.Title} (+{quest.RewardXP} XP)",
            Description = quest.Description,
            TitleColor = new Color(0.25f, 0.92f, 0.45f), // Emerald Green
            BorderColor = new Color(0.25f, 0.92f, 0.45f, 1f)
        });
    }

    private void UpdateToastAnimation()
    {
        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.unscaledDeltaTime;
            float elapsed = ToastDuration - _toastTimer;

            float alpha = 1f;
            if (elapsed < 0.25f)
            {
                alpha = Mathf.Clamp01(elapsed / 0.25f);
            }
            else if (_toastTimer < 0.5f)
            {
                alpha = Mathf.Clamp01(_toastTimer / 0.5f);
            }

            if (_toastCanvasGroup != null)
            {
                _toastCanvasGroup.alpha = alpha;
            }

            if (_toastTimer <= 0f)
            {
                if (_toastObj != null) _toastObj.SetActive(false);
            }
        }
        else if (_toastQueue.Count > 0)
        {
            // Pop next notification from queue
            ToastData next = _toastQueue.Dequeue();
            DisplayToast(next);
        }
    }

    private void DisplayToast(ToastData data)
    {
        if (_toastObj == null) return;

        _toastTitleText.text = data.Title;
        _toastTitleText.color = data.TitleColor;
        _toastSubtitleText.text = data.Subtitle;
        _toastDescText.text = data.Description;
        _toastOutline.effectColor = data.BorderColor;

        _toastObj.SetActive(true);
        _toastTimer = ToastDuration;
        if (_toastCanvasGroup != null)
        {
            _toastCanvasGroup.alpha = 0f;
        }

        // Play pleasant notification chime
        if (_audioSource != null && _chimeClip != null)
        {
            _audioSource.PlayOneShot(_chimeClip, 0.70f);
        }
    }

    private void CreateChimeClip()
    {
        int sampleRate = 44100;
        float duration = 0.50f;
        int sampleCount = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // Ascending D5-F#5-A5 major triad with smooth exponential decay
            float envelope = Mathf.Exp(-t * 5.0f);
            float note1 = Mathf.Sin(2f * Mathf.PI * 587.33f * t);
            float note2 = t > 0.08f ? Mathf.Sin(2f * Mathf.PI * 739.99f * (t - 0.08f)) * Mathf.Exp(-(t - 0.08f) * 4.5f) : 0f;
            float note3 = t > 0.16f ? Mathf.Sin(2f * Mathf.PI * 880.00f * (t - 0.16f)) * Mathf.Exp(-(t - 0.16f) * 4.0f) : 0f;

            samples[i] = (note1 * 0.40f * envelope + note2 * 0.45f + note3 * 0.55f) * 0.35f;
        }

        _chimeClip = AudioClip.Create("QuestAchievementChime", sampleCount, 1, sampleRate, false);
        _chimeClip.SetData(samples, 0);
    }
}
