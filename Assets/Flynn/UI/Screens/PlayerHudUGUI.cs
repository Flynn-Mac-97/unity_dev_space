using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Self-contained screen-space UGUI HUD for the player. Builds its own Canvas + widgets in
/// code (no UXML, no prefab wiring, no UI-Toolkit OnEnable timing trap), so dropping this one
/// component on a GameObject is the entire HUD. Widgets:
///   • Hotbar  — slots pinned bottom-centre (slot count is data-driven from PlayerInventory),
///               driven by inventory events: active slot highlighted, icons + stack counts shown,
///               drag a slot off the bar to drop it; a FULL tag appears when no slot is free.
///   • Charge  — a power bar that appears while a swing/throw is charging (light/heavy tick).
///   • Prompt  — a generic world-anchored interaction prompt ("[E] Pick Up", "[Q] Grapple", …)
///               that floats over whatever IInteractionPromptProvider the aimer is targeting.
///   • HP bar  — a world-anchored bar over a struck resource node; the fill lerps down on each hit
///               and the bar fades out shortly after. Driven by a ResourceHitChannel.
///   • Echo    — a top-right Echo Shard counter (Stardew-money style), driven by an IntVariable.
/// Wire the player refs in the Inspector; if left null the scene component refs are resolved once.
/// </summary>
public class PlayerHudUGUI : MonoBehaviour
{
    [Header("Player references (auto-resolved if left null)")]
    [SerializeField] private PlayerInventory       _inventory;
    [SerializeField] private PlayerMouseAimer       _aimer;
    [SerializeField] private WrenchSwingController  _swing;
    [SerializeField] private WrenchThrowController  _throw;
    [SerializeField] private WrenchConfig           _config;
    [SerializeField] private PlayerDropController    _dropController;

    [Header("Channels / variables (asset refs — wire in Inspector)")]
    [SerializeField] private ResourceHitChannel _hitChannel;
    [SerializeField] private IntVariable        _echoShards;
    [SerializeField] private Sprite             _echoIcon;

    [Header("Layout")]
    [SerializeField] private int   _slotSize       = 64;
    [SerializeField] private int   _slotGap        = 8;
    [SerializeField] private float _promptOffsetY  = 56f;

    [Header("Resource HP bar")]
    [Tooltip("Screen-space pixels above the node position where the bar sits.")]
    [SerializeField] private float _hpScreenOffsetY = 10f;
    [SerializeField] private float _hpHideDelay      = 1.4f;
    [SerializeField] private float _hpLerpSpeed      = 2.5f;

    private static readonly Color White     = Color.white;
    private static readonly Color Dim        = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color Black      = Color.black;
    private static readonly Color AccentFill = new Color(0.3f, 0.8f, 1f, 1f);

    private const float ChargeTrackW = 160f;
    private const float HpTrackW     = 120f;

    // Hotbar widgets.
    private Image[]           _slotBorders;
    private Image[]           _slotIcons;
    private TextMeshProUGUI[] _slotCounts;
    private RectTransform     _hotbarRect;
    private GameObject        _fullTag;

    // Charge widgets.
    private GameObject       _chargeRoot;
    private RectTransform    _chargeFill;
    private RectTransform    _chargeTick;
    private TextMeshProUGUI  _chargeLabel;

    // Interaction-prompt widgets (generic: pickup / inspect / talk / grapple…).
    private GameObject       _promptRoot;
    private RectTransform    _promptRect;
    private TextMeshProUGUI  _promptLabel;

    // Resource HP bar widgets.
    private GameObject     _hpRoot;
    private RectTransform  _hpRect;
    private RectTransform  _hpFill;
    private ResourceNode   _hpNode;
    private float          _hpDisplayed;
    private float          _hpTarget;
    private float          _hpHideTimer;

    // Echo Shard counter widgets.
    private GameObject      _echoRoot;
    private TextMeshProUGUI _echoLabel;

    private Camera _cam;
    private bool   _built;

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        ResolveRefs();
        _cam = Camera.main;
        BuildUI();
    }

    private void OnEnable()
    {
        if (_inventory != null)
        {
            _inventory.OnSlotChanged       += RefreshSlot;
            _inventory.OnActiveSlotChanged += RefreshActiveSlot;
        }
        if (_hitChannel != null) _hitChannel.OnHit  += HandleResourceHit;
        if (_echoShards != null) _echoShards.OnChanged += HandleEchoChanged;
        if (_built) RefreshAll();
    }

    private void Start()
    {
        // PlayerInventory builds its runtime slots in Awake (no event), and Awake order across
        // objects isn't guaranteed. By Start its slot count is valid — build the hotbar now.
        if (_slotBorders == null) BuildHotbar(transform);
        RefreshAll();
        if (_echoShards != null) HandleEchoChanged(_echoShards.Value);
    }

    private void OnDisable()
    {
        if (_inventory != null)
        {
            _inventory.OnSlotChanged       -= RefreshSlot;
            _inventory.OnActiveSlotChanged -= RefreshActiveSlot;
        }
        if (_hitChannel != null) _hitChannel.OnHit  -= HandleResourceHit;
        if (_echoShards != null) _echoShards.OnChanged -= HandleEchoChanged;
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        UpdateCharge();
        UpdateInteractionPrompt();
        UpdateHpBar();
    }

    // ── Setup ───────────────────────────────────────────────────────────────

    private void ResolveRefs()
    {
        // Inspector wiring is preferred; fall back to a one-time scene resolve.
        if (_inventory      == null) _inventory      = FindObjectOfType<PlayerInventory>();
        if (_aimer          == null) _aimer          = FindObjectOfType<PlayerMouseAimer>();
        if (_swing          == null) _swing          = FindObjectOfType<WrenchSwingController>();
        if (_throw          == null) _throw          = FindObjectOfType<WrenchThrowController>();
        if (_dropController  == null) _dropController  = FindObjectOfType<PlayerDropController>();
    }

    private void BuildUI()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        BuildCharge(transform);
        BuildInteractionPrompt(transform);
        BuildHpBar(transform);
        BuildEchoCounter(transform);

        _built = true;
        // Hotbar is built in Start (needs the inventory's slot count).
    }

    private void BuildHotbar(Transform root)
    {
        int n = _inventory != null ? _inventory.SlotCount : 4;
        _slotBorders = new Image[n];
        _slotIcons   = new Image[n];
        _slotCounts  = new TextMeshProUGUI[n];

        float totalW = n * _slotSize + (n - 1) * _slotGap;

        _hotbarRect = NewRect("Hotbar", root);
        _hotbarRect.anchorMin = _hotbarRect.anchorMax = new Vector2(0.5f, 0f);
        _hotbarRect.pivot     = new Vector2(0.5f, 0f);
        _hotbarRect.anchoredPosition = new Vector2(0f, 24f);
        _hotbarRect.sizeDelta = new Vector2(totalW, _slotSize);

        for (int i = 0; i < n; i++)
        {
            // Border (white square) — colour is the active/inactive indicator + the drag target.
            var border = NewImage($"Slot_{i}", _hotbarRect, White);
            border.anchorMin = border.anchorMax = new Vector2(0f, 0.5f);
            border.pivot = new Vector2(0f, 0.5f);
            border.sizeDelta = new Vector2(_slotSize, _slotSize);
            border.anchoredPosition = new Vector2(i * (_slotSize + _slotGap), 0f);
            _slotBorders[i] = border.GetComponent<Image>();

            // Fill (black inset) — the panel body. Not a raycast target so drags hit the border.
            var fill = NewImage("Fill", border, Black);
            Stretch(fill, 2f);
            fill.GetComponent<Image>().raycastTarget = false;

            // Icon (centred, hidden until an item occupies the slot).
            var icon = NewImage("Icon", fill, White);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(_slotSize - 16, _slotSize - 16);
            var iconImg = icon.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = false;
            _slotIcons[i] = iconImg;

            // Hotkey number, top-left.
            var key = NewText($"Key_{i}", border, (i + 1).ToString(), 11, TextAlignmentOptions.TopLeft);
            Stretch(key, 4f);

            // Stack count, bottom-right (hidden when ≤ 1).
            var count = NewText($"Count_{i}", border, "", 14, TextAlignmentOptions.BottomRight);
            Stretch(count, 4f);
            _slotCounts[i] = count.GetComponent<TextMeshProUGUI>();
            _slotCounts[i].enabled = false;

            // Drag-to-drop.
            var drag = border.gameObject.AddComponent<HotbarSlotDragHandler>();
            drag.Init(i, _dropController, _hotbarRect, icon);
        }

        // FULL tag, centred above the bar.
        _fullTag = NewImage("FullTag", _hotbarRect, Black).gameObject;
        var ft = (RectTransform)_fullTag.transform;
        ft.anchorMin = ft.anchorMax = new Vector2(0.5f, 1f);
        ft.pivot = new Vector2(0.5f, 0f);
        ft.sizeDelta = new Vector2(72f, 24f);
        ft.anchoredPosition = new Vector2(0f, 8f);
        var ftOutline = _fullTag.AddComponent<Outline>();
        ftOutline.effectColor = White; ftOutline.effectDistance = new Vector2(1f, 1f);
        var ftLabel = NewText("Label", _fullTag.transform, "FULL", 13, TextAlignmentOptions.Center);
        Stretch(ftLabel, 2f);
        _fullTag.SetActive(false);
    }

    private void BuildCharge(Transform root)
    {
        _chargeRoot = NewRect("Charge", root).gameObject;
        var rt = (RectTransform)_chargeRoot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 112f);
        rt.sizeDelta = new Vector2(ChargeTrackW, 28f);

        var track = NewImage("Track", _chargeRoot.transform, White);
        track.anchorMin = new Vector2(0.5f, 0f); track.anchorMax = new Vector2(0.5f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.sizeDelta = new Vector2(ChargeTrackW, 10f);
        track.anchoredPosition = Vector2.zero;
        var inner = NewImage("Inner", track, Black);
        Stretch(inner, 1f);

        var fill = NewImage("Fill", inner, AccentFill);
        fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta = new Vector2(0f, 0f);
        _chargeFill = fill;

        var tick = NewImage("Tick", inner, White);
        tick.anchorMin = new Vector2(0f, 0f); tick.anchorMax = new Vector2(0f, 1f);
        tick.pivot = new Vector2(0.5f, 0.5f);
        tick.sizeDelta = new Vector2(1f, 0f);
        _chargeTick = tick;

        _chargeLabel = NewText("Label", _chargeRoot.transform, "", 12, TextAlignmentOptions.Bottom)
            .GetComponent<TextMeshProUGUI>();
        var lr = (RectTransform)_chargeLabel.transform;
        lr.anchorMin = new Vector2(0.5f, 0f); lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.sizeDelta = new Vector2(ChargeTrackW, 16f);
        lr.anchoredPosition = new Vector2(0f, 12f);

        _chargeRoot.SetActive(false);
    }

    private void BuildInteractionPrompt(Transform root)
    {
        _promptRoot = NewImage("InteractionPrompt", root, Black).gameObject;
        _promptRect = (RectTransform)_promptRoot.transform;
        _promptRect.sizeDelta = new Vector2(180f, 40f);
        var outline = _promptRoot.AddComponent<Outline>();
        outline.effectColor = White;
        outline.effectDistance = new Vector2(1f, 1f);

        _promptLabel = NewText("Label", _promptRoot.transform, "", 14, TextAlignmentOptions.Center)
            .GetComponent<TextMeshProUGUI>();
        Stretch((RectTransform)_promptLabel.transform, 6f);

        _promptRoot.SetActive(false);
    }

    private void BuildHpBar(Transform root)
    {
        _hpRoot = NewRect("ResourceHp", root).gameObject;
        _hpRect = (RectTransform)_hpRoot.transform;
        _hpRect.sizeDelta = new Vector2(HpTrackW, 10f);

        // Track: white border, black interior (matches the charge bar).
        var track = NewImage("Track", _hpRoot.transform, White);
        Stretch(track, 0f);
        var inner = NewImage("Inner", track, Black);
        Stretch(inner, 1f);

        var fill = NewImage("Fill", inner, White);
        fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta = new Vector2(0f, 0f);
        _hpFill = fill;

        _hpRoot.SetActive(false);
    }

    private void BuildEchoCounter(Transform root)
    {
        _echoRoot = NewImage("EchoCounter", root, Black).gameObject;
        var rt = (RectTransform)_echoRoot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        rt.sizeDelta = new Vector2(120f, 36f);
        var outline = _echoRoot.AddComponent<Outline>();
        outline.effectColor = White; outline.effectDistance = new Vector2(1f, 1f);

        if (_echoIcon != null)
        {
            var icon = NewImage("Icon", _echoRoot.transform, White);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(24f, 24f);
            icon.anchoredPosition = new Vector2(8f, 0f);
            var img = icon.GetComponent<Image>();
            img.sprite = _echoIcon; img.preserveAspect = true; img.raycastTarget = false;
        }

        _echoLabel = NewText("Count", _echoRoot.transform, "0", 18, TextAlignmentOptions.MidlineRight)
            .GetComponent<TextMeshProUGUI>();
        var lr = (RectTransform)_echoLabel.transform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(0f, 0f); lr.offsetMax = new Vector2(-10f, 0f);

        // Hidden until the first shard is collected.
        _echoRoot.SetActive(false);
    }

    // ── Per-frame ───────────────────────────────────────────────────────────

    private void UpdateCharge()
    {
        if (_chargeRoot == null) return;

        bool swinging = _swing != null && _swing.IsCharging;
        bool throwing = _throw != null && _throw.IsCharging;
        bool charging = swinging || throwing;

        if (_chargeRoot.activeSelf != charging) _chargeRoot.SetActive(charging);
        if (!charging) return;

        float charge = swinging ? _swing.ChargeNormalized : _throw.ChargeNormalized;
        float innerW = ChargeTrackW - 2f;
        _chargeFill.sizeDelta = new Vector2(Mathf.Clamp01(charge) * innerW, 0f);
        _chargeLabel.text = (swinging ? "SWING " : "THROW ") + Mathf.RoundToInt(charge * 100f) + "%";

        _chargeTick.gameObject.SetActive(swinging);
        if (swinging)
        {
            float thr = _config != null ? _config.heavySwingThreshold : 0.6f;
            _chargeTick.anchoredPosition = new Vector2(thr * innerW, 0f);
        }
    }

    private void UpdateInteractionPrompt()
    {
        if (_promptRoot == null) return;

        IInteractionPromptProvider provider = _aimer != null ? _aimer.HoveredInteractable : null;
        if (provider == null || !provider.TryGetPrompt(out InteractionPrompt prompt) || !prompt.IsValid)
        {
            if (_promptRoot.activeSelf) _promptRoot.SetActive(false);
            return;
        }

        if (_cam == null) return;
        Vector3 sp = _cam.WorldToScreenPoint(prompt.Anchor.position);
        if (sp.z < 0f)
        {
            if (_promptRoot.activeSelf) _promptRoot.SetActive(false);
            return;
        }

        _promptRect.position = new Vector3(sp.x, sp.y + _promptOffsetY, 0f);
        _promptLabel.text = prompt.Display;
        if (!_promptRoot.activeSelf) _promptRoot.SetActive(true);
    }

    private void UpdateHpBar()
    {
        if (_hpRoot == null) return;

        // Node destroyed or timed out → hide.
        if (_hpNode == null || _hpHideTimer <= 0f)
        {
            if (_hpRoot.activeSelf) _hpRoot.SetActive(false);
            return;
        }

        _hpHideTimer -= Time.deltaTime;

        if (_cam == null) return;
        // Anchor at the node position, nudged up a few screen pixels.
        Vector3 sp = _cam.WorldToScreenPoint(_hpNode.transform.position);
        if (sp.z < 0f)
        {
            if (_hpRoot.activeSelf) _hpRoot.SetActive(false);
            return;
        }

        // Lerp the displayed fill toward the real health for a satisfying drop.
        _hpDisplayed = Mathf.MoveTowards(_hpDisplayed, _hpTarget, _hpLerpSpeed * Time.deltaTime);
        float innerW = HpTrackW - 2f;
        _hpFill.sizeDelta = new Vector2(Mathf.Clamp01(_hpDisplayed) * innerW, 0f);

        _hpRect.position = new Vector3(sp.x, sp.y + _hpScreenOffsetY, 0f);
        if (!_hpRoot.activeSelf) _hpRoot.SetActive(true);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleResourceHit(ResourceNode node, float health01)
    {
        if (node != _hpNode)
        {
            _hpNode = node;
            _hpDisplayed = 1f; // animate down from full when a new node is struck
        }
        _hpTarget = health01;
        _hpHideTimer = _hpHideDelay;
    }

    private void HandleEchoChanged(int value)
    {
        if (_echoLabel == null) return;
        _echoLabel.text = value.ToString();
        if (_echoRoot != null && !_echoRoot.activeSelf) _echoRoot.SetActive(true);
    }

    // ── Hotbar refresh (event-driven) ─────────────────────────────────────────

    private void RefreshAll()
    {
        if (!_built || _inventory == null || _slotIcons == null) return;
        for (int i = 0; i < _inventory.SlotCount && i < _slotIcons.Length; i++) RefreshSlot(i);
        RefreshActiveSlot(_inventory.ActiveSlotIndex);
        UpdateFullTag();
    }

    private void RefreshSlot(int i)
    {
        if (_slotIcons == null || i < 0 || i >= _slotIcons.Length) return;

        InventorySlot slot = _inventory.GetSlot(i);
        bool hasItem = !slot.IsEmpty;

        if (hasItem && slot.item.icon != null)
        {
            _slotIcons[i].sprite  = slot.item.icon;
            _slotIcons[i].enabled = true;
        }
        else
        {
            _slotIcons[i].sprite  = null;
            _slotIcons[i].enabled = false;
        }

        if (hasItem && slot.count > 1)
        {
            _slotCounts[i].text    = slot.count.ToString();
            _slotCounts[i].enabled = true;
        }
        else
        {
            _slotCounts[i].enabled = false;
        }

        UpdateFullTag();
    }

    private void RefreshActiveSlot(int active)
    {
        if (_slotBorders == null) return;
        for (int i = 0; i < _slotBorders.Length; i++)
            _slotBorders[i].color = (i == active) ? White : Dim;
    }

    private void UpdateFullTag()
    {
        if (_fullTag == null || _inventory == null) return;
        bool full = _inventory.IsFull;
        if (_fullTag.activeSelf != full) _fullTag.SetActive(full);
    }

    // ── UGUI builders ─────────────────────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }

    private static RectTransform NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = White;
        t.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    /// <summary>Stretch a child to fill its parent with a uniform inset.</summary>
    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
