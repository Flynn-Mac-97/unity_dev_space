using UnityEngine;
using Flynn.UI.Core;

namespace Flynn.Common
{
    /// <summary>
    /// Drop on any GameObject with a SpriteRenderer + Collider2D to get:
    /// • Hover tracking (IsHovered, static IsHovered(target))
    /// • Outline highlight via material swap — swaps the SpriteRenderer's material
    ///   to the outline material on hover, restores the original on un-hover.
    /// • Scale pop on hover.
    /// Settings come from a HoverProfile SO so all objects of the same type
    /// share the same values. Override with a per-instance profile if needed.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Hoverable : MonoBehaviour
    {
        [Header("Profile")]
        [Tooltip("Shared hover settings. Required — no profile means no hover behaviour.")]
        [SerializeField] private HoverProfile _profile;

        private SpriteRenderer _renderer;
        private Material _originalMaterial;
        private bool _isHovered;
        private Vector3 _originalScale;

        private Material OutlineMat => _profile != null ? _profile.outlineMaterial : null;
        private float HoverScale => _profile != null ? _profile.hoverScale : 1f;
        private float ScaleLerpSpeed => _profile != null ? _profile.scaleLerpSpeed : 12f;

        /// <summary>True when the mouse cursor is over this object.</summary>
        public bool IsHovered => _isHovered;

        /// <summary>The currently hovered Hoverable in the scene (or null).</summary>
        public static Hoverable Current { get; private set; }

        /// <summary>Static helper: is this specific GameObject (or its parent) currently hovered?</summary>
        public static bool IsHoveredTarget(GameObject go)
        {
            if (Current == null) return false;
            return Current.gameObject == go || go.transform.IsChildOf(Current.transform);
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _originalMaterial = _renderer != null ? _renderer.sharedMaterial : null;
            _originalScale = transform.localScale;

            if (_profile == null)
                Debug.LogWarning("[Hoverable] " + gameObject.name + " has no HoverProfile assigned — hover will not work.", this);
        }

        private void OnEnable()
        {
            if (MousePointer.Instance != null)
                MousePointer.Instance.OnHoverChanged += HandleHoverChanged;
        }

        private void OnDisable()
        {
            if (MousePointer.Instance != null)
                MousePointer.Instance.OnHoverChanged -= HandleHoverChanged;
            if (Current == this)
            {
                RestoreMaterial();
                Current = null;
            }
            if (_isHovered) SetHovered(false);
        }

        private void HandleHoverChanged(GameObject hovered)
        {
            bool isMe = hovered != null && (hovered == gameObject || hovered.transform.IsChildOf(transform));
            if (isMe == _isHovered) return;
            SetHovered(isMe);
            if (_isHovered) Current = this;
            else if (Current == this) Current = null;
        }

        private void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            if (_renderer == null || OutlineMat == null) return;

            if (hovered)
            {
                if (_originalMaterial == null || _renderer.sharedMaterial == OutlineMat)
                    _originalMaterial = _renderer.sharedMaterial;
                _renderer.material = OutlineMat;
            }
            else
            {
                RestoreMaterial();
            }
        }

        private void RestoreMaterial()
        {
            if (_renderer != null && _originalMaterial != null)
                _renderer.material = _originalMaterial;
        }

        private void Update()
        {
            if (HoverScale > 1f)
            {
                Vector3 target = _isHovered
                    ? _originalScale * HoverScale
                    : _originalScale;
                transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * ScaleLerpSpeed);
            }
        }
    }
}
