using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flynn.Demo
{
    /// <summary>
    /// Fullscreen fade-to-black "demo complete" overlay on the PlayerHud
    /// UIDocument (same discovery as ObjectiveTracker's chip container).
    /// Freezes gameplay with Time.timeScale = 0; fade runs on unscaled time.
    /// </summary>
    public class DemoEndScreen : MonoBehaviour
    {
        [SerializeField] private string _title = "Signal Restored";
        [SerializeField] private string _subtitle = "The gate is open. Another island. Another voice.\n\nDemo complete — thank you for playing.";
        [SerializeField] private float _fadeDuration = 1.6f;

        private bool _shown;

        public void Show()
        {
            if (_shown) return;
            _shown = true;
            StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            var root = FindHudRoot();
            if (root == null) yield break;

            var overlay = new VisualElement { name = "demo-end-overlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.right = 0;
            overlay.style.top = 0; overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            overlay.style.opacity = 0f;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            var title = new Label(_title);
            title.style.fontSize = 42;
            title.style.color = new Color(0.95f, 0.9f, 0.7f, 1f);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            overlay.Add(title);

            var sub = new Label(_subtitle);
            sub.style.fontSize = 18;
            sub.style.color = new Color(0.8f, 0.82f, 0.85f, 1f);
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            sub.style.marginTop = 16;
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.maxWidth = 560;
            overlay.Add(sub);

            root.Add(overlay);

            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                overlay.style.opacity = Mathf.Clamp01(t / _fadeDuration);
                yield return null;
            }
            overlay.style.opacity = 1f;

            Time.timeScale = 0f;
        }

        private static VisualElement FindHudRoot()
        {
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            UIDocument target = null;
            foreach (var doc in docs)
            {
                if (doc.gameObject.name == "PlayerHud") { target = doc; break; }
            }
            if (target == null && docs.Length > 0) target = docs[0];
            return target != null ? target.rootVisualElement : null;
        }
    }
}
