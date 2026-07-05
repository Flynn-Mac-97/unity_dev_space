using System;
using System.Collections.Generic;
using UnityEngine;


using Flynn.Core;
using Flynn.Player;
using Flynn.Resources;

namespace Flynn.UI.Core
{
    /// <summary>
    /// Central UI relay — same factory/hub pattern as ResourceManager and PlayerInventory: gameplay
    /// systems fire an inbound signal (<see cref="SignalShow"/> / <see cref="SignalHide"/> /
    /// <see cref="SignalToggle"/>), the manager routes it to the matching reusable panel, then
    /// broadcasts an outbound event (<see cref="OnPanelShown"/> / <see cref="OnPanelHidden"/>).
    ///
    /// Callers never reference a concrete screen — they only know a <see cref="UIPanelId"/> and reach
    /// the manager through <see cref="Instance"/>. The manager owns the list of reusable panels and
    /// reuses them (show/hide), never instantiating per request.
    ///
    /// One per scene. Runs early (negative execution order) so <see cref="Instance"/> and the registry
    /// exist before any caller's Awake/OnEnable.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Tooltip("Log each show/hide signal. Off for production.")]
        [SerializeField] private bool _debugLog = true;

        [Tooltip("Reusable panels managed here. Drop UIPanel components in, or Register at runtime.")]
        [SerializeField] private List<UIPanel> _panels = new();

        private readonly Dictionary<UIPanelId, IUIPanel> _registry = new();

        /// <summary>Raised after a panel is shown. Subscribe with += on startup.</summary>
        public event Action<UIPanelId> OnPanelShown;
        /// <summary>Raised after a panel is hidden. Subscribe with += on startup.</summary>
        public event Action<UIPanelId> OnPanelHidden;

        // ── Signals (inbound) ───────────────────────────────────────────────────────

        /// <summary>Signal: show the panel with this id. No-op (warns) if none is registered.</summary>
        public void SignalShow(UIPanelId id)
        {
            if (!TryGet(id, out IUIPanel panel)) return;
            if (_debugLog) Debug.Log($"[UIManager] show {id}");
            panel.Show();
            OnPanelShown?.Invoke(id);
        }

        /// <summary>Signal: hide the panel with this id.</summary>
        public void SignalHide(UIPanelId id)
        {
            if (!TryGet(id, out IUIPanel panel)) return;
            if (_debugLog) Debug.Log($"[UIManager] hide {id}");
            panel.Hide();
            OnPanelHidden?.Invoke(id);
        }

        /// <summary>Signal: toggle the panel's visibility.</summary>
        public void SignalToggle(UIPanelId id)
        {
            if (!TryGet(id, out IUIPanel panel)) return;
            if (panel.IsVisible) SignalHide(id);
            else SignalShow(id);
        }

        public bool IsVisible(UIPanelId id) => TryGet(id, out IUIPanel p, warn: false) && p.IsVisible;

        // ── Registration ──────────────────────────────────────────────────────────

        /// <summary>Register a panel at runtime (panels in the serialized list register automatically).</summary>
        public void Register(IUIPanel panel)
        {
            if (panel == null || panel.PanelId == UIPanelId.None) return;
            _registry[panel.PanelId] = panel;
        }

        public void Unregister(IUIPanel panel)
        {
            if (panel == null) return;
            if (_registry.TryGetValue(panel.PanelId, out IUIPanel existing) && existing == panel)
                _registry.Remove(panel.PanelId);
        }

        // ── Unity messages ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[UIManager] Duplicate; destroying {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            foreach (UIPanel panel in _panels)
            {
                if (panel == null) continue;
                if (panel.PanelId == UIPanelId.None)
                {
                    Debug.LogWarning($"[UIManager] '{panel.name}' has PanelId.None; skipped.", panel);
                    continue;
                }
                Register(panel);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private bool TryGet(UIPanelId id, out IUIPanel panel, bool warn = true)
        {
            if (_registry.TryGetValue(id, out panel) && panel != null) return true;
            if (warn) Debug.LogWarning($"[UIManager] No panel registered for {id}.", this);
            return false;
        }
    }

}
