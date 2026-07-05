using UnityEngine;
using Flynn.Core;
using Flynn.Events;
using Flynn.Resources;

namespace Flynn.Player.Combat
{
    /// <summary>
    /// The wrench in flight. Runtime-built (no prefab): straight flight toward the
    /// aim direction, first hittable takes damage, then the wrench rebounds, tumbles
    /// to a stop, rests, and homes back to the player. Publishes ResourceHit /
    /// ToolHitTarget on impact and ToolReturned on catch.
    /// </summary>
    public class ThrownWrench : MonoBehaviour
    {
        private enum Phase { Flight, Tumble, Rest, Return }

        private Transform _player;
        private PowerBuildupSettings _settings;
        private Vector2 _velocity;
        private float _traveled;
        private float _maxRange;
        private int _damage;
        private bool _isPerfect;
        private float _charge;

        private Phase _phase = Phase.Flight;
        private float _restTimer;
        private float _spinSpeed;
        private Transform _visual;
        private readonly System.Collections.Generic.HashSet<ResourceNode> _pierced = new();

        private const float HitRadius = 0.16f;
        private const float TumbleDrag = 4f;
        private const float FallDrift = 0.8f;
        private const float CatchDistance = 0.7f;

        /// <summary>Spawn and launch a wrench that flies TO <paramref name="target"/>
        /// (the mouse point), clamped to the charge's max range.</summary>
        public static ThrownWrench Launch(
            Transform player, Vector3 target, float charge, int damage, bool isPerfect,
            PowerBuildupSettings settings, Sprite sprite)
        {
            // Spawn dead-center on the character sprite (pivot is at the feet),
            // then aim from the spawn point so it lands exactly on the cursor.
            Vector3 spawn = player.position + Vector3.up * 0.4f;

            Vector2 dir = target - spawn;
            float targetDist = dir.magnitude;
            if (targetDist < 0.01f) { dir = Vector2.right; targetDist = 0.2f; }
            else dir /= targetDist;

            var go = new GameObject("_thrownWrench");
            go.transform.position = spawn;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : FallbackSprite();
            sr.sortingOrder = 5100;

            var wrench = go.AddComponent<ThrownWrench>();
            wrench._player = player;
            wrench._settings = settings;
            wrench._velocity = dir * settings.throwSpeed;
            // Land at the cursor — charge only caps how far that can be.
            wrench._maxRange = Mathf.Min(
                targetDist,
                Mathf.Lerp(settings.throwRangeLight, settings.throwRangeHeavy, charge));
            wrench._damage = damage;
            wrench._isPerfect = isPerfect;
            wrench._charge = charge;
            wrench._spinSpeed = 720f;
            wrench._visual = visual.transform;
            return wrench;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (_phase)
            {
                case Phase.Flight:  TickFlight(dt); break;
                case Phase.Tumble:  TickTumble(dt); break;
                case Phase.Rest:    TickRest(dt); break;
                case Phase.Return:  TickReturn(dt); break;
            }

            if (_visual != null)
                _visual.Rotate(0f, 0f, -_spinSpeed * dt);
        }

        private void TickFlight(float dt)
        {
            Vector2 step = _velocity * dt;
            Vector2 from = transform.position;

            // Sweep so speed can't tunnel between frames. The wrench PIERCES every
            // resource on its path (grass, overgrowth, ...) and only rebounds off
            // solid non-resource geometry.
            RaycastHit2D[] hits = Physics2D.CircleCastAll(from, HitRadius, step.normalized, step.magnitude);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(_player)) continue;

                var node = hit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    if (_pierced.Add(node))                       // damage each node once
                        OnPierceNode(node, hit.point, _velocity.normalized);
                    continue;                                     // keep flying
                }

                // Solid world geometry stops the wrench (triggers pass through).
                if (!hit.collider.isTrigger)
                {
                    transform.position = hit.point;
                    OnHitSurface(hit.collider.gameObject, _velocity.normalized);
                    return;
                }
            }

            transform.position = from + step;
            _traveled += step.magnitude;

            if (_traveled >= _maxRange)
            {
                // Spent flight — flop to the ground where it is.
                _velocity *= 0.25f;
                _phase = Phase.Tumble;
            }
        }

        private void OnPierceNode(ResourceNode node, Vector2 hitPoint, Vector2 dir)
        {
            Publish(new ResourceHit(node, dir, _damage));
            Publish(new ToolHitTarget(new ToolHitContext(
                gameObject, ToolType.Wrench, ToolActionType.Throw,
                _damage, hitPoint, dir, _charge)));
        }

        private void OnHitSurface(GameObject surface, Vector2 dir)
        {
            if (Flynn.Effects.HitImpactFX.Instance != null)
                Flynn.Effects.HitImpactFX.Instance.PlaySurfaceHit(transform.position, dir, surface);
            Rebound(dir);
        }

        private void Rebound(Vector2 dir)
        {
            // Bounce back along the flight path with damping + a little scatter.
            Vector2 back = -dir;
            Vector2 scatter = new Vector2(-dir.y, dir.x) * Random.Range(-0.35f, 0.35f);
            _velocity = (back + scatter).normalized * _settings.throwSpeed * _settings.reboundDamping;
            _phase = Phase.Tumble;
        }

        private void TickTumble(float dt)
        {
            // Slide out with drag + a slight downward drift so it reads as falling.
            _velocity = Vector2.MoveTowards(_velocity, Vector2.zero, TumbleDrag * dt);
            _velocity += Vector2.down * (FallDrift * dt);
            _spinSpeed = Mathf.MoveTowards(_spinSpeed, 90f, 900f * dt);

            transform.position += (Vector3)(_velocity * dt);

            if (_velocity.magnitude < 0.5f)
            {
                _phase = Phase.Rest;
                _restTimer = 0f;
                _spinSpeed = 0f;
            }
        }

        private void TickRest(float dt)
        {
            _restTimer += dt;
            if (_restTimer >= _settings.restDuration)
                _phase = Phase.Return;
        }

        private void TickReturn(float dt)
        {
            if (_player == null) { Finish(); return; }

            Vector2 toPlayer = _player.position - transform.position;
            if (toPlayer.magnitude <= CatchDistance) { Finish(); return; }

            _velocity += toPlayer.normalized * (_settings.returnAccel * dt);
            float maxSpeed = _settings.throwSpeed * 1.5f;
            if (_velocity.magnitude > maxSpeed) _velocity = _velocity.normalized * maxSpeed;

            _spinSpeed = Mathf.MoveTowards(_spinSpeed, 540f, 1800f * dt);
            transform.position += (Vector3)(_velocity * dt);
        }

        private void Finish()
        {
            Publish(new ToolReturned());
            Destroy(gameObject);
        }

        private static void Publish<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }

        // ── Fallback visual (never invisible, even with no sprite wired) ──

        private static Sprite _fallback;

        private static Sprite FallbackSprite()
        {
            if (_fallback != null) return _fallback;

            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool shaft = Mathf.Abs(y - s / 2) <= 2 && x >= 4 && x <= s - 5;
                    bool head = (x - (s - 6)) * (x - (s - 6)) + (y - s / 2) * (y - s / 2) <= 25;
                    tex.SetPixel(x, y, shaft || head ? new Color(0.85f, 0.85f, 0.9f, 1f) : Color.clear);
                }
            tex.Apply();
            _fallback = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32);
            return _fallback;
        }
    }
}
