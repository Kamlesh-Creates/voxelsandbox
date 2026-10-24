using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(EquippedItemController))]
public class HotbarController : MonoBehaviour
{
    [SerializeField] private EquippedItemController equippedItemController;

    private readonly ItemData[] _items =
    {
        new ItemData("Axe", ItemKind.Axe, BlockType.Air, new Color(0.42f, 0.25f, 0.12f)),
        new ItemData("Grass", ItemKind.Block, BlockType.Grass, new Color(0.25f, 0.65f, 0.18f)),
        new ItemData("Dirt", ItemKind.Block, BlockType.Dirt, new Color(0.45f, 0.25f, 0.1f)),
        new ItemData("Stone", ItemKind.Block, BlockType.Stone, new Color(0.45f, 0.47f, 0.5f)),
        new ItemData("Wood", ItemKind.Block, BlockType.Wood, new Color(0.45f, 0.24f, 0.08f)),
        new ItemData("Sand", ItemKind.Block, BlockType.Sand, new Color(0.82f, 0.7f, 0.4f))
    };

    private Image[] _slotBackgrounds;
    private Outline[] _slotOutlines;
    private Text[] _quantityTexts;
    private int _selectedIndex;
    private Font _uiFont;
    private bool _initialized;

    public int SelectedIndex => _selectedIndex;
    public ItemData SelectedItem => _items[_selectedIndex];

    private void Awake()
    {
        equippedItemController = GetComponent<EquippedItemController>();

        if (InventoryController.Instance == null)
        {
            GameObject invObj = new GameObject("InventoryController");
            invObj.AddComponent<InventoryController>();
        }

        if (GetComponent<BlockInteractionController>() == null)
        {
            gameObject.AddComponent<BlockInteractionController>();
        }

        InitializeHotbar();
    }

    private void Start()
    {
        SelectItem(0);
        UpdateQuantities();
    }

    private void OnEnable()
    {
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.OnInventoryChanged += UpdateQuantities;
        }
    }

    private void OnDisable()
    {
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.OnInventoryChanged -= UpdateQuantities;
        }
    }

    private void InitializeHotbar()
    {
        if (_initialized) return;

        if (equippedItemController == null)
        {
            Debug.LogError("HotbarController requires an EquippedItemController on the same PlayerCamera.", this);
            enabled = false;
            return;
        }

        CreateHotbar();
        _initialized = true;

        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.OnInventoryChanged += UpdateQuantities;
        }
    }

    private void Update()
    {
        // Suppress hotbar selection while pause menu, settings, death screen, or quest panel is open
        if (PauseMenuController.IsPaused || Time.timeScale <= 0f || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) || QuestUI.IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectItem(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectItem(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectItem(2);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectItem(3);
            if (keyboard.digit5Key.wasPressedThisFrame) SelectItem(4);
            if (keyboard.digit6Key.wasPressedThisFrame) SelectItem(5);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0f) SelectItem((_selectedIndex + _items.Length - 1) % _items.Length);
            if (scroll < 0f) SelectItem((_selectedIndex + 1) % _items.Length);
        }
    }

    private void CreateHotbar()
    {
        GameObject canvasObject = new GameObject("HotbarCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject barObject = new GameObject("Hotbar");
        barObject.transform.SetParent(canvas.transform, false);
        RectTransform barRect = barObject.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 28f);
        barRect.sizeDelta = new Vector2(382f, 68f);

        Image barBackground = barObject.AddComponent<Image>();
        barBackground.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);

        HorizontalLayoutGroup layout = barObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        _slotBackgrounds = new Image[_items.Length];
        _slotOutlines = new Outline[_items.Length];
        _quantityTexts = new Text[_items.Length];

        for (int i = 0; i < _items.Length; i++)
        {
            CreateSlot(barObject.transform, i);
        }
    }

    private void CreateSlot(Transform parent, int index)
    {
        GameObject slotObject = new GameObject("Slot" + (index + 1));
        slotObject.transform.SetParent(parent, false);
        LayoutElement layoutElement = slotObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 56f;
        layoutElement.minHeight = 56f;
        layoutElement.preferredWidth = 56f;
        layoutElement.preferredHeight = 56f;

        Image background = slotObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.1f, 0.13f, 0.96f);
        _slotBackgrounds[index] = background;

        Outline outline = slotObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.34f, 0.4f, 1f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        _slotOutlines[index] = outline;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.42f);
        iconRect.anchorMax = new Vector2(0.5f, 0.42f);
        iconRect.sizeDelta = new Vector2(24f, 24f);
        Image icon = iconObject.AddComponent<Image>();
        icon.color = _items[index].IconColor;

        // Slot number (1-6) in top-left
        GameObject numberObject = new GameObject("Number");
        numberObject.transform.SetParent(slotObject.transform, false);
        RectTransform numberRect = numberObject.AddComponent<RectTransform>();
        numberRect.anchorMin = new Vector2(0f, 1f);
        numberRect.anchorMax = new Vector2(0f, 1f);
        numberRect.pivot = new Vector2(0f, 1f);
        numberRect.anchoredPosition = new Vector2(5f, -4f);
        numberRect.sizeDelta = new Vector2(18f, 18f);
        Text number = numberObject.AddComponent<Text>();
        number.text = (index + 1).ToString();
        number.font = _uiFont;
        number.fontSize = 13;
        number.color = Color.white;
        number.alignment = TextAnchor.UpperLeft;

        // Display Name Label in bottom center
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(slotObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 3f);
        labelRect.sizeDelta = new Vector2(0f, 14f);
        Text label = labelObject.AddComponent<Text>();
        label.text = _items[index].DisplayName;
        label.font = _uiFont;
        label.fontSize = 9;
        label.color = new Color(0.9f, 0.92f, 0.96f);
        label.alignment = TextAnchor.MiddleCenter;

        // Item Stack Quantity in bottom right
        GameObject quantityObject = new GameObject("Quantity");
        quantityObject.transform.SetParent(slotObject.transform, false);
        RectTransform quantityRect = quantityObject.AddComponent<RectTransform>();
        quantityRect.anchorMin = new Vector2(1f, 0f);
        quantityRect.anchorMax = new Vector2(1f, 0f);
        quantityRect.pivot = new Vector2(1f, 0f);
        quantityRect.anchoredPosition = new Vector2(-4f, 14f);
        quantityRect.sizeDelta = new Vector2(28f, 16f);
        Text quantityText = quantityObject.AddComponent<Text>();
        quantityText.font = _uiFont;
        quantityText.fontSize = 12;
        quantityText.color = new Color(1f, 0.95f, 0.45f);
        quantityText.alignment = TextAnchor.MiddleRight;
        _quantityTexts[index] = quantityText;
    }

    public void UpdateQuantities()
    {
        if (_quantityTexts == null || _quantityTexts.Length != _items.Length) return;

        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i].Kind == ItemKind.Axe)
            {
                _quantityTexts[i].text = "1";
            }
            else
            {
                int count = InventoryController.Instance != null
                    ? InventoryController.Instance.GetItemCount(_items[i].BlockType)
                    : 0;
                _quantityTexts[i].text = count.ToString();
            }
        }
    }

    private void SelectItem(int index)
    {
        InitializeHotbar();
        if (!_initialized) return;

        _selectedIndex = Mathf.Clamp(index, 0, _items.Length - 1);

        for (int i = 0; i < _slotBackgrounds.Length; i++)
        {
            bool selected = i == _selectedIndex;
            _slotBackgrounds[i].color = selected
                ? new Color(0.2f, 0.17f, 0.08f, 1f)
                : new Color(0.08f, 0.1f, 0.13f, 0.96f);
            _slotOutlines[i].effectColor = selected
                ? new Color(1f, 0.78f, 0.18f, 1f)
                : new Color(0.3f, 0.34f, 0.4f, 1f);
            _slotOutlines[i].effectDistance = selected
                ? new Vector2(2.5f, 2.5f)
                : new Vector2(1.5f, 1.5f);
        }

        if (_items[_selectedIndex].Kind == ItemKind.Axe)
        {
            equippedItemController.EquipAxe();
        }
        else
        {
            equippedItemController.EquipBlock(_items[_selectedIndex].BlockType);
        }
    }
}
