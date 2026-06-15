using UnityEngine;
using Flynn.Events;

namespace Flynn.Platforming
{
    /// <summary>
    /// Side-on 2D platformer controller for the layered diorama view. Gameplay is
    /// pure XY with real gravity: horizontal input + jump. The ~30° camera tilt is
    /// purely cosmetic, so everything here stays 2D.
    ///
    /// SINGLE RESPONSIBILITY: turn input + physics into movement, and expose the
    /// three numbers other systems read —
    ///   • <see cref="IsGrounded"/>
    ///   • <see cref="HeightAboveSurface"/>  (drives the drop-shadow height cue;
    ///       also equals how far the player will fall off a ledge — one number,
    ///       shadow gap == fall depth)
    ///   • <see cref="CurrentPlatform"/>     (drives platform occlusion fade)
    ///
    /// Reuses the 2D terrain model (TerrainStateAggregator2D): ice = low grip,
    /// wind = horizontal push, mud = no jump. No animation/visual logic lives here.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlatformerController2D : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _acceleration = 60f;
        [SerializeField] private float _deceleration = 50f;

        [Header("Jump")]
        [Tooltip("Desired peak jump height in world units. Jump velocity is derived from this + gravity.")]
        [SerializeField] private float _jumpHeight = 3f;
        [SerializeField] private float _gravityScale = 3f;
        [Tooltip("Extra gravity multiplier while falling, for a snappier arc.")]
        [SerializeField] private float _fallGravityMult = 1.6f;
        [Tooltip("Grace window to still jump just after walking off a ledge.")]
        [SerializeField] private float _coyoteTime = 0.1f;
        [Tooltip("Grace window so a jump pressed just before landing still fires.")]
        [SerializeField] private float _jumpBuffer = 0.1f;

        [Header("Ground / Surface Probe")]
        [SerializeField] private LayerMask _groundLayers = ~0;
        [Tooltip("Feet-to-surface gap under which the player counts as grounded.")]
        [SerializeField] private float _groundCheckDistance = 0.12f;
        [Tooltip("How far down to look for the surface below (for the drop-shadow while airborne).")]
        [SerializeField] private float _surfaceProbeDistance = 50f;
        [Tooltip("Offset from the transform to the player's feet (ray origin).")]
        [SerializeField] private Vector2 _feetOffset = new Vector2(0f, -0.5f);

        // ── Public state other systems read ──────────────────────────────────
        public bool IsGrounded { get; private set; }

        /// <summary>Vertical gap from the player's feet to the surface directly below:
        /// 0 when grounded, grows while jumping/falling. Drives the drop-shadow and
        /// equals the fall distance off a ledge.</summary>
        public float HeightAboveSurface { get; private set; }

        /// <summary>Collider of the surface currently below the feet (platform or ground),
        /// or null over a void. Lets the occlusion manager know what we stand on.</summary>
        public Collider2D CurrentPlatform { get; private set; }

        /// <summary>-1 facing left, +1 facing right.</summary>
        public float FacingSign { get; private set; } = 1f;

        /// <summary>When true, an external system (e.g. the grapple) owns horizontal
        /// velocity; the controller leaves velocity.x alone. Gravity/jump still apply.</summary>
        public bool SuspendHorizontalControl { get; set; }

        // ── Components ────────────────────────────────────────────────────────
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private TerrainStateAggregator2D _terrain;
        private Shadow2DTarget _shadow;

        // ── Input / timers ────────────────────────────────────────────────────
        private float _moveInput;
        private float _coyoteTimer;
        private float _bufferTimer;
        private bool _wasGrounded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _terrain = GetComponent<TerrainStateAggregator2D>();
            _shadow = GetComponent<Shadow2DTarget>();

            _rb.freezeRotation = true;
            _rb.gravityScale = _gravityScale;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Update()
        {
            // Input only. Physics happens in FixedUpdate.
            _moveInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(_moveInput) > 0.01f) FacingSign = Mathf.Sign(_moveInput);

            // Buffer the press so it fires the instant we touch ground.
            if (Input.GetButtonDown("Jump")) _bufferTimer = _jumpBuffer;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ProbeSurface();

            // Landing detection (was airborne, now grounded).
            if (IsGrounded && !_wasGrounded)
                Publish(new PlayerLanded(transform.position));
            _wasGrounded = IsGrounded;

            TerrainState2D terrain = _terrain != null ? _terrain.CurrentState : TerrainState2D.Default;

            // ── Horizontal motion ─────────────────────────────────────────────
            float vx;
            if (SuspendHorizontalControl)
            {
                // Grapple (or other system) drives X this step; pass current through.
                vx = _rb.velocity.x;
            }
            else
            {
                float targetX = _moveInput * _moveSpeed * terrain.SpeedMultiplier;
                if (Mathf.Abs(_moveInput) > 0.01f)
                {
                    // On ice, steering is weak so momentum dominates.
                    float accel = terrain.LowGrip ? _acceleration * terrain.DecelerationRetention : _acceleration;
                    vx = Mathf.MoveTowards(_rb.velocity.x, targetX, accel * dt);
                }
                else if (terrain.LowGrip)
                {
                    vx = _rb.velocity.x; // glide — no braking on ice
                }
                else
                {
                    vx = Mathf.MoveTowards(_rb.velocity.x, 0f, _deceleration * dt);
                }
                vx += terrain.ExternalForce.x * dt; // wind etc.
            }

            // ── Jump timers ───────────────────────────────────────────────────
            _coyoteTimer = IsGrounded ? _coyoteTime : _coyoteTimer - dt;
            _bufferTimer -= dt;

            float vy = _rb.velocity.y;

            // ── Jump ──────────────────────────────────────────────────────────
            bool canJump = _coyoteTimer > 0f && !terrain.BlocksJump;
            if (_bufferTimer > 0f && canJump)
            {
                // v = sqrt(2 g h) reaches exactly _jumpHeight at apex.
                float g = Mathf.Abs(Physics2D.gravity.y) * _rb.gravityScale;
                vy = Mathf.Sqrt(2f * g * _jumpHeight);
                _bufferTimer = 0f;
                _coyoteTimer = 0f;
                Publish(new PlayerJumped(transform.position));
            }

            // Heavier gravity while falling for a tighter arc.
            if (vy < 0f)
                vy += Physics2D.gravity.y * _rb.gravityScale * (_fallGravityMult - 1f) * dt;

            _rb.velocity = new Vector2(vx, vy);

            // Face the move direction.
            if (Mathf.Abs(_moveInput) > 0.01f) _sr.flipX = _moveInput < 0f;

            // Feed the drop-shadow: shadow drops by exactly our height above ground.
            if (_shadow != null) _shadow.DynamicLift = HeightAboveSurface;
        }

        /// <summary>One downward ray serves both grounding and the drop-shadow.
        /// Sets <see cref="IsGrounded"/>, <see cref="HeightAboveSurface"/>, <see cref="CurrentPlatform"/>.</summary>
        private void ProbeSurface()
        {
            Vector2 feet = (Vector2)transform.position + _feetOffset;
            RaycastHit2D hit = Physics2D.Raycast(feet, Vector2.down, _surfaceProbeDistance, _groundLayers);

            if (hit.collider != null)
            {
                CurrentPlatform = hit.collider;
                HeightAboveSurface = Mathf.Max(0f, hit.distance);
                IsGrounded = hit.distance <= _groundCheckDistance && _rb.velocity.y <= 0.01f;
            }
            else
            {
                CurrentPlatform = null;
                HeightAboveSurface = 0f;
                IsGrounded = false;
            }
        }

        private static void Publish<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 feet = (Vector2)transform.position + _feetOffset;
            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawLine(feet, feet + Vector2.down * 0.5f);
        }
#endif
    }
}
