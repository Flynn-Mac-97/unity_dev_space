using UnityEngine;

/// <summary>
/// Reads SolarpunkCharacterController state and feeds the Animator. Locomotion is now a
/// velocity blend tree: this just pushes VelocityX/Y/Z (Rigidbody velocity) plus Speed and
/// IsGrounded; the tree owns direction/clip selection. Also owns the wrench swing phase
/// (windup freeze / release scale on animator.speed) and exposes the IPlayerVisual surface
/// (VisualCenter, attack/charge) for the combat & interaction controllers.
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class FlynnAnimationDriver : MonoBehaviour, IPlayerVisual
{
    [SerializeField] private SolarpunkCharacterController _controller;
    [SerializeField] private Camera _camera;
    [Tooltip("Assign the child Visual GameObject that holds the SpriteRenderer. Only this object will billboard — the physics root stays unrotated.")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField, Range(0.1f, 5f)] private float _visualScale = 1f;

    [Header("Facing (sprite flip)")]
    [Tooltip("SpriteRenderer to flip. Auto-found on _visualRoot at Awake if left empty.")]
    [SerializeField] private SpriteRenderer _sprite;
    [Tooltip("Min horizontal speed before facing updates. Below this the last facing is held (no flip flicker when idling).")]
    [SerializeField, Range(0f, 1f)] private float _faceDeadzone = 0.05f;

    [Header("Visual Center (for throw origin & aim reticle)")]
    [Tooltip("Half the sprite's visible height in world units. Used to compute the billboard's visual centre for camera-corrected spawn/reticle positions.")]
    [SerializeField, Range(0f, 3f)] private float _spriteHalfHeight = 0.5f;

    /// <summary>
    /// World-space centre of the billboarded sprite, accounting for camera tilt.
    /// Because the billboard's up = camera.up (not world up), the visual centre
    /// shifts horizontally toward the viewer on a tilted perspective camera.
    /// Use this for spawn points and any indicator that should appear to originate
    /// from the sprite rather than from the physics root (feet position).
    /// </summary>
    public Vector3 VisualCenter =>
        _visualRoot != null
            ? _visualRoot.position + _visualRoot.up * _spriteHalfHeight
            : transform.position;

    /// <summary>
    /// XZ projection of VisualCenter at the character's foot Y.
    /// Use this for ground-plane indicators (aim reticle root, shadow) so they
    /// appear centred under the sprite rather than under the physics root.
    /// </summary>
    public Vector3 VisualGroundCenter =>
        new Vector3(VisualCenter.x, transform.position.y, VisualCenter.z);

    private Animator _animator;

    // Last pressed XZ facing direction, held while idle so the blend tree doesn't snap back to
    // the front/Down pose on release. Starts facing front (Down = -Z).
    private Vector2 _lastFacingXZ = new Vector2(0f, -1f);

    // ── Swing (wrench) phase ────────────────────────────────────────────────────
    // The charge windup freezes the attack clip on frame 0; the release plays it at a
    // charge-scaled speed. These own animator.speed while active so the locomotion speed
    // logic in Update doesn't stomp the freeze/scale.
    private enum SwingPhase { None, Charging, Releasing }
    private SwingPhase _swingPhase = SwingPhase.None;
    private int _swingIndex;
    private float _attackSpeed = 1f;
    private bool _releaseSeen; // saw the attack state begin after a release (Play settles a frame late)

    private const float AttackFps = 12f; // matches FlynnAnimationSetup.Fps — used to map charge → windup frame
    private const int WindupStartFrame = 1;
    private const int WindupEndFrame   = 5;

    // attackIndex → last-frame index of that attack clip (length*frameRate). Cached at Awake so
    // the windup scrub is exact and never depends on a fragile per-frame state-length read.
    private readonly System.Collections.Generic.Dictionary<int, float> _attackLastFrame
        = new System.Collections.Generic.Dictionary<int, float>();

    // Animator AttackIndex (1-4) → attack state name. Matches FlynnAnimationSetup.
    private static string AttackStateName(int animIndex) => animIndex switch
    {
        1 => "Attack_Pick",
        2 => "Attack_Axe",
        3 => "Attack_Hammer",
        _ => "Attack_Wrench",
    };

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");
    private static readonly int AttackHash      = Animator.StringToHash("Attack");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");

    private static readonly int xVelocityHash = Animator.StringToHash("VelocityX");
    private static readonly int yVelocityHash = Animator.StringToHash("VelocityY");
    private static readonly int zVelocityHash = Animator.StringToHash("VelocityZ");

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires the correct attack animation. animIndex: 1=pick 2=axe 3=hammer 4=wrench.
    /// Called by the wrench controllers; safe to call from any other system.
    ///
    /// Guard: discards the request while any attack state is already active.
    /// Attack states must carry the "Attack" tag in the Animator Controller for this
    /// guard to work. Without it, the Any State trigger queues up and double-fires
    /// when the animation exits back to idle.
    /// </summary>
    public void TriggerAttack(int animIndex)
    {
        if (_animator == null) return;
        if (IsAttacking) return;
        _animator.SetInteger(AttackIndexHash, animIndex);
        _animator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// Enter the swing windup: jump the matching attack clip to its start frame and pause it.
    /// Bypasses the AnyState "Attack" trigger (so it never double-fires); the release resumes
    /// the same state from wherever the windup paused. Pass the tool's Animator AttackIndex (1-4).
    /// </summary>
    public void BeginChargePose(int animIndex)
    {
        if (_animator == null) return;
        _swingPhase = SwingPhase.Charging;
        _swingIndex = animIndex;
        _animator.Play(AttackStateName(animIndex), 0, 0f);
        _animator.speed = 0f; // paused; UpdateChargePose drives the frame from charge
    }

    /// <summary>
    /// Drive the (paused) windup pose: the clip scrubs from frame 1 → 5 as <paramref name="charge01"/>
    /// goes 0 → 1 and holds at frame 5, so the buildup visibly winds up. Also live-swaps the tool.
    /// </summary>
    public void UpdateChargePose(int animIndex, float charge01)
    {
        return; // TODO
        if (_animator == null || _swingPhase != SwingPhase.Charging) return;
        _swingIndex = animIndex;
        _animator.Play(AttackStateName(animIndex), 0, WindupNormalizedTime(animIndex, charge01));
        _animator.speed = 0f; // paused at the scrubbed frame
    }

    /// <summary>
    /// Charge fraction → clip normalizedTime sitting on a discrete windup frame. Charge 0→1 steps
    /// the displayed frame 1,2,3,4,5 (rounded), holding on frame 5 at full charge.
    /// </summary>
    private float WindupNormalizedTime(int animIndex, float charge01)
    {
        // Last-frame index of this clip (cached). Fallback to a live read if not cached yet.
        if (!_attackLastFrame.TryGetValue(animIndex, out float lastFrame))
        {
            lastFrame = _animator.GetCurrentAnimatorStateInfo(0).length * AttackFps;
            if (lastFrame < 1f) return 0f;
        }
        int frame = Mathf.RoundToInt(Mathf.Lerp(WindupStartFrame, WindupEndFrame, Mathf.Clamp01(charge01)));
        return Mathf.Clamp01(frame / lastFrame);
    }

    /// <summary>
    /// Release the swing: play the attack clip from frame 0 at <paramref name="animSpeed"/>
    /// (lower = heavier/slower). Hands control back to locomotion when the clip exits.
    /// </summary>
    public void ReleaseSwing(int animIndex, float animSpeed)
    {
        if (_animator == null) return;
        if (animIndex != _swingIndex || _swingPhase != SwingPhase.Charging)
        {
            _swingIndex = animIndex;
            _animator.Play(AttackStateName(animIndex), 0, 0f);
        }
        _attackSpeed = Mathf.Max(0.05f, animSpeed);
        _swingPhase = SwingPhase.Releasing;
        _releaseSeen = false;
    }

    /// <summary>Abort a windup (e.g. wrench unequipped / throw started) and return to idle.</summary>
    public void CancelSwing()
    {
        if (_animator == null) { _swingPhase = SwingPhase.None; return; }
        _swingPhase = SwingPhase.None;
        _animator.speed = 1f;
        _animator.Play("Idle_Front", 0, 0f);
    }

    /// <summary>True while the animator is in (or blending into) an attack state.</summary>
    public bool IsAttacking =>
        _animator != null &&
        (_animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
         (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsTag("Attack")));

    /// <summary>
    /// No-op. Facing is now driven by the velocity blend tree (VelocityX/Y/Z), so explicit
    /// orientation is no longer needed. Kept to satisfy <see cref="IPlayerVisual"/>; the
    /// combat/interaction controllers still call it but the blend tree owns facing.
    /// </summary>
    public void FaceWorldDirection(Vector3 worldDir) { }

    /// <summary>
    /// Sets SpriteRenderer.flipX from the 8-direction facing sector of the XZ velocity.
    /// The source art is NOT consistently oriented: the side sprite faces RIGHT (flip to go
    /// left) while both diagonal sprites face LEFT (flip to go right). So flip is a per-sector
    /// lookup, not sign-of-X — this reproduces what the baked m_FlipX curves used to do.
    /// Straight up / down never flip (symmetric front/back art). Below the deadzone the last
    /// facing is held so idling doesn't snap the flip.
    /// </summary>
    private void UpdateFacing(Vector3 velocity)
    {
        if (_sprite == null) return;

        Vector2 dir = new Vector2(velocity.x, velocity.z);
        if (dir.sqrMagnitude < _faceDeadzone * _faceDeadzone) return; // hold last facing

        // 0=E 1=NE 2=N 3=NW 4=W 5=SW 6=S 7=SE  (atan2: x=right, z=up/away)
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        int sector = ((Mathf.RoundToInt(ang / 45f) % 8) + 8) % 8;

        switch (sector)
        {
            case 0: _sprite.flipX = false; break; // E  Right
            case 1: _sprite.flipX = true;  break; // NE UR
            case 2: _sprite.flipX = false; break; // N  Up   (no flip)
            case 3: _sprite.flipX = false; break; // NW UL
            case 4: _sprite.flipX = true;  break; // W  Left
            case 5: _sprite.flipX = false; break; // SW DL
            case 6: _sprite.flipX = false; break; // S  Down (no flip)
            case 7: _sprite.flipX = true;  break; // SE DR
        }
    }

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        _animator = _visualRoot != null
            ? _visualRoot.GetComponent<Animator>()
            : GetComponent<Animator>();
        if (_sprite == null && _visualRoot != null) _sprite = _visualRoot.GetComponentInChildren<SpriteRenderer>();
        if (_camera == null) _camera = Camera.main;

        if (_visualRoot != null) _visualRoot.localScale = Vector3.one * _visualScale;

        CacheAttackClipFrames();
    }

    /// <summary>
    /// Record each attack clip's last-frame index (length × frameRate) so the windup can scrub to
    /// exact frames. Clip names come from FlynnAnimationSetup: Flynn_attack_01..04 → tool index 1..4.
    /// </summary>
    private void CacheAttackClipFrames()
    {
        _attackLastFrame.Clear();
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null) continue;
            int idx = clip.name switch
            {
                "Flynn_attack_01" => 1,
                "Flynn_attack_02" => 2,
                "Flynn_attack_03" => 3,
                "Flynn_attack_04" => 4,
                _                 => 0,
            };
            if (idx == 0) continue;
            _attackLastFrame[idx] = Mathf.Max(1f, clip.length * clip.frameRate);
        }
    }

    private void LateUpdate()
    {
        if (_camera == null || _visualRoot == null) return;
        //_visualRoot.rotation = _camera.transform.rotation;
        //_visualRoot.localScale = Vector3.one * _visualScale;
    }

    private void Update()
    {
        // Locomotion is selected by a velocity blend tree: feed the Rigidbody velocity
        // (VelocityX/Y/Z), plus Speed (XZ magnitude) and IsGrounded for the idle/run/air split.
        Vector3 velocity = _controller.MoveInput; // use input for more responsive facing in the blend tree; the Rigidbody velocity feeds the actual speed parameter

        // The blend tree's (0,0) cell is the front/Down pose, so a raw zero input snaps the
        // facing back to front on release. Hold the last pressed XZ direction instead, so the
        // idle pose keeps facing wherever Flynn was last heading. Speed is still the real
        // magnitude, so this only steers the facing cell — it never fakes movement.
        Vector2 xz = new Vector2(velocity.x, velocity.z);
        if (xz.sqrMagnitude >= _faceDeadzone * _faceDeadzone) _lastFacingXZ = xz.normalized;
        Vector2 facingFeed = xz.sqrMagnitude >= _faceDeadzone * _faceDeadzone ? xz : _lastFacingXZ;

        _animator.SetFloat(xVelocityHash, facingFeed.x);
        _animator.SetFloat(yVelocityHash, velocity.y);
        _animator.SetFloat(zVelocityHash, facingFeed.y);
        _animator.SetFloat(SpeedHash, xz.magnitude);
        _animator.SetBool(IsGroundedHash, _controller.IsGrounded);

        // Flip is no longer baked into the locomotion clips (a 2D blend tree averages an
        // animated m_FlipX into nothing). Drive it here from the facing sector instead.
        UpdateFacing(new Vector3(facingFeed.x, velocity.y, facingFeed.y));

        // While swinging, the swing phase owns animator.speed (freeze on windup, scaled on
        // release) — skip the locomotion playback-speed reset.
        if (_swingPhase == SwingPhase.Charging)
        {
            _animator.speed = 0f;
            return;
        }
        if (_swingPhase == SwingPhase.Releasing)
        {
            _animator.speed = _attackSpeed;
            // Play settles a frame after ReleaseSwing; wait until we see the attack state
            // begin, then hand back to locomotion once it has exited.
            if (IsAttacking) _releaseSeen = true;
            else if (_releaseSeen) _swingPhase = SwingPhase.None;
            return;
        }

        // Blend tree plays at native rate; a prior swing may have left animator.speed scaled.
        _animator.speed = 1f;
    }
}
