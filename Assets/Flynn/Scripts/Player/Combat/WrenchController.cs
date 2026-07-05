using System.Collections;
using UnityEngine;
using Flynn.Core;
using Flynn.Events;
using Flynn.Npc;
using Flynn.Resources;
using Flynn.UI.Core;

namespace Flynn.Player.Combat
{
    /// <summary>
    /// Owns both wrench verbs.
    /// LMB: the throw anim starts on press and freezes at the hit pose; a quick
    /// release is a base swing, holding past the charge delay builds power with a
    /// sweetspot release window. RMB cancels a swing buildup.
    /// RMB: hold = aim (slowed movement, anim frozen at halfway, buildup + sweetspot),
    /// release = throw a ThrownWrench projectile. LMB cancels the aim.
    /// During any buildup the 16-dir pose face-locks to the mouse.
    /// </summary>
    public class WrenchController : MonoBehaviour
    {
        private enum State { Idle, SwingPress, SwingCharge, SwingAnim, ThrowAim, WrenchAway }

        [Header("Refs")]
        [SerializeField] private PowerBuildupSettings _settings;
        [SerializeField] private GameObject _wrenchPivot;

        [Header("Swing")]
        [SerializeField] private float _swingRange = 1.2f;
        [Tooltip("Radius around the (range-clamped) aim point searched for a target.")]
        [SerializeField] private float _targetRadius = 0.45f;
        [Tooltip("Fraction of the throw clip where the swing freezes while charging (the hit pose).")]
        [SerializeField] private float _swingFreezePoint = 0.3f;

        [Header("Swing Feel")]
        [Tooltip("Seconds from press to the hit landing. Decoupled from clip frames so taps feel instant.")]
        [SerializeField] private float _tapHitDelay = 0.09f;
        [Tooltip("Seconds from swing release until the next swing can start (chop rhythm).")]
        [SerializeField] private float _swingCooldown = 0.35f;

        [Header("Procedural Swing")]
        [Tooltip("Half-angle of the wrench sweep either side of the aim direction.")]
        [SerializeField] private float _sweepHalfAngle = 80f;
        [Tooltip("Seconds the release sweep takes.")]
        [SerializeField] private float _sweepTime = 0.12f;
        [Tooltip("Wrench orbit radius from the body centre during the swing.")]
        [SerializeField] private float _swingRadius = 0.4f;
        [Tooltip("Pivot local offset (relative to its parent) while swinging.")]
        [SerializeField] private Vector3 _swingPivotLocal = new Vector3(0f, 0.12f, 0f);
        [Tooltip("Extra local rotation for the wrench sprite while swinging.")]
        [SerializeField] private float _swingSpriteAngle = 0f;

        [Header("Tool Sprites (per target)")]
        [Tooltip("Default / no target.")]
        [SerializeField] private Sprite _spriteWrench;
        [Tooltip("Wood targets (trees).")]
        [SerializeField] private Sprite _spriteAxe;
        [Tooltip("Stone targets.")]
        [SerializeField] private Sprite _spritePick;
        [Tooltip("Tech/metal targets (ancient hammer).")]
        [SerializeField] private Sprite _spriteHammer;

        [Header("Throw Anim")]
        [Tooltip("Fraction of the throw clip where the aim pose freezes.")]
        [SerializeField] private float _aimFreezePoint = 0.5f;
        [Tooltip("Animator speed while frozen (0 looks dead; a crawl reads alive).")]
        [SerializeField] private float _aimFreezeSpeed = 0.05f;

        private PlayerController2D _player;
        private State _state = State.Idle;
        private float _holdTimer;
        private int _slowHandle = -1;
        private bool _buildupAnnounced;

        // Anim timing captured when the trigger actually fires
        private float _animStartTime;
        private float _animDuration;
        private float _hitDelay;      // seconds from anim start to the hit frame
        private float _freezeAt;      // seconds from anim start to the freeze pose
        private bool _awaitingTrigger;
        private float _pendingFreezePoint;

        private Vector3 _aimPoint;
        private ResourceNode _aimNode;

        private Coroutine _swingRoutine;
        private Coroutine _freezeRoutine;

        // Procedural swing pose. Driven from LateUpdate — the player's Animator
        // writes the pivot transform during the animation pass, so any pose set
        // in Update/coroutines gets overwritten and the wrench never moves.
        private enum SwingPose { None, Windup, Sweep }
        private SwingPose _pose;
        private float _sweepT;
        private float _sweepAim;

        // Wrench pivot home pose (hand position for the throw clip) — the
        // procedural swing re-poses the pivot and must restore it afterwards.
        private Transform _wrenchVisualT;
        private SpriteRenderer _wrenchSR;
        private Sprite _wrenchHomeSprite;
        private Vector3 _pivotHomePos;
        private Quaternion _pivotHomeRot;
        private Vector3 _wrenchHomePos;
        private Quaternion _wrenchHomeRot;

        public bool WrenchIsHome => _state != State.WrenchAway;

        /// <summary>True while a wrench verb is mid-action (press/charge/aim/swing anim).
        /// WrenchAway doesn't count — the player moves freely while the wrench flies.</summary>
        public bool IsActing => _state is State.SwingPress or State.SwingCharge or State.SwingAnim or State.ThrowAim;

        private PlayerJumpController _jump;

        private void Awake()
        {
            _player = GetComponent<PlayerController2D>();
            _jump = GetComponent<PlayerJumpController>();
            if (_wrenchPivot != null)
            {
                _wrenchPivot.SetActive(false);
                var pt = _wrenchPivot.transform;
                _pivotHomePos = pt.localPosition;
                _pivotHomeRot = pt.localRotation;
                if (pt.childCount > 0)
                {
                    _wrenchVisualT = pt.GetChild(0);
                    _wrenchHomePos = _wrenchVisualT.localPosition;
                    _wrenchHomeRot = _wrenchVisualT.localRotation;
                    _wrenchSR = _wrenchVisualT.GetComponent<SpriteRenderer>();
                    if (_wrenchSR != null) _wrenchHomeSprite = _wrenchSR.sprite;
                }
            }
        }

        private void OnEnable()
        {
            _player.TriggerFired += OnTriggerFired;
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Subscribe<ToolReturned>(OnToolReturned);
        }

        private void OnDisable()
        {
            _player.TriggerFired -= OnTriggerFired;
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Unsubscribe<ToolReturned>(OnToolReturned);
        }

        private void Update()
        {
            if (_settings == null || DialogueManager.IsDialogueOpen) return;

            switch (_state)
            {
                case State.Idle:        TickIdle(); break;
                case State.SwingPress:  TickSwingPress(); break;
                case State.SwingCharge: TickSwingCharge(); break;
                case State.ThrowAim:    TickThrowAim(); break;
                // SwingAnim / WrenchAway resolve via coroutines & events
            }
        }

        // ── Idle ──────────────────────────────────────────────────────────

        private void TickIdle()
        {
            if (_player.IsMovementLocked) return;
            if (_jump != null && _jump.IsAirborne) return; // no wrench starts midair

            if (Input.GetMouseButtonDown(0)) BeginSwingPress();
            else if (Input.GetMouseButtonDown(1)) BeginThrowAim();
        }

        // ── Swing: anim starts on press, freezes at the hit pose ─────────

        private void BeginSwingPress()
        {
            _state = State.SwingPress;
            _holdTimer = 0f;
            FaceLockToMouse();

            // Procedural swing: no body clip. The wrench poses at the windup
            // angle (applied in LateUpdate, after the Animator); hit timing runs
            // off the press timestamp.
            _animStartTime = Time.time;
            if (_wrenchPivot != null) _wrenchPivot.SetActive(true);
            _pose = SwingPose.Windup;
        }

        private void TickSwingPress()
        {
            _holdTimer += Time.deltaTime;
            FaceLockToMouse();

            if (Input.GetMouseButtonUp(0)) { ReleaseSwing(0f); return; }

            if (_holdTimer >= _settings.holdToChargeDelay)
            {
                _state = State.SwingCharge;
                _buildupAnnounced = true;
                _slowHandle = _player.AddSpeedModifier(_settings.swingChargeSlow);
                Publish(new PowerBuildupStarted(ActionType.Swing));
            }
        }

        private void TickSwingCharge()
        {
            _holdTimer += Time.deltaTime;
            FaceLockToMouse();

            float n = ChargeMath.Normalized(_holdTimer, _settings.holdToChargeDelay, _settings.maxChargeTime);
            bool inZone = ChargeMath.InPerfectZone(n, _settings.perfectZoneStart, _settings.perfectZoneEnd);
            Publish(new PowerBuildupChanged(n, inZone));

            if (Input.GetMouseButtonDown(1)) { CancelBuildup(ActionType.Swing); return; }
            if (Input.GetMouseButtonUp(0)) ReleaseSwing(n);
        }

        private void ReleaseSwing(float normalized)
        {
            bool isPerfect = ChargeMath.InPerfectZone(normalized, _settings.perfectZoneStart, _settings.perfectZoneEnd);
            int damage = ChargeMath.Damage(normalized, _settings.lightSwingDamage, _settings.heavySwingDamage, isPerfect, _settings.perfectMultiplier);
            float cost = ChargeMath.Cost(normalized, _settings.lightSwingBatteryCost, _settings.heavySwingBatteryCost);

            EndBuildupCommon();
            if (_buildupAnnounced)
            {
                Publish(new PowerBuildupReleased(normalized, isPerfect, damage, cost, ActionType.Swing));
                _buildupAnnounced = false;
            }

            if (RobotBattery.Instance != null) RobotBattery.Instance.Spend(cost);

            CacheAim();

            _state = State.SwingAnim;
            _player.IsMovementLocked = true; // brief: released again right after the hit lands

            _pose = SwingPose.Sweep;
            _sweepT = 0f;
            _sweepAim = AimAngleDeg();

            if (_swingRoutine != null) StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(SwingResolveRoutine(damage, isPerfect, normalized, Time.time));
        }

        private IEnumerator SwingResolveRoutine(int damage, bool isPerfect, float charge, float releaseTime)
        {
            // Hit lands at a fixed real-time delay from press, not at a clip frame —
            // taps must feel instant. A charge held past the delay hits on release.
            float elapsed = Time.time - _animStartTime;
            float untilHit = Mathf.Max(0f, _tapHitDelay - elapsed);

            if (untilHit > 0.001f) yield return new WaitForSeconds(untilHit);

            Publish(new ToolSwingStarted(ToolType.Wrench, _aimPoint));

            Vector2 playerPos = transform.position;
            Vector2 hitDir = _aimPoint - (Vector3)playerPos;
            if (hitDir.sqrMagnitude < 0.01f) hitDir = _player.LastMoveDirection;
            hitDir.Normalize();

            if (_aimNode != null)
            {
                Publish(new ResourceHit(_aimNode, hitDir, damage));
                Publish(new ToolHitTarget(new ToolHitContext(
                    gameObject, ToolType.Wrench, ToolActionType.Swing,
                    damage, _aimNode.transform.position, hitDir, charge)));

                float knockback = _aimNode.HitKnockback;
                float shake = _aimNode.CameraShakeAmount * (isPerfect ? 1.6f : 1f);

                if (Flynn.Effects.CameraShake.Instance != null && shake > 0f)
                    Flynn.Effects.CameraShake.Instance.Shake(shake, 0.25f);

                if (knockback > 0f)
                    _player.ApplyKnockback(-hitDir * knockback);
            }

            // Hit landed — free the player immediately; the clip tail is visual only.
            _player.IsMovementLocked = false;

            // Cooldown sets the chop rhythm, measured from release.
            float untilReady = Mathf.Max(0.05f, _swingCooldown - (Time.time - releaseTime));
            yield return new WaitForSeconds(untilReady);

            if (_pose == SwingPose.None && _wrenchPivot != null) _wrenchPivot.SetActive(false);
            if (_state == State.SwingAnim) _state = State.Idle;
            _swingRoutine = null;
        }

        // ── Procedural sweep (LateUpdate: must run after the Animator) ────

        private void LateUpdate()
        {
            switch (_pose)
            {
                case SwingPose.Windup:
                    ApplySwingPose();
                    UpdateToolSprite();
                    SetSwingAngle(AimAngleDeg() + _sweepHalfAngle);
                    break;

                case SwingPose.Sweep:
                    _sweepT += Time.deltaTime;
                    float p = Mathf.Clamp01(_sweepT / _sweepTime);
                    float ease = 1f - (1f - p) * (1f - p);
                    ApplySwingPose();
                    SetSwingAngle(_sweepAim + Mathf.Lerp(_sweepHalfAngle, -_sweepHalfAngle, ease));
                    if (p >= 1f) RestoreWrenchHome();
                    break;
            }
        }

        /// <summary>Windup shows the tool matched to what the click would hit.</summary>
        private void UpdateToolSprite()
        {
            if (_wrenchSR == null) return;
            var node = ResolveAim(out _);
            Sprite pick = _spriteWrench != null ? _spriteWrench : _wrenchHomeSprite;
            if (node != null)
            {
                switch (node.ResourceKind)
                {
                    case Flynn.Resources.ResourceType.Wood:      if (_spriteAxe != null) pick = _spriteAxe; break;
                    case Flynn.Resources.ResourceType.Stone:     if (_spritePick != null) pick = _spritePick; break;
                    case Flynn.Resources.ResourceType.TechTrash: if (_spriteHammer != null) pick = _spriteHammer; break;
                }
            }
            if (_wrenchSR.sprite != pick) _wrenchSR.sprite = pick;
        }

        private void ApplySwingPose()
        {
            if (_wrenchPivot == null) return;
            _wrenchPivot.transform.localPosition = _swingPivotLocal;
            if (_wrenchVisualT != null)
            {
                _wrenchVisualT.localPosition = new Vector3(_swingRadius, 0f, 0f);
                _wrenchVisualT.localRotation = Quaternion.Euler(0f, 0f, _swingSpriteAngle);
            }
        }

        private void RestoreWrenchHome()
        {
            _pose = SwingPose.None;
            if (_wrenchPivot == null) return;
            var pt = _wrenchPivot.transform;
            pt.localPosition = _pivotHomePos;
            pt.localRotation = _pivotHomeRot;
            if (_wrenchVisualT != null)
            {
                _wrenchVisualT.localPosition = _wrenchHomePos;
                _wrenchVisualT.localRotation = _wrenchHomeRot;
            }
            if (_wrenchSR != null && _wrenchHomeSprite != null) _wrenchSR.sprite = _wrenchHomeSprite;
            _wrenchPivot.SetActive(false);
        }

        private float AimAngleDeg()
        {
            Vector2 dir = MouseWorld() - transform.position;
            if (dir.sqrMagnitude < 0.0001f) dir = _player.LastMoveDirection;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        private void SetSwingAngle(float degrees)
        {
            if (_wrenchPivot == null) return;
            _wrenchPivot.transform.rotation = Quaternion.Euler(0f, 0f, degrees);
        }

        // ── Throw: aim → release / cancel ─────────────────────────────────

        private void BeginThrowAim()
        {
            _state = State.ThrowAim;
            _holdTimer = 0f;
            _buildupAnnounced = true;
            _slowHandle = _player.AddSpeedModifier(_settings.throwAimSlow);
            FaceLockToMouse();

            Publish(new ToolThrowStarted(MouseWorld()));
            Publish(new PowerBuildupStarted(ActionType.Throw));

            if (_wrenchPivot != null) _wrenchPivot.SetActive(true);
            _player.HoldTrigger = true;
            StartThrowClip(_aimFreezePoint);
        }

        private void TickThrowAim()
        {
            _holdTimer += Time.deltaTime;
            FaceLockToMouse();

            // Throw buildup starts immediately — no hold delay.
            float n = ChargeMath.Normalized(_holdTimer, 0f, _settings.maxChargeTime);
            bool inZone = ChargeMath.InPerfectZone(n, _settings.perfectZoneStart, _settings.perfectZoneEnd);
            Publish(new PowerBuildupChanged(n, inZone));

            if (Input.GetMouseButtonDown(0)) { CancelBuildup(ActionType.Throw); return; }
            if (Input.GetMouseButtonUp(1)) ReleaseThrow(n);
        }

        private void ReleaseThrow(float normalized)
        {
            bool isPerfect = ChargeMath.InPerfectZone(normalized, _settings.perfectZoneStart, _settings.perfectZoneEnd);
            int damage = ChargeMath.Damage(normalized, _settings.lightThrowDamage, _settings.heavyThrowDamage, isPerfect, _settings.perfectMultiplier);
            float cost = ChargeMath.Cost(normalized, _settings.lightThrowBatteryCost, _settings.heavyThrowBatteryCost);

            Vector3 target = MouseWorld();

            EndBuildupCommon();
            Publish(new PowerBuildupReleased(normalized, isPerfect, damage, cost, ActionType.Throw));
            Publish(new ToolThrowReleased(target, normalized));
            _buildupAnnounced = false;

            if (RobotBattery.Instance != null) RobotBattery.Instance.Spend(cost);

            if (_freezeRoutine != null) { StopCoroutine(_freezeRoutine); _freezeRoutine = null; }
            _player.AnimatorSpeedOverride = null;
            _player.HoldTrigger = false;

            _state = State.WrenchAway;
            StartCoroutine(SpawnWrenchAfter(0.1f, target, normalized, damage, isPerfect));
        }

        private IEnumerator SpawnWrenchAfter(float delay, Vector3 target, float charge, int damage, bool isPerfect)
        {
            yield return new WaitForSeconds(delay);

            Sprite wrenchSprite = null;
            if (_wrenchPivot != null)
            {
                var sr = _wrenchPivot.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) wrenchSprite = sr.sprite;
                _wrenchPivot.SetActive(false);
            }

            ThrownWrench.Launch(transform, target, charge, damage, isPerfect, _settings, wrenchSprite);
        }

        private void OnToolReturned(ToolReturned evt)
        {
            if (_state == State.WrenchAway) _state = State.Idle;
        }

        // ── Cancel / shared ───────────────────────────────────────────────

        private void CancelBuildup(ActionType type)
        {
            EndBuildupCommon();
            if (_buildupAnnounced)
            {
                // Damage 0 = cancelled; HUD hides the bar, no action fires.
                Publish(new PowerBuildupReleased(0f, false, 0, 0f, type));
                _buildupAnnounced = false;
            }

            if (_freezeRoutine != null) { StopCoroutine(_freezeRoutine); _freezeRoutine = null; }
            _player.AnimatorSpeedOverride = null;
            _player.HoldTrigger = false;
            RestoreWrenchHome();
            _state = State.Idle;
        }

        private void EndBuildupCommon()
        {
            _player.FacingOverride = null;
            if (_slowHandle >= 0)
            {
                _player.RemoveSpeedModifier(_slowHandle);
                _slowHandle = -1;
            }
        }

        private void FaceLockToMouse()
        {
            Vector2 dir = MouseWorld() - transform.position;
            if (dir.sqrMagnitude > 0.01f) _player.FacingOverride = dir.normalized;
        }

        private Vector3 MouseWorld()
            => MousePointer.Instance != null ? MousePointer.Instance.WorldPoint : transform.position + (Vector3)_player.LastMoveDirection;

        /// <summary>
        /// Range-clamped aim: the swing lands where you point, but never further
        /// than _swingRange. The target is whatever ResourceNode is closest to the
        /// clamped point — stale hover targets in the distance can't be hit.
        /// Public so SwingTargetIndicator can preview the exact same resolution
        /// every frame — the marker and the actual hit can never disagree.
        /// </summary>
        public ResourceNode ResolveAim(out Vector3 aimPoint)
        {
            Vector3 origin = transform.position;
            Vector3 raw = MouseWorld();
            Vector2 offset = raw - origin;
            if (offset.magnitude > _swingRange) offset = offset.normalized * _swingRange;
            aimPoint = origin + (Vector3)offset;

            ResourceNode bestNode = null;
            float best = float.MaxValue;
            foreach (var col in Physics2D.OverlapCircleAll(aimPoint, _targetRadius))
            {
                var node = col.GetComponentInParent<ResourceNode>();
                if (node == null) continue;
                float d = Vector2.SqrMagnitude((Vector2)node.transform.position - (Vector2)aimPoint);
                if (d < best) { best = d; bestNode = node; }
            }
            return bestNode;
        }

        private void CacheAim()
        {
            _aimNode = ResolveAim(out _aimPoint);
            _player.FacePoint(_aimPoint);
        }

        // ── Shared throw-clip start + freeze ──────────────────────────────

        private float _triggerSpeedCache = 1f;

        private void StartThrowClip(float freezePoint)
        {
            _pendingFreezePoint = freezePoint;
            _awaitingTrigger = true;

            float clipLen = _player.GetClipLength("throw");
            float animSpeed = Mathf.Max(0.05f, _settings.throwAnimSpeed);
            float duration = clipLen > 0f ? clipLen / animSpeed : 0.5f;
            _player.PlayTrigger(PlayerAnimationState.Throw, duration);
        }

        private void OnTriggerFired(PlayerAnimationState state, float triggerSpeed, AnimationClip clip)
        {
            if (state != PlayerAnimationState.Throw || !_awaitingTrigger) return;
            _awaitingTrigger = false;
            _triggerSpeedCache = triggerSpeed;

            float clipLen = clip != null ? clip.length : 0f;
            _animStartTime = Time.time;
            _animDuration = clipLen > 0f && triggerSpeed > 0f ? clipLen / triggerSpeed : 0.5f;

            float fps = clip != null ? clip.frameRate : 12f;
            float frameDur = triggerSpeed > 0f ? 1f / (fps * triggerSpeed) : 1f / fps;
            _hitDelay = 4f * frameDur;

            _freezeAt = _animDuration * _pendingFreezePoint;
            if (_freezeRoutine != null) StopCoroutine(_freezeRoutine);
            _freezeRoutine = StartCoroutine(FreezePoseAfter(_freezeAt));
        }

        private IEnumerator FreezePoseAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            // Still holding? Freeze the pose. (Release/cancel clears the override.)
            if (_state is State.SwingPress or State.SwingCharge or State.ThrowAim)
                _player.AnimatorSpeedOverride = _aimFreezeSpeed;
            _freezeRoutine = null;
        }

        private static void Publish<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }
    }
}
