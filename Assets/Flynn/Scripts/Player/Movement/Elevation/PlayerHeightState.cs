using System.Collections.Generic;
using UnityEngine;
using Flynn.Common;

namespace Flynn.Player
{
    /// <summary>
    /// Single source of truth for the player's elevation on the isometric grid (fake-Z).
    ///
    /// The body root stays on the ground tile (foot-sort + physics unaffected); only a child
    /// <see cref="_visualRoot"/> is lifted up screen-Y by <c>Elevation + JumpOffset</c>. The
    /// shadow stays on the ground and fades with the transient jump lift.
    ///
    /// This is the ONLY component that writes <c>_visualRoot.localPosition.y</c>. The jump
    /// controller contributes its arc via <see cref="JumpOffset"/>; ramps drive
    /// <see cref="SetContinuousElevation"/>; zones/ledges/jumps land via <see cref="SetLevel"/>.
    /// </summary>
    [DefaultExecutionOrder(-40)] // after PlayerController2D (-50), before SortableSprite (default 0)
    public class PlayerHeightState : MonoBehaviour
    {
        [Header("Visual (single writer)")]
        [Tooltip("Child transform that receives the vertical elevation + jump lift.")]
        [SerializeField] private Transform _visualRoot;
        [Tooltip("Child transform for the ground shadow (stays at ground, fades on jump).")]
        [SerializeField] private Transform _shadowRoot;
        [Tooltip("SpriteRenderer on the shadow (alpha fades as the jump lift grows).")]
        [SerializeField] private SpriteRenderer _shadowRenderer;

        [Header("Elevation")]
        [Tooltip("World-Y lift applied per whole elevation level. Tune against the 2:1 cell (cellSize.y = 0.5) so one level ≈ one tile-height.")]
        [SerializeField] private float _unitsPerLevel = 0.5f;
        [Tooltip("How fast Elevation eases toward its target (units/sec-ish via Lerp factor).")]
        [SerializeField] private float _lerpSpeed = 10f;

        [Header("Shadow Fade")]
        [Tooltip("Shadow alpha at full jump peak (0 = invisible at apex).")]
        [Range(0f, 1f)][SerializeField] private float _shadowMinAlpha = 0.25f;
        [Tooltip("Shadow scale at full jump peak (smaller = higher-reading jump).")]
        [Range(0f, 1f)][SerializeField] private float _shadowMinScale = 0.6f;
        [Tooltip("Jump lift (world-Y) that maps to the fully-faded shadow.")]
        [SerializeField] private float _shadowFadeRefHeight = 0.4f;

        [Header("Sorting")]
        [Tooltip("SortableSprite to bias by elevation (higher = drawn in front). Optional.")]
        [SerializeField] private SortableSprite _sortable;

        [Header("Collision Tiering (optional)")]
        [Tooltip("Leave empty for pure trigger-zone mode (no per-tier collision gating).\n" +
                 "Otherwise one layer name per elevation level, index = level.")]
        [SerializeField] private string[] _tierLayerNames;

        // ── State ──────────────────────────────────────────────────────────────
        private float _elevation;        // current applied world-Y of the standing surface
        private float _targetElevation;  // where Elevation is easing toward
        private int   _heightLevel;
        private bool  _snapNext;

        private Vector3 _visualBaseLocal;
        private Vector3 _shadowBaseLocal;
        private Vector3 _shadowBaseScale;
        private Color   _shadowBaseColor;

        private TierCollision _tier;

        // ── Public API ─────────────────────────────────────────────────────────
        /// <summary>Current eased world-Y of the surface the player stands on.</summary>
        public float Elevation   => _elevation;
        /// <summary>Discrete elevation tier (drives collision + sort).</summary>
        public int   HeightLevel => _heightLevel;
        /// <summary>World-Y lift per whole level (read by ramps/jumps).</summary>
        public float UnitsPerLevel => _unitsPerLevel;
        /// <summary>Transient jump arc lift, set by PlayerJumpController each frame.</summary>
        public float JumpOffset { get; set; }

        /// <summary>Snap or ease to a discrete elevation level (zones, jump landings).</summary>
        public void SetLevel(int level, bool snap)
        {
            if (level != _heightLevel)
            {
                _heightLevel = level;
                _tier?.Apply(level);
            }
            _targetElevation = level * _unitsPerLevel;
            _snapNext = snap;
        }

        /// <summary>Continuous elevation control for ramps. Eases toward <paramref name="worldY"/>
        /// and updates the discrete level (for collision/sort) without snapping.</summary>
        public void SetContinuousElevation(float worldY, int level)
        {
            _targetElevation = worldY;
            if (level != _heightLevel)
            {
                _heightLevel = level;
                _tier?.Apply(level);
            }
        }

        private void Awake()
        {
            if (_visualRoot != null) _visualBaseLocal = _visualRoot.localPosition;
            if (_shadowRoot != null) { _shadowBaseLocal = _shadowRoot.localPosition; _shadowBaseScale = _shadowRoot.localScale; }
            if (_shadowRenderer != null) _shadowBaseColor = _shadowRenderer.color;

            if (_tierLayerNames != null && _tierLayerNames.Length > 0)
                _tier = new TierCollision(gameObject.layer, _tierLayerNames);

            _elevation = _targetElevation = _heightLevel * _unitsPerLevel;
            _tier?.Apply(_heightLevel);
        }

        private void Update()
        {
            // Ease toward target elevation (ramps set it continuously; jumps snap on land)
            if (_snapNext)
            {
                _elevation = _targetElevation;
                _snapNext = false;
            }
            else
            {
                _elevation = Mathf.Lerp(_elevation, _targetElevation, 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime));
            }

            // Push elevation into the depth-sort: float for the order bias, level for the
            // (coarse) per-level sorting layer that cleanly crosses static tilemap roofs.
            if (_sortable != null)
            {
                _sortable.Elevation = _elevation;
                _sortable.ElevationLevel = _heightLevel;
            }
        }

        private void LateUpdate()
        {
            float lift = _elevation + JumpOffset;

            if (_visualRoot != null)
                _visualRoot.localPosition = _visualBaseLocal + new Vector3(0f, lift, 0f);

            // Shadow sits on the CURRENT surface (rides standing Elevation — the platform is "ground"),
            // but NOT the transient jump; the jump arc is what separates the player from the shadow.
            float jump01 = _shadowFadeRefHeight > 0f ? Mathf.Clamp01(JumpOffset / _shadowFadeRefHeight) : 0f;
            if (_shadowRoot != null)
            {
                _shadowRoot.localPosition = _shadowBaseLocal + new Vector3(0f, _elevation, 0f);
                _shadowRoot.localScale = _shadowBaseScale * Mathf.Lerp(1f, _shadowMinScale, jump01);
            }
            if (_shadowRenderer != null)
            {
                Color c = _shadowBaseColor;
                c.a = _shadowBaseColor.a * Mathf.Lerp(1f, _shadowMinAlpha, jump01);
                _shadowRenderer.color = c;
            }
        }
    }

    /// <summary>
    /// Optional, lean per-tier collision gating helper (not a component): the player collides
    /// only with the collider layer of its CURRENT elevation tier, ignoring all others. NOT a
    /// full N×N matrix — just "active tier on, the rest off" toggled on level change. Bundled
    /// here because only <see cref="PlayerHeightState"/> uses it.
    /// </summary>
    public class TierCollision
    {
        private readonly int _playerLayer;
        private readonly List<int> _tierLayers = new List<int>();

        public TierCollision(int playerLayer, string[] tierLayerNames)
        {
            _playerLayer = playerLayer;
            foreach (var name in tierLayerNames)
            {
                int l = LayerMask.NameToLayer(name);
                if (l >= 0) _tierLayers.Add(l);
                else Debug.LogWarning($"[TierCollision] Layer '{name}' not found — add it in Project Settings > Tags and Layers.");
            }
        }

        /// <summary>Enable collision with the active tier's layer, ignore all others.</summary>
        public void Apply(int activeLevel)
        {
            for (int i = 0; i < _tierLayers.Count; i++)
            {
                bool ignore = (i != activeLevel);
                Physics2D.IgnoreLayerCollision(_playerLayer, _tierLayers[i], ignore);
            }
        }
    }
}
