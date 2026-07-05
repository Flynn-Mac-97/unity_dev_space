using UnityEngine;
using Flynn.Npc;
using Flynn.Resources;

namespace Flynn.Player.Combat
{
    /// <summary>
    /// Live swing-target feedback. Every frame asks WrenchController.ResolveAim
    /// what a click right now would hit:
    /// - a node would be hit → warm pulsing ellipse under that node (soft lock)
    /// - nothing in reach → dim ellipse at the range-clamped aim point, so the
    ///   player learns the swing's reach without any UI text.
    /// Lives on the Player next to WrenchController.
    /// </summary>
    public class SwingTargetIndicator : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Optional sprite override; when null a crisp pixel-art ring is generated.")]
        [SerializeField] private Sprite _ringSprite;
        [SerializeField] private Color _targetColor = new Color(1f, 0.9f, 0.55f, 0.9f);
        [SerializeField] private Color _reachColor = new Color(1f, 1f, 1f, 0.3f);
        [Tooltip("Marker world width when locked on a node.")]
        [SerializeField] private float _targetSize = 0.9f;
        [SerializeField] private float _reachSize = 0.5f;
        [SerializeField] private float _pulseSpeed = 5f;
        [Tooltip("Marker offset below the target's pivot (node bases sit at pivot).")]
        [SerializeField] private Vector2 _markerOffset = new Vector2(0f, -0.08f);

        private WrenchController _wrench;
        private SpriteRenderer _marker;
        private ResourceNode _lastNode;

        private void Awake()
        {
            _wrench = GetComponent<WrenchController>();

            var go = new GameObject("_swingTarget");
            go.transform.SetParent(null);
            _marker = go.AddComponent<SpriteRenderer>();
            _marker.sprite = _ringSprite != null ? _ringSprite : PixelRing();
            _marker.enabled = false;
        }

        private static Sprite _pixelRing;

        /// <summary>Crisp 2:1 iso ellipse outline, point-filtered — no glow, no gradient.</summary>
        private static Sprite PixelRing()
        {
            if (_pixelRing != null) return _pixelRing;

            const int w = 48, h = 24;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            float a = w * 0.5f - 1f, bAx = h * 0.5f - 1f;
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / a;
                    float dy = (y + 0.5f - h * 0.5f) / bAx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // ~1.5 texel band = clean 1-2px outline at native size
                    tex.SetPixel(x, y, Mathf.Abs(d - 1f) < 0.09f ? (Color)white : (Color)clear);
                }
            }
            tex.Apply();
            _pixelRing = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 48f);
            _pixelRing.name = "_pixelRing";
            return _pixelRing;
        }

        private void OnDestroy()
        {
            if (_marker != null) Destroy(_marker.gameObject);
        }

        private void LateUpdate()
        {
            if (_wrench == null || _marker == null) return;

            bool active = _wrench.WrenchIsHome && !DialogueManager.IsDialogueOpen;
            if (!active)
            {
                _marker.enabled = false;
                return;
            }

            var node = _wrench.ResolveAim(out Vector3 aimPoint);
            // Pixel-art ring: pulse alpha, never scale (scaling shimmers point-filtered pixels).
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * _pulseSpeed);

            if (node != null)
            {
                var nodeSR = node.GetComponentInChildren<SpriteRenderer>();
                _marker.transform.position = node.transform.position + (Vector3)_markerOffset;
                var c = _targetColor; c.a *= pulse;
                _marker.color = c;
                SetWidth(_targetSize);
                if (nodeSR != null)
                {
                    _marker.sortingLayerID = nodeSR.sortingLayerID;
                    _marker.sortingOrder = nodeSR.sortingOrder - 1;
                }
                _lastNode = node;
            }
            else
            {
                _marker.transform.position = aimPoint + (Vector3)_markerOffset;
                _marker.color = _reachColor;
                SetWidth(_reachSize);
                _marker.sortingLayerName = "Level0";
                _marker.sortingOrder = 1;
                _lastNode = null;
            }
            _marker.enabled = true;
        }

        private void SetWidth(float worldWidth)
        {
            if (_marker.sprite == null) return;
            float native = _marker.sprite.bounds.size.x;
            if (native <= 0f) return;
            // Texture is already a 2:1 iso ellipse — scale uniformly.
            float s = worldWidth / native;
            _marker.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
