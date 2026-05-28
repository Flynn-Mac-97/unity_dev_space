using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
public class NpcRadialMenuBuilder : MonoBehaviour
{
    static readonly Color k_PanelBg     = new Color(0f, 0f, 0f, 0.92f);
    static readonly Color k_PanelOutline = new Color(1f, 1f, 1f, 1f);
    static readonly Color k_ButtonBg    = new Color(0f, 0f, 0f, 1f);
    static readonly Color k_ButtonText  = Color.white;

    const float k_PanelW    = 180f;
    const float k_BtnW      = 160f;
    const float k_BtnH      = 44f;
    const float k_BtnGap    = 8f;
    const float k_PanelPad  = 10f;

    private NpcInteractionContext _context;
    private readonly List<NpcAction> _bound = new List<NpcAction>();

    public void Bind(NpcInteractionContext context, IList<NpcAction> actions)
    {
        _context = context;
        _bound.Clear();
        if (actions != null)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] == null) continue;
                if (!actions[i].IsAvailable(context)) continue;
                _bound.Add(actions[i]);
            }
        }
        Rebuild();
    }

    private void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        int count = Mathf.Max(1, _bound.Count);
        float panelH = (k_BtnH * count) + (k_BtnGap * Mathf.Max(0, count - 1)) + (k_PanelPad * 2f);

        var canvasRt = GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(k_PanelW, panelH);

        if (TryGetComponent<CanvasScaler>(out var scaler))
            scaler.dynamicPixelsPerUnit = 10f;

        var panelRt = MakePanel(transform);

        if (_bound.Count == 0)
        {
            MakeLabel(panelRt, new Vector2(0f, 0f), "No actions");
            return;
        }

        float top = (panelH * 0.5f) - k_PanelPad - (k_BtnH * 0.5f);
        for (int i = 0; i < _bound.Count; i++)
        {
            float y = top - i * (k_BtnH + k_BtnGap);
            BuildButton(panelRt, new Vector2(0f, y), _bound[i]);
        }
    }

    RectTransform MakePanel(Transform parent)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = k_PanelBg; img.raycastTarget = false;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = k_PanelOutline;
        outline.effectDistance = new Vector2(1f, -1f);
        return rt;
    }

    void BuildButton(RectTransform parent, Vector2 anchoredPos, NpcAction action)
    {
        var go = new GameObject(action.name + "_Btn",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(k_BtnW, k_BtnH);
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color = k_ButtonBg;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = go.GetComponent<Button>();
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.65f);
        cb.pressedColor     = new Color(0.6f, 0.6f, 0.6f, 1f);
        cb.selectedColor    = Color.white;
        cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        cb.fadeDuration     = 0.05f;
        btn.colors = cb;

        var captured = action;
        var ctx = _context;
        btn.onClick.AddListener(() => captured.Execute(ctx));

        string label = string.IsNullOrWhiteSpace(action.actionLabel) ? action.name : action.actionLabel;
        if (!string.IsNullOrWhiteSpace(action.hotkeyHint))
            label = string.Format("{0}  [{1}]", label, action.hotkeyHint);

        MakeLabel(rt, Vector2.zero, label);
    }

    static void MakeLabel(RectTransform parent, Vector2 anchoredPos, string text)
    {
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(parent, false);
        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);
        rt.anchoredPosition += anchoredPos;

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.color         = k_ButtonText;
        tmp.fontSize      = 22f;
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
    }
}
