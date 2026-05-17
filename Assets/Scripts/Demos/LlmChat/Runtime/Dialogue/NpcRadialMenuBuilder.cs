using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
#endif

/// <summary>
/// Procedurally builds a World Space uGUI radial interaction menu for an NPC.
/// Place this on the World Space Canvas GameObject (e.g. "RadialMenu").
/// Right-click → Build Menu, or call BuildMenu() at runtime.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
public class NpcRadialMenuBuilder : MonoBehaviour
{
    private enum MenuAction
    {
        Talk,
        Trade,
        Attack
    }

    [Header("Interaction Events")]
    public UnityEvent onTalk;
    public UnityEvent onTrade;
    public UnityEvent onAttack;

    [Header("Play Mode")]
    [Tooltip("If enabled, rebuild the menu at play start so runtime onClick handlers are guaranteed.")]
    public bool rebuildMenuOnPlay = true;

    // ── Design Tokens ────────────────────────────────────────────────────────
    static readonly Color k_PanelBg    = new Color(0.11f, 0.11f, 0.12f, 0.90f);
    static readonly Color k_ButtonColor = new Color(0.24f, 0.24f, 0.27f, 1f);

    const float k_PanelW   = 130f;
    const float k_PanelH   = 160f;
    const float k_BtnW     = 120f;
    const float k_BtnH     = 36f;
    const float k_AccentW  = 10f;
    const float k_LabelPad = 8f;   // left pad after accent strip

    // Button Y anchored positions from panel centre — 3 × 36 + 2 × 6 = 120 total,
    // 20 px top/bottom margin inside 160 panel.
    static readonly float[] k_BtnY = { 42f, 0f, -42f };

    private void Awake()
    {
        if (!Application.isPlaying) return;

        if (rebuildMenuOnPlay)
        {
            BuildMenu();
            return;
        }

        RebindExistingButtonsForPlay();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    [ContextMenu("Build Menu")]
    public void BuildMenu()
    {
        // 1. Destroy all existing children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
#else
            Destroy(child);
#endif
        }

        // 2. Ensure this Canvas is in WorldSpace mode and sized correctly
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRt = GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(k_PanelW, k_PanelH);

        // CanvasScaler (optional) — bump DPI so TMP renders crisply at small scale
        if (TryGetComponent<CanvasScaler>(out var scaler))
            scaler.dynamicPixelsPerUnit = 10f;

        // 3. Panel background — solid dark rect, crisp pixel-art style
        var panelRt = MakeImage("Panel", transform,
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            offsetMin: Vector2.zero, offsetMax: Vector2.zero,
            color: k_PanelBg, sprite: null,
            type: Image.Type.Simple, raycast: false);

        // 4. Build the three action buttons
        BuildButton(panelRt, "TalkButton",   new Vector2(0f, k_BtnY[0]), k_ButtonColor, "Talk",   MenuAction.Talk);
        BuildButton(panelRt, "TradeButton",  new Vector2(0f, k_BtnY[1]), k_ButtonColor, "Trade",  MenuAction.Trade);
        BuildButton(panelRt, "AttackButton", new Vector2(0f, k_BtnY[2]), k_ButtonColor, "Attack", MenuAction.Attack);

        Debug.Log("[NpcRadialMenuBuilder] Menu rebuilt.");

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    // ── Private Builders ─────────────────────────────────────────────────────

    void BuildButton(RectTransform parent, string btnName,
                     Vector2 anchoredPos, Color btnColor,
                     string labelText, MenuAction action)
    {
        // Root: Image + Button
        var go = new GameObject(btnName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta       = new Vector2(k_BtnW, k_BtnH);
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color  = btnColor;
        img.sprite = null;
        img.type   = Image.Type.Simple;

        // Color states: subtle graybox lift on hover, darker on press.
        var btn = go.GetComponent<Button>();
        var cb  = ColorBlock.defaultColorBlock;
        cb.normalColor      = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor     = new Color(0.60f, 0.60f, 0.60f, 1f);
        cb.selectedColor    = Color.white;
        cb.disabledColor    = new Color(0.5f,  0.5f,  0.5f,  0.4f);
        cb.colorMultiplier  = 1f;
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;

        WireButtonClick(btn, action);

        // Left accent strip — 10 px wide, full height, slightly brighter
        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGo.transform.SetParent(go.transform, false);

        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 0f);
        accentRt.anchorMax = new Vector2(0f, 1f);
        accentRt.pivot     = new Vector2(0f, 0.5f);
        accentRt.offsetMin = Vector2.zero;
        accentRt.offsetMax = new Vector2(k_AccentW, 0f);

        var accentImg = accentGo.GetComponent<Image>();
        accentImg.color         = Brighten(btnColor, 0.28f);
        accentImg.raycastTarget = false;

        // Label — bold white, left-aligned past accent + padding
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);

        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(k_AccentW + k_LabelPad, 0f);
        labelRt.offsetMax = new Vector2(-4f, 0f);

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text             = labelText;
        tmp.color            = Color.white;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.fontSize         = 16f;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget    = false;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;
    }

    void RebindExistingButtonsForPlay()
    {
        WireExistingButton("TalkButton", MenuAction.Talk);
        WireExistingButton("TradeButton", MenuAction.Trade);
        WireExistingButton("AttackButton", MenuAction.Attack);
    }

    void WireExistingButton(string buttonName, MenuAction action)
    {
        Button btn = FindButtonRecursive(transform, buttonName);
        if (btn == null)
            return;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => InvokeAction(action));
    }

    static Button FindButtonRecursive(Transform root, string buttonName)
    {
        if (root == null || string.IsNullOrWhiteSpace(buttonName))
            return null;

        if (string.Equals(root.name, buttonName, System.StringComparison.Ordinal))
        {
            Button selfButton = root.GetComponent<Button>();
            if (selfButton != null)
                return selfButton;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Button found = FindButtonRecursive(root.GetChild(i), buttonName);
            if (found != null)
                return found;
        }

        return null;
    }

    void WireButtonClick(Button btn, MenuAction action)
    {
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();

        if (Application.isPlaying)
        {
            btn.onClick.AddListener(() => InvokeAction(action));
            return;
        }

#if UNITY_EDITOR
        while (btn.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(btn.onClick, 0);

        switch (action)
        {
            case MenuAction.Talk:
                UnityEventTools.AddPersistentListener(btn.onClick, InvokeTalk);
                break;
            case MenuAction.Trade:
                UnityEventTools.AddPersistentListener(btn.onClick, InvokeTrade);
                break;
            case MenuAction.Attack:
                UnityEventTools.AddPersistentListener(btn.onClick, InvokeAttack);
                break;
        }

        EditorUtility.SetDirty(btn);
#endif
    }

    public void InvokeTalk() => onTalk?.Invoke();
    public void InvokeTrade() => onTrade?.Invoke();
    public void InvokeAttack() => onAttack?.Invoke();

    void InvokeAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Talk:
                InvokeTalk();
                break;
            case MenuAction.Trade:
                InvokeTrade();
                break;
            case MenuAction.Attack:
                InvokeAttack();
                break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static RectTransform MakeImage(string name, Transform parent,
                                    Vector2 anchorMin, Vector2 anchorMax,
                                    Vector2 offsetMin, Vector2 offsetMax,
                                    Color color, Sprite sprite,
                                    Image.Type type, bool raycast)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;

        var img = go.GetComponent<Image>();
        img.color           = color;
        img.sprite          = sprite;
        img.type            = type;
        img.raycastTarget   = raycast;

        return rt;
    }

    static Color Brighten(Color c, float amount) =>
        new Color(Mathf.Clamp01(c.r + amount),
                  Mathf.Clamp01(c.g + amount),
                  Mathf.Clamp01(c.b + amount), c.a);

    // Sprites not needed — pixel-art style uses clean flat colour rectangles.
}
