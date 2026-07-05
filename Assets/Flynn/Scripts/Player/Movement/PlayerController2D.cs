using System;
using System.Collections.Generic;
using UnityEngine;
using Flynn.Npc;

namespace Flynn.Player
{
    public enum PlayerAnimationState
    {
        Idle = 0,
        Run = 1,
        Swim = 2,
        Grapple = 3,
        Carry = 4,
        Throw = 5,
        Jump = 6
    }

    public enum FacingDirection
    {
        Down = 0,
        Left = 1,
        Up = 2
    }

    /// <summary>
    /// 2D character controller for isometric top-down movement.
    /// Rigidbody2D stays gravityScale=0. Movement can be locked externally.
    /// Wrench input/combat lives in WrenchController (Player/Combat) — this class
    /// only exposes the animator/movement hooks it needs.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _acceleration = 12f;
        [SerializeField] private float _deceleration = 10f;

        [Header("Facing")]
        [SerializeField] private bool _flipToFaceDirection = true;
        [SerializeField] private float _directionSmoothing = 10f;

        [Header("Isometric")]
        [Tooltip("Y/X ratio of the iso grid cell size (0.5 = 2:1 classic iso). Diagonal movement is scaled to align with tile edges.")]
        [SerializeField] private float _isoRatio = 0.5f;

        [Header("Animation")]
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerAnimationState _animationState = PlayerAnimationState.Idle;
        [SerializeField] private AnimationSpeedConfigSO _animSpeedConfig;

        // ── Components ──
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private PlayerHeightState _height;

        // ── State ──
        private Vector2 _moveInput;
        private Vector2 _lastMoveDirection = Vector2.right;
        private Vector2 _smoothedDirection = Vector2.down;

        // ── Speed modifiers (e.g. flora slow-down) ──
        private readonly List<float> _speedModifiers = new();
        private float SpeedMultiplier
        {
            get
            {
                float mul = 1f;
                foreach (float m in _speedModifiers) mul = Mathf.Min(mul, m);
                return mul;
            }
        }

        // ── Animation State ──
        private FacingDirection _facingDir = FacingDirection.Down;
        private bool _triggerActive;
        private float _triggerSpeed = 1f;
        private bool _knockbackActive;
        private float _triggerTargetDuration;

        // ── Public State ──
        public Vector2 MoveInput => _moveInput;
        public Vector2 LastMoveDirection => _lastMoveDirection;
        public Vector2 GroundPosition => transform.position;
        public Vector2 Velocity => _rb.velocity;
        public float NormalizedSpeed
        {
            get
            {
                float effectiveMax = _moveSpeed * SpeedMultiplier;
                return effectiveMax > 0f ? Mathf.Clamp01(_rb.velocity.magnitude / effectiveMax) : 0f;
            }
        }
        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;
        public bool IsMovementLocked { get; set; }

        // ── Combat hooks (driven by WrenchController) ──

        /// <summary>While set, the 16-dir animator faces this world direction instead
        /// of the movement input (charge/aim face-lock). Movement itself is unaffected.</summary>
        public Vector2? FacingOverride { get; set; }

        /// <summary>Keeps the current trigger animation latched even while movement is
        /// unlocked (throw aim holds the pose mid-clip). Clear to resume locomotion.</summary>
        public bool HoldTrigger { get; set; }

        /// <summary>While set, wins over every internal animator.speed computation
        /// (used to freeze the throw anim at its halfway pose).</summary>
        public float? AnimatorSpeedOverride { get; set; }

        /// <summary>Fired the frame a trigger animation (Throw/Jump) actually starts.
        /// Args: state, playback speed, resolved clip (may be null).</summary>
        public event Action<PlayerAnimationState, float, AnimationClip> TriggerFired;

        /// <summary>Impulse knockback (swing recoil). Decays inside FixedUpdate while
        /// movement is locked.</summary>
        public void ApplyKnockback(Vector2 impulse)
        {
            _knockbackActive = true;
            _rb.AddForce(impulse, ForceMode2D.Impulse);
        }

        /// <summary>Register a speed modifier (e.g. 0.5 for 50% in flora). Returns a handle to remove later.</summary>
        public int AddSpeedModifier(float multiplier)
        {
            _speedModifiers.Add(multiplier);
            return _speedModifiers.Count - 1;
        }

        public void RemoveSpeedModifier(int handle)
        {
            if (handle >= 0 && handle < _speedModifiers.Count)
                _speedModifiers[handle] = 1f;
        }

        /// <summary>Delegates to <see cref="PlayerHeightState"/> when present (the elevation
        /// source of truth); falls back to a plain field if no height state is attached.</summary>
        public int HeightIndex
        {
            get => _height != null ? _height.HeightLevel : _heightIndexFallback;
            set { if (_height != null) _height.SetLevel(value, snap: false); else _heightIndexFallback = value; }
        }
        private int _heightIndexFallback;

        public PlayerAnimationState AnimationState
        {
            get => _animationState;
            set => _animationState = value;
        }
        public FacingDirection FacingDir => _facingDir;

        /// <summary>Snap the player's sprite and animator to face a world direction.</summary>
        public void FaceDirection(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            _lastMoveDirection = dir;
        }

        /// <summary>Snap player to face a world-space point (NPC, target, etc.).
        /// Sets both _lastMoveDirection and _smoothedDirection so the 16-dir
        /// blend tree snaps immediately with no lerp delay.</summary>
        public void FacePoint(Vector3 worldPoint)
        {
            Vector2 dir = worldPoint - transform.position;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            _lastMoveDirection = dir;
            _smoothedDirection = dir;
            UpdateFacingDirection();
        }

        /// <summary>Queue a trigger state (Jump/Throw) scaled to finish in exactly <paramref name="targetDuration"/> seconds.</summary>
        public void PlayTrigger(PlayerAnimationState state, float targetDuration)
        {
            _triggerTargetDuration = targetDuration;
            _animationState = state;
        }

        /// <summary>Length in seconds of the first controller clip whose name starts with
        /// <paramref name="animName"/> (e.g. "throw"). 0 when not found.</summary>
        public float GetClipLength(string animName)
        {
            AnimationClip clip = GetClip(animName);
            return clip != null ? clip.length : 0f;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _height = GetComponent<PlayerHeightState>();
            _rb.gravityScale = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Update()
        {
            // Self-guard: if dialogue is open, zero input and skip all interaction.
            // IsMovementLocked is also set by DialogueManager.LockPlayerMovement, but
            // this check doesn't depend on the reference being resolved.
            if (DialogueManager.IsDialogueOpen)
                IsMovementLocked = true;

            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            // Facing: combat override wins; otherwise follow input (unless locked)
            if (FacingOverride.HasValue)
            {
                Vector2 f = FacingOverride.Value;
                if (f.sqrMagnitude > 0.01f)
                    _lastMoveDirection = f.normalized;
            }
            else if (!IsMovementLocked)
            {
                // Direction for animations — use raw input so diagonals are at 45°
                if (input.sqrMagnitude > 0.01f)
                    _lastMoveDirection = input.normalized;
            }

            if (IsMovementLocked)
                input = Vector2.zero;

            // Scale Y on diagonals so movement aligns with iso tile edges (2:1 ratio)
            if (input.x != 0f && input.y != 0f)
                input.y *= _isoRatio;

            if (input.sqrMagnitude > 1f) input = input.normalized;
            _moveInput = input;

            if (!IsMovementLocked || FacingOverride.HasValue)
                UpdateFacingDirection();

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (IsMovementLocked)
            {
                if (_knockbackActive)
                {
                    if (_rb.velocity.sqrMagnitude < 0.1f)
                    {
                        _knockbackActive = false;
                        _rb.velocity = Vector2.zero;
                    }
                    return;
                }
                _rb.velocity = Vector2.zero;
                return;
            }

            float delta = Time.fixedDeltaTime;

            float speedMul = SpeedMultiplier;
            float decelRetention = 1f;
            float accel = _acceleration;
            float decel = _deceleration * decelRetention;

            Vector2 velocity = _rb.velocity;
            Vector2 target = _moveInput * _moveSpeed * speedMul;

            if (_moveInput.sqrMagnitude > 0.01f)
            {
                velocity = Vector2.MoveTowards(velocity, target, accel * delta);
            }
            else
            {
                velocity = Vector2.MoveTowards(velocity, Vector2.zero, decel * delta);
            }

            _rb.velocity = velocity;

        }

        private void UpdateFacingDirection()
        {
            Vector2 dir = _lastMoveDirection;
            if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x))
                _facingDir = dir.y > 0f ? FacingDirection.Up : FacingDirection.Down;
            else
                _facingDir = FacingDirection.Left;
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            // 16-direction blend params — adaptively lerp toward target direction.
            // Large direction changes snap fast; small adjustments blend smoothly.
            Vector2 targetDir = _lastMoveDirection.sqrMagnitude > 0.01f ? _lastMoveDirection.normalized : _smoothedDirection;
            float angleDelta = Vector2.Angle(_smoothedDirection, targetDir);
            float smoothSpeed = angleDelta > 90f ? _directionSmoothing * 4f
                             : angleDelta > 45f ? _directionSmoothing * 2f
                             : _directionSmoothing;
            _smoothedDirection = Vector2.Lerp(_smoothedDirection, targetDir, Time.deltaTime * smoothSpeed);
            if (_smoothedDirection.sqrMagnitude > 0.001f)
                _smoothedDirection = _smoothedDirection.normalized;
            _animator.SetFloat("MoveX", _smoothedDirection.x);
            _animator.SetFloat("MoveY", _smoothedDirection.y);

            // Hold constant speed while a trigger animation (Jump/Throw) is playing.
            // HoldTrigger keeps the pose latched during throw aim (movement unlocked).
            if (_triggerActive)
            {
                _animator.speed = AnimatorSpeedOverride ?? _triggerSpeed;
                if (!IsMovementLocked && !HoldTrigger)
                    _triggerActive = false;
                return;
            }

            // Trigger states: fire once, then fall back to auto-resolved locomotion
            if (_animationState is PlayerAnimationState.Throw or PlayerAnimationState.Jump)
            {
                string trigger = _animationState == PlayerAnimationState.Throw ? "Throw" : "Jump";
                string animName = _animationState == PlayerAnimationState.Throw ? "throw" : "jump";

                if (_triggerTargetDuration > 0f)
                {
                    float clipLength = GetClipLength(animName);
                    _triggerSpeed = clipLength > 0f
                        ? clipLength / _triggerTargetDuration
                        : (_animSpeedConfig != null ? _animSpeedConfig.GetSpeed(_animationState) : 1f);
                    _triggerTargetDuration = 0f;
                }
                else
                {
                    _triggerSpeed = _animSpeedConfig != null
                        ? _animSpeedConfig.GetSpeed(_animationState)
                        : 1f;
                }

                _animator.SetTrigger(trigger);
                _animator.speed = AnimatorSpeedOverride ?? _triggerSpeed;
                _triggerActive = true;

                TriggerFired?.Invoke(_animationState, _triggerSpeed, GetClip(animName));

                _animationState = PlayerAnimationState.Idle;
                return;
            }

            // Bool states for Swim / Grapple / Carry
            _animator.SetBool("Swimming",  _animationState == PlayerAnimationState.Swim);
            _animator.SetBool("Grappling", _animationState == PlayerAnimationState.Grapple);
            _animator.SetBool("Carrying",  _animationState == PlayerAnimationState.Carry);

            // Locomotion: Speed blends idle ↔ run in the 1D blend tree
            float speed = (_animationState is PlayerAnimationState.Idle or PlayerAnimationState.Run)
                ? Mathf.Clamp01(NormalizedSpeed)
                : 0f;

            float animSpeed = 1f;
            if (_animSpeedConfig != null)
            {
                animSpeed = speed > 0.1f
                    ? _animSpeedConfig.runSpeed
                    : _animSpeedConfig.GetSpeed(PlayerAnimationState.Idle);
            }
            // Scale animation playback with actual movement speed so running
            // through flora (50% speed) plays run anim at 50% speed.
            _animator.speed = AnimatorSpeedOverride ?? (animSpeed * SpeedMultiplier);

            _animator.SetFloat("Speed", speed);
        }

        private AnimationClip GetClip(string animName)
        {
            if (_animator?.runtimeAnimatorController == null) return null;
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.StartsWith(animName)) return clip;
            }
            return null;
        }
    }
}
