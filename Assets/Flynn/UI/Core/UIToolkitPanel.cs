using UnityEngine;
using UnityEngine.UIElements;


using Flynn.Core;

namespace Flynn.UI.Core
{
    /// <summary>
    /// Base for UI Toolkit panels driven by <see cref="UIManager"/>. Wraps the one fragile part of
    /// UI Toolkit — element binding — in the proven defense: the visual tree may not exist during
    /// OnEnable (component order isn't guaranteed), so <see cref="Bind"/> is retried from Update until
    /// it succeeds. Subclasses query/cache elements in Bind and never touch the tree before it's ready.
    ///
    /// Show/Hide toggle the root's <c>display</c> instead of the GameObject, so the UIDocument stays
    /// alive and we never rebuild + rebind the tree on every show (which is what made the old HUD
    /// flaky). The GameObject itself stays active.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UIToolkitPanel : UIPanel
    {
        [Tooltip("Visible as soon as it binds (always-present HUD = true; pop-ups like the hover tag = false).")]
        [SerializeField] private bool _startVisible = true;

        protected UIDocument Document { get; private set; }
        protected VisualElement Root { get; private set; }

        private bool _bound;

        // ── Unity messages ────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            Document = GetComponent<UIDocument>();
            IsVisible = _startVisible;
        }

        protected virtual void OnEnable()
        {
            _bound = false;
            TryBind();
        }

        protected virtual void Update()
        {
            if (!_bound) TryBind();
        }

        // ── Show / hide (display toggle, document stays alive) ──────────────────────

        public override void Show()
        {
            IsVisible = true;
            ApplyDisplay();
            OnShow();
        }

        public override void Hide()
        {
            IsVisible = false;
            ApplyDisplay();
            OnHide();
        }

        // ── Binding ─────────────────────────────────────────────────────────────────

        /// <summary>Query and cache elements. Return false if the tree isn't built yet (retried next frame).</summary>
        protected abstract bool Bind(VisualElement root);

        /// <summary>Called once after a successful bind — do the first data refresh here.</summary>
        protected virtual void OnBound() { }

        private void TryBind()
        {
            if (Document == null) return;
            VisualElement root = Document.rootVisualElement;
            if (root == null) return;
            if (!Bind(root)) return; // sentinel element missing → tree not ready, try again next frame

            Root = root;
            _bound = true;
            ApplyDisplay();
            OnBound();
        }

        private void ApplyDisplay()
        {
            if (Root != null)
                Root.style.display = IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

}
