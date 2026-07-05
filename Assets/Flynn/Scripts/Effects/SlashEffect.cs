using System.Collections;
using UnityEngine;

using Flynn.Core;
using Flynn.Common;
using Flynn.Events;
using Flynn.Player;

namespace Flynn.Effects
{
    /// <summary>
    /// Spawns a procedural arc sprite that scales up from the player toward
    /// the mouse and fades. Triggered by ToolSwingStarted.
    /// </summary>
    public class SlashEffect : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float _duration = 0.2f;
        [SerializeField] private float _startScale = 0.05f;
        [SerializeField] private float _endScale = 0.2f;
        [SerializeField] private float _offset = 0.3f;
        [Tooltip("Total arc the crescent sweeps through, matching the wrench swing.")]
        [SerializeField] private float _sweepArcDegrees = 160f;

        [Header("Color")]
        [SerializeField] private Color _color = new Color(1f, 1f, 0.8f, 0.6f);

        [Header("Hit Effect")]
        [SerializeField] private float _hitDuration = 0.25f;
        [SerializeField] private float _hitStartScale = 0.1f;
        [SerializeField] private float _hitEndScale = 0.4f;
        [SerializeField] private Color _hitColor = new Color(1f, 0.9f, 0.5f, 0.9f);

        [Header("Refs")]
        [SerializeField] private PlayerAnchor _playerAnchor;
        [SerializeField] private SpriteSortingConfigSO _sortingConfig;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Subscribe<ToolSwingStarted>(OnSwing);
                GameEventBus.Instance.Subscribe<ResourceHit>(OnResourceHit);
            }
        }

        private void OnDisable()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Unsubscribe<ToolSwingStarted>(OnSwing);
                GameEventBus.Instance.Unsubscribe<ResourceHit>(OnResourceHit);
            }
        }

        // ── Event handler ─────────────────────────────────────────────────────────

        private void OnSwing(ToolSwingStarted evt)
        {
            if (_playerAnchor == null || !_playerAnchor.HasPlayer) return;

            Vector3 playerPos = _playerAnchor.Current.position;
            Vector2 dir = evt.AimPoint - playerPos;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            dir.Normalize();

            var pc = _playerAnchor.Current.GetComponent<PlayerController2D>();
            if (pc != null) pc.FaceDirection(dir);

            StartCoroutine(SlashRoutine(playerPos, dir));
        }

        private void OnResourceHit(ResourceHit evt)
        {
            Vector3 hitPos = evt.Node.transform.position;
            StartCoroutine(HitBurstRoutine(hitPos));
        }

        // ── Slash animation ──────────────────────────────────────────────────────

        private IEnumerator SlashRoutine(Vector3 playerPos, Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var pivot = new GameObject("_slashPivot");
            pivot.transform.position = playerPos;
            pivot.transform.rotation = Quaternion.Euler(0, 0, angle);

            var spriteGO = new GameObject("_slashSprite");
            spriteGO.transform.SetParent(pivot.transform, false);
            spriteGO.transform.localPosition = new Vector3(_offset, 0, 0);

            var sr = spriteGO.AddComponent<SpriteRenderer>();
            sr.sprite = GetSlashSprite();
            sr.color = _color;
            sr.sortingOrder = ComputeSortOrder(playerPos) + 100;

            float half = _sweepArcDegrees * 0.5f;
            float t = 0f;
            while (t < _duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / _duration;
                float ease = 1f - (1f - p) * (1f - p);

                // Crescent sweeps through the arc with the wrench instead of
                // popping statically at the aim point.
                pivot.transform.rotation = Quaternion.Euler(0f, 0f, angle + Mathf.Lerp(half, -half, ease));

                float s = Mathf.Lerp(_startScale, _endScale, ease);
                spriteGO.transform.localScale = Vector3.one * s;

                var c = sr.color;
                c.a = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
                sr.color = c;

                yield return null;
            }

            Destroy(pivot);
        }

        // ── Hit burst animation ───────────────────────────────────────────────────

        private IEnumerator HitBurstRoutine(Vector3 hitPos)
        {
            var go = new GameObject("_hitBurst");
            go.transform.position = hitPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetHitBurstSprite();
            sr.color = _hitColor;
            sr.sortingOrder = ComputeSortOrder(hitPos) + 200;

            float t = 0f;
            while (t < _hitDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / _hitDuration;
                float ease = 1f - (1f - p) * (1f - p);

                float s = Mathf.Lerp(_hitStartScale, _hitEndScale, ease);
                go.transform.localScale = Vector3.one * s;

                var c = sr.color;
                c.a = (1f - p) * _hitColor.a;
                sr.color = c;

                yield return null;
            }

            Destroy(go);
        }

        // ── Sorting ──────────────────────────────────────────────────────────────

        private int ComputeSortOrder(Vector3 position)
        {
            if (_sortingConfig == null) return 5100;
            float depth = Vector3.Dot(position, _sortingConfig.SortAxisNormalized);
            return _sortingConfig.baseOrder
                 - Mathf.RoundToInt(depth * _sortingConfig.stepsPerUnit)
                 + 10;
        }

        // ── Procedural arc sprite ────────────────────────────────────────────────

        private static Sprite _slashSprite;

        private void Awake()
        {
            _slashSprite = null;
            _hitBurstSprite = null;
        }

        /// <summary>
        /// Thick white arc (crescent) facing right. Pivot at 15% so it extends
        /// outward from the player. pixelsPerUnit = 32 for chunky pixels.
        /// </summary>
        private static Sprite GetSlashSprite()
        {
            if (_slashSprite != null) return _slashSprite;

            const int w = 96, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            float baseInner = h * 0.18f;
            float baseOuter = h * 0.44f;
            float maxAng = Mathf.PI * 0.55f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);

                    // Taper: band narrows to zero at the arc tips
                    float t = 1f - Mathf.Abs(ang) / maxAng;       // 1 at center → 0 at tips
                    float taper = t * t;                             // ease toward tips

                    float innerR = Mathf.Lerp(baseOuter, baseInner, taper);
                    float outerR = baseOuter;

                    // Smooth band with soft edges
                    float band = Mathf.Clamp01((outerR - dist) / 3f) * Mathf.Clamp01((dist - innerR) / 3f);

                    // Smooth arc cutoff
                    float arc = Mathf.Clamp01(t);

                    float a = band * arc;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            _slashSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32);
            return _slashSprite;
        }

        private static Sprite _hitBurstSprite;

        /// <summary>
        /// Radial burst — soft circle that expands and fades. pixelsPerUnit = 32.
        /// </summary>
        private static Sprite GetHitBurstSprite()
        {
            if (_hitBurstSprite != null) return _hitBurstSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxRadius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Soft falloff from center
                    float a = Mathf.Clamp01(1f - dist / maxRadius);
                    a = a * a;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            _hitBurstSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32);
            return _hitBurstSprite;
        }
    }
}
