using System.Collections;
using UnityEngine;
using Flynn.Common;
using Flynn.Core;
using Flynn.Events;
using Flynn.UI.Core;

namespace Flynn.Player.Combat
{
    /// <summary>
    /// All player-facing wrench feedback, driven purely by bus events:
    /// - radial charge ring at the player's feet (fill arc + sweetspot wedge + perfect burst)
    /// - throw aim dotted line + landing reticle (length grows with charge)
    /// - impact juice for ToolHitTarget (hit-stop, camera shake, impact FX)
    /// - catch pop on ToolReturned.
    /// Sits on the Player. No scene wiring beyond the settings asset.
    /// </summary>
    public class WrenchChargeFX : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PowerBuildupSettings _settings;
        [SerializeField] private SpriteSortingConfigSO _sortingConfig;

        [Header("Ring (black & white placeholder style)")]
        [SerializeField] private float _ringRadius = 0.26f;
        [Tooltip("Player pivot is at the feet — offset up to center on the sprite.")]
        [SerializeField] private Vector2 _ringOffset = new Vector2(0f, 0.3f);
        [SerializeField] private float _ringWidth = 0.018f;
        [SerializeField] private Color _backColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _fillColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color _zoneIdleColor = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private Color _zoneHotColor = new Color(1f, 1f, 1f, 1f);

        [Header("Aim Line")]
        [SerializeField] private float _dotSpacing = 0.5f;
        [SerializeField] private Color _aimColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color _reticleColor = new Color(1f, 1f, 1f, 0.9f);

        private const int RingSegments = 48;
        private const int MaxDots = 20;

        private LineRenderer _backRing;
        private LineRenderer _fillArc;
        private LineRenderer _zoneArc;
        private SpriteRenderer[] _dots;
        private SpriteRenderer _reticle;
        private static Material _lineMat;

        private bool _charging;
        private ActionType _chargeType;
        private float _charge;
        private bool _inZone;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            _backRing = MakeRing("_chargeBack", _ringWidth * 2.2f);
            _fillArc = MakeRing("_chargeFill", _ringWidth);
            _zoneArc = MakeRing("_chargeZone", _ringWidth * 1.6f);
            MakeAimPool();
            HideAll();
        }

        private void OnEnable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Subscribe<PowerBuildupStarted>(OnBuildupStarted);
            bus.Subscribe<PowerBuildupChanged>(OnBuildupChanged);
            bus.Subscribe<PowerBuildupReleased>(OnBuildupReleased);
            bus.Subscribe<ToolHitTarget>(OnToolHit);
            bus.Subscribe<ToolReturned>(OnToolReturned);
            bus.Subscribe<ResourceDepleted>(OnResourceDepleted);
        }

        private void OnDisable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Unsubscribe<PowerBuildupStarted>(OnBuildupStarted);
            bus.Unsubscribe<PowerBuildupChanged>(OnBuildupChanged);
            bus.Unsubscribe<PowerBuildupReleased>(OnBuildupReleased);
            bus.Unsubscribe<ToolHitTarget>(OnToolHit);
            bus.Unsubscribe<ToolReturned>(OnToolReturned);
            bus.Unsubscribe<ResourceDepleted>(OnResourceDepleted);
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnBuildupStarted(PowerBuildupStarted evt)
        {
            _charging = true;
            _chargeType = evt.Type;
            _charge = 0f;
            _backRing.gameObject.SetActive(true);
            _fillArc.gameObject.SetActive(true);
            _zoneArc.gameObject.SetActive(true);
        }

        private void OnBuildupChanged(PowerBuildupChanged evt)
        {
            _charge = evt.Normalized;
            _inZone = evt.InPerfectZone;
        }

        private void OnBuildupReleased(PowerBuildupReleased evt)
        {
            _charging = false;
            HideAll();
            if (evt.IsPerfect)
                StartCoroutine(BurstRoutine(RingCenter(), _zoneHotColor, 1.1f));
        }

        private void OnToolHit(ToolHitTarget evt)
        {
            var hit = evt.Hit;
            bool perfect = _settings != null &&
                ChargeMath.InPerfectZone(hit.Charge, _settings.perfectZoneStart, _settings.perfectZoneEnd);

            // Cozy tuning: ordinary hits get NO time freeze — feedback comes from
            // chips/flash/sound. Only a perfect charged strike earns a small stop;
            // the node break (below) is the drama beat.
            if (Flynn.Effects.HitStop.Instance != null && perfect)
                Flynn.Effects.HitStop.Instance.Play(0.6f);

            if (Flynn.Effects.HitImpactFX.Instance != null)
                Flynn.Effects.HitImpactFX.Instance.PlayHit(
                    hit.HitPoint, hit.HitDirection, hit.Damage, Color.white);

            // No camera shake on throw hits — pierce chains would rattle the screen.
            // Swing shake stays in WrenchController (single hit, node-tuned amount).
        }

        private void OnToolReturned(ToolReturned evt)
        {
            StartCoroutine(BurstRoutine(transform.position, _aimColor, 0.5f));
        }

        private void OnResourceDepleted(ResourceDepleted evt)
        {
            // Node break = the one drama beat: short stop + gentle shake.
            if (Flynn.Effects.HitStop.Instance != null)
                Flynn.Effects.HitStop.Instance.Play(1f);
            if (Flynn.Effects.CameraShake.Instance != null)
                Flynn.Effects.CameraShake.Instance.Shake(0.07f, 0.2f);
        }

        // ── Per-frame visuals ─────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_charging) return;

            DrawBackRing();
            DrawFillArc(_charge);
            DrawZoneArc();

            bool showAim = _chargeType == ActionType.Throw;
            if (showAim) DrawAimLine();
            else HideAimLine();
        }

        private Vector3 RingCenter() => transform.position + (Vector3)_ringOffset;

        private void DrawBackRing()
        {
            // Full thin black circle — makes the white fill readable on any ground.
            _backRing.positionCount = RingSegments + 1;
            for (int i = 0; i <= RingSegments; i++)
            {
                float ang = (90f - (float)i / RingSegments * 360f) * Mathf.Deg2Rad;
                _backRing.SetPosition(i, RingCenter() + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * _ringRadius);
            }
            _backRing.startColor = _backRing.endColor = _backColor;
            _backRing.sortingOrder = SortAt(RingCenter()) - 6; // just under fill/zone
        }

        private void DrawFillArc(float normalized)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(RingSegments * normalized) + 1);
            _fillArc.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = (float)(i) / RingSegments;                    // fraction of full circle
                float ang = (90f - t * 360f) * Mathf.Deg2Rad;            // start at 12 o'clock, clockwise
                _fillArc.SetPosition(i, RingCenter() + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * _ringRadius);
            }
            _fillArc.startColor = _fillArc.endColor = _fillColor;
            ApplySort(_fillArc);
        }

        private void DrawZoneArc()
        {
            if (_settings == null) return;
            float from = _settings.perfectZoneStart, to = _settings.perfectZoneEnd;
            int count = Mathf.Max(2, Mathf.CeilToInt(RingSegments * (to - from)) + 1);
            _zoneArc.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Lerp(from, to, (float)i / (count - 1));
                float ang = (90f - t * 360f) * Mathf.Deg2Rad;
                _zoneArc.SetPosition(i, RingCenter() + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * _ringRadius);
            }

            Color c = _inZone ? _zoneHotColor : _zoneIdleColor;
            if (_inZone)
            {
                // Soft pulse while the release window is open (unscaled: survives hit-stop).
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 24f);
                c.a *= pulse;
            }
            _zoneArc.startColor = _zoneArc.endColor = c;
            ApplySort(_zoneArc);
        }

        private void DrawAimLine()
        {
            if (_settings == null || MousePointer.Instance == null) return;

            Vector3 origin = transform.position;
            Vector2 toMouse = MousePointer.Instance.WorldPoint - origin;
            float mouseDist = toMouse.magnitude;
            if (mouseDist < 0.01f) return;
            Vector2 dir = toMouse / mouseDist;

            // The wrench lands at the cursor; charge caps the reachable distance.
            float range = Mathf.Min(mouseDist,
                Mathf.Lerp(_settings.throwRangeLight, _settings.throwRangeHeavy, _charge));
            Vector3 start = origin + (Vector3)(dir * 0.6f);
            int dotCount = Mathf.Min(MaxDots, Mathf.FloorToInt((range - 0.6f) / _dotSpacing));

            for (int i = 0; i < _dots.Length; i++)
            {
                bool on = i < dotCount;
                _dots[i].gameObject.SetActive(on);
                if (!on) continue;
                Vector3 p = start + (Vector3)(dir * (_dotSpacing * (i + 1)));
                _dots[i].transform.position = p;
                Color c = _aimColor;
                c.a *= Mathf.Lerp(1f, 0.35f, (float)i / Mathf.Max(1, dotCount)); // fade with distance
                _dots[i].color = c;
                _dots[i].sortingOrder = SortAt(p) + 30;
            }

            _reticle.gameObject.SetActive(true);
            Vector3 end = origin + (Vector3)(dir * range);
            _reticle.transform.position = end;
            _reticle.transform.Rotate(0f, 0f, 90f * Time.unscaledDeltaTime);
            _reticle.color = _inZone ? _zoneHotColor : _reticleColor;
            _reticle.sortingOrder = SortAt(end) + 31;
        }

        private void HideAimLine()
        {
            if (_dots == null) return;
            foreach (var d in _dots) d.gameObject.SetActive(false);
            if (_reticle != null) _reticle.gameObject.SetActive(false);
        }

        private void HideAll()
        {
            if (_backRing != null) _backRing.gameObject.SetActive(false);
            if (_fillArc != null) _fillArc.gameObject.SetActive(false);
            if (_zoneArc != null) _zoneArc.gameObject.SetActive(false);
            HideAimLine();
        }

        // ── Perfect / catch burst ─────────────────────────────────────────

        private IEnumerator BurstRoutine(Vector3 pos, Color color, float scale)
        {
            var go = new GameObject("_wrenchBurst");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SoftCircleSprite();
            sr.color = color;
            sr.sortingOrder = SortAt(pos) + 40;

            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                float ease = 1f - (1f - p) * (1f - p);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, scale, ease);
                var c = sr.color;
                c.a = (1f - p) * color.a;
                sr.color = c;
                yield return null;
            }
            Destroy(go);
        }

        // ── Construction helpers ──────────────────────────────────────────

        private LineRenderer MakeRing(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.startWidth = lr.endWidth = width;
            lr.material = LineMaterial();
            lr.numCapVertices = 4;
            return lr;
        }

        private void MakeAimPool()
        {
            _dots = new SpriteRenderer[MaxDots];
            for (int i = 0; i < MaxDots; i++)
            {
                var go = new GameObject("_aimDot");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SoftCircleSprite();
                go.transform.localScale = Vector3.one * 0.12f;
                go.SetActive(false);
                _dots[i] = sr;
            }

            var rgo = new GameObject("_aimReticle");
            rgo.transform.SetParent(transform, false);
            _reticle = rgo.AddComponent<SpriteRenderer>();
            _reticle.sprite = RingSprite();
            rgo.transform.localScale = Vector3.one * 0.5f;
            rgo.SetActive(false);
        }

        private void ApplySort(LineRenderer lr)
        {
            lr.sortingOrder = SortAt(RingCenter()) - 5; // just behind the player sprite
        }

        private int SortAt(Vector3 position)
        {
            if (_sortingConfig == null) return 5100;
            float depth = Vector3.Dot(position, _sortingConfig.SortAxisNormalized);
            return _sortingConfig.baseOrder - Mathf.RoundToInt(depth * _sortingConfig.stepsPerUnit) + 10;
        }

        private static Material LineMaterial()
        {
            if (_lineMat == null)
                _lineMat = new Material(Shader.Find("Sprites/Default"));
            return _lineMat;
        }

        // ── Procedural sprites ────────────────────────────────────────────

        private static Sprite _softCircle;
        private static Sprite _ringSprite;

        private static Sprite SoftCircleSprite()
        {
            if (_softCircle != null) return _softCircle;
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (s * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            _softCircle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32);
            return _softCircle;
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float rOuter = s * 0.46f, rInner = s * 0.34f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = Mathf.Clamp01((rOuter - d) / 2f) * Mathf.Clamp01((d - rInner) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32);
            return _ringSprite;
        }
    }
}
