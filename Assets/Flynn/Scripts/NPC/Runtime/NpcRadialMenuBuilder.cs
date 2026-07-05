using UnityEngine;
using UnityEngine.UI;
using TMPro;


using Flynn.UI.Core;

namespace Flynn.Npc
{
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
        const float k_PanelPad  = 10f;

        private NpcInteraction _npc;

        public void Bind(NpcInteraction npc)
        {
            _npc = npc;
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float panelH = k_BtnH + (k_PanelPad * 2f);

            var canvasRt = GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(k_PanelW, panelH);

            if (TryGetComponent<CanvasScaler>(out var scaler))
                scaler.dynamicPixelsPerUnit = 10f;

            var panelRt = MakePanel(transform);
            float y = (panelH * 0.5f) - k_PanelPad - (k_BtnH * 0.5f);
            BuildTalkButton(panelRt, new Vector2(0f, y));
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

        void BuildTalkButton(RectTransform parent, Vector2 anchoredPos)
        {
            var go = new GameObject("Talk_Btn",
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

            var captured = _npc;
            btn.onClick.AddListener(() => captured.OnTalk());

            MakeLabel(rt, Vector2.zero, "Talk  [E]");
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

}
