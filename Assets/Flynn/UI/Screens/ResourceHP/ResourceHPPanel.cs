using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Flynn.Events;

/// <summary>
/// Screen-space UI Toolkit panel that projects floating HP bars above ResourceNodes.
/// Driven by <see cref="UIManager"/> as <see cref="UIPanelId.ResourceHP"/>. Subscribes
/// to <see cref="ResourceDamaged"/> / <see cref="ResourceDepleted"/> on GameEventBus
/// and pools VisualElement bars. Bars are positioned by projecting world-space node
/// positions to panel coordinates via RuntimePanelUtils.
///
/// Bar style: white fill depleting left→right on a black background (Flynn mono).
/// Lives as a UIDocument under MANAGERS/UI.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ResourceHPPanel : UIToolkitPanel
{
    [Header("Appearance")]
    [Tooltip("World-space height offset above the node origin.")]
    [SerializeField] private float _heightOffset = 1.6f;

    [Header("Timing")]
    [Tooltip("Seconds to keep the bar visible after the last hit.")]
    [SerializeField] private float _displayDuration = 3f;

    [Tooltip("Seconds for the fade-out animation.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    [Tooltip("UXML template for a single health bar instance.")]
    [SerializeField] private VisualTreeAsset _healthBarTemplate;

    // ── Event bus subscription state ────────────────────────────────────────

    private bool _subscribed;

    // ── Pool & active tracking ─────────────────────────────────────────────

    private readonly Stack<VisualElement> _pool = new();
    private readonly Dictionary<ResourceNode, BarEntry> _active = new();

    private VisualElement _root;

    private struct BarEntry
    {
        public VisualElement Bar;
        public VisualElement Fill;
        public float HideTime;
        public bool Fading;
    }

    // ── Unity messages ──────────────────────────────────────────────────────

    protected override void OnEnable()
    {
        base.OnEnable();
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Subscribe<ResourceDamaged>(OnResourceDamaged);
            GameEventBus.Instance.Subscribe<ResourceDepleted>(OnResourceDepleted);
        }
    }

    private void OnDisable()
    {
        if (GameEventBus.Instance != null && _subscribed)
        {
            GameEventBus.Instance.Unsubscribe<ResourceDamaged>(OnResourceDamaged);
            GameEventBus.Instance.Unsubscribe<ResourceDepleted>(OnResourceDepleted);
            _subscribed = false;
        }
    }

    private void TrySubscribe()
    {
        if (_subscribed || GameEventBus.Instance == null) return;
        GameEventBus.Instance.Subscribe<ResourceDamaged>(OnResourceDamaged);
        GameEventBus.Instance.Subscribe<ResourceDepleted>(OnResourceDepleted);
        _subscribed = true;
    }

    private void LateUpdate()
    {
        TrySubscribe();

        if (_root == null || Camera.main == null) return;

        float now = Time.time;
        List<ResourceNode> toRelease = null;

        foreach (var kvp in _active)
        {
            var node = kvp.Key;
            var entry = kvp.Value;

            // Node destroyed — release immediately
            if (node == null)
            {
                if (toRelease == null) toRelease = new List<ResourceNode>();
                toRelease.Add(node);
                continue;
            }

            // Project world position to panel coordinates
            Vector3 worldPos = node.transform.position + Vector3.up * _heightOffset;
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                _root.panel, worldPos, Camera.main);

            // Offset so bar centers above the node
            float barWidth = entry.Bar.resolvedStyle.width;
            entry.Bar.style.left = panelPos.x - barWidth * 0.5f;
            entry.Bar.style.top = panelPos.y;

            // Start fading when timer expires
            if (!entry.Fading && now >= entry.HideTime)
            {
                entry.Fading = true;
                entry.Bar.AddToClassList("health-bar--fading");
                entry.Bar.style.transitionDuration =
                    new List<TimeValue> { new TimeValue(_fadeDuration) };
            }

            // Check if fade finished
            if (entry.Fading && now >= entry.HideTime + _fadeDuration)
            {
                if (toRelease == null) toRelease = new List<ResourceNode>();
                toRelease.Add(node);
            }
        }

        if (toRelease != null)
        {
            foreach (var node in toRelease)
                ReleaseBar(node);
        }
    }

    // ── Binding ─────────────────────────────────────────────────────────────

    protected override bool Bind(VisualElement root)
    {
        _root = root.Q<VisualElement>("ResourceHPRoot");
        return _root != null;
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void OnResourceDamaged(ResourceDamaged evt)
    {
        if (evt.Node == null || _root == null) return;

        if (!_active.TryGetValue(evt.Node, out var entry))
        {
            entry = AcquireBar(evt.Node);
            if (entry.Bar == null) return;
        }

        // Update fill width — white shrinks as HP depletes, revealing black bg
        float ratio = evt.Node.MaxHealth > 0
            ? (float)evt.RemainingHp / evt.Node.MaxHealth : 0f;
        entry.Fill.style.width = new StyleLength(Length.Percent(ratio * 100f));

        // Show and reset timer
        entry.Bar.AddToClassList("health-bar--visible");
        entry.Bar.RemoveFromClassList("health-bar--fading");
        entry.Bar.style.opacity = 1f;
        entry.Bar.style.transitionDuration = StyleKeyword.Null;
        entry.Fading = false;
        entry.HideTime = Time.time + _displayDuration;

        _active[evt.Node] = entry;
    }

    private void OnResourceDepleted(ResourceDepleted evt)
    {
        if (evt.Node == null) return;
        if (!_active.TryGetValue(evt.Node, out var entry)) return;

        entry.Fill.style.width = 0f;
        entry.Fading = true;
        entry.HideTime = Time.time;
        entry.Bar.AddToClassList("health-bar--fading");
        entry.Bar.style.transitionDuration =
            new List<TimeValue> { new TimeValue(_fadeDuration) };

        _active[evt.Node] = entry;
    }

    // ── Pool acquire / release ──────────────────────────────────────────────

    private BarEntry AcquireBar(ResourceNode node)
    {
        VisualElement bar;
        if (_pool.Count > 0)
        {
            bar = _pool.Pop();
        }
        else
        {
            bar = CreateBarElement();
        }

        bar.name = $"HPBar_{node.name}";
        _root.Add(bar);

        var fill = bar.Q<VisualElement>("BarFill");
        var entry = new BarEntry
        {
            Bar = bar,
            Fill = fill,
            HideTime = Time.time + _displayDuration,
            Fading = false,
        };

        _active[node] = entry;
        return entry;
    }

    private void ReleaseBar(ResourceNode node)
    {
        if (!_active.TryGetValue(node, out var entry)) return;

        entry.Bar.RemoveFromClassList("health-bar--visible");
        entry.Bar.RemoveFromClassList("health-bar--fading");
        entry.Bar.style.opacity = StyleKeyword.Null;
        entry.Bar.style.transitionDuration = StyleKeyword.Null;
        entry.Fill.style.width = new StyleLength(Length.Percent(100f));
        entry.Bar.name = "HealthBar_Pooled";

        _root.Remove(entry.Bar);
        _active.Remove(node);
        _pool.Push(entry.Bar);
    }

    // ── Bar element factory ─────────────────────────────────────────────────

    private VisualElement CreateBarElement()
    {
        if (_healthBarTemplate != null)
        {
            var container = _healthBarTemplate.CloneTree();
            return container.ElementAt(0) ?? container;
        }

        // Fallback: build in code (should not happen when template is assigned)
        var bar = new VisualElement { name = "HealthBar" };
        bar.AddToClassList("health-bar");

        var bg = new VisualElement { name = "BarBackground" };
        bg.AddToClassList("health-bar__bg");

        var fill = new VisualElement { name = "BarFill" };
        fill.AddToClassList("health-bar__fill");

        bg.Add(fill);
        bar.Add(bg);
        return bar;
    }
}
