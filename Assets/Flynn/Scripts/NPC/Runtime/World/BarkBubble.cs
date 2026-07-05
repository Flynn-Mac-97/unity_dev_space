using System.Collections;
using TMPro;
using UnityEngine;

namespace Flynn.Npc
{
    /// <summary>
    /// One transient world-space speech bubble: flat black plate, 1px white border
    /// (project placeholder style), white type-on text. Self-destroys after hold+fade.
    /// Spawn via the static factory; no prefab, no scene wiring.
    /// </summary>
    public class BarkBubble : MonoBehaviour
    {
        private const float TypeInterval = 0.025f;
        private const float HoldSeconds = 2.4f;
        private const float FadeSeconds = 0.35f;
        private const float BobAmount = 0.06f;

        private SpriteRenderer _plate;
        private TextMeshPro _text;
        private string _fullText;
        private Vector3 _basePos;

        public static BarkBubble Spawn(Vector3 worldPos, string text, int sortingOrder)
        {
            var go = new GameObject("_barkBubble");
            go.transform.position = worldPos;

            var bubble = go.AddComponent<BarkBubble>();
            bubble._fullText = text;
            bubble._basePos = worldPos;

            var plateGO = new GameObject("Plate");
            plateGO.transform.SetParent(go.transform, false);
            bubble._plate = plateGO.AddComponent<SpriteRenderer>();
            bubble._plate.sprite = PlateSprite();
            bubble._plate.drawMode = SpriteDrawMode.Sliced;
            bubble._plate.sortingOrder = sortingOrder;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshPro>();
            tmp.fontSize = 3.2f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.rectTransform.sizeDelta = new Vector2(3.8f, 1.2f);
            tmp.sortingOrder = sortingOrder + 1;
            bubble._text = tmp;

            bubble.StartCoroutine(bubble.LifeRoutine());
            return bubble;
        }

        private IEnumerator LifeRoutine()
        {
            // Type on
            _text.text = "";
            for (int i = 0; i < _fullText.Length; i++)
            {
                _text.text = _fullText.Substring(0, i + 1);
                FitPlate();
                yield return new WaitForSeconds(TypeInterval);
            }

            // Hold with a gentle bob
            float t = 0f;
            while (t < HoldSeconds)
            {
                t += Time.deltaTime;
                transform.position = _basePos + Vector3.up * (Mathf.Sin(t * 2.2f) * BobAmount);
                yield return null;
            }

            // Fade
            t = 0f;
            Color pc = _plate.color, tc = _text.color;
            while (t < FadeSeconds)
            {
                t += Time.deltaTime;
                float a = 1f - t / FadeSeconds;
                _plate.color = new Color(pc.r, pc.g, pc.b, pc.a * a);
                _text.color = new Color(tc.r, tc.g, tc.b, a);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void FitPlate()
        {
            _text.ForceMeshUpdate();
            Vector2 size = _text.GetRenderedValues(false);
            _plate.size = new Vector2(Mathf.Max(0.8f, size.x + 0.32f), Mathf.Max(0.44f, size.y + 0.24f));
        }

        // Flat black plate, 1px white border — 9-sliced so it stretches cleanly.
        private static Sprite _plateSprite;

        private static Sprite PlateSprite()
        {
            if (_plateSprite != null) return _plateSprite;
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool border = x == 0 || y == 0 || x == s - 1 || y == s - 1;
                    tex.SetPixel(x, y, border ? Color.white : new Color(0f, 0f, 0f, 0.88f));
                }
            tex.Apply();
            _plateSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32,
                0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2)); // 9-slice borders
            return _plateSprite;
        }
    }
}
