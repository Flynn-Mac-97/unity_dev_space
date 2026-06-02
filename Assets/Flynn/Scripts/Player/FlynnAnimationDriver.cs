using UnityEngine;

/// <summary>
/// Single-responsibility component that reads SolarpunkCharacterController state
/// and drives the Animator + SpriteRenderer flip for 2.5D 4-direction animation.
/// FacingDir values: 0 = front (positive), 1 = back, 2 = side
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class FlynnAnimationDriver : MonoBehaviour
{
    [SerializeField] private SolarpunkCharacterController _controller;
    [SerializeField] private Camera _camera;
    [Tooltip("Assign the child Visual GameObject that holds the SpriteRenderer. Only this object will billboard — the physics root stays unrotated.")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField, Range(0.1f, 5f)] private float _visualScale = 1f;

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

    [Header("Animation Speed Multipliers")]
    [SerializeField, Range(0.1f, 5f)] private float _idleFrontSpeed = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _idleBackSpeed  = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _idleSideSpeed  = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _runFrontSpeed  = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _runBackSpeed   = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _runSideSpeed   = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _jumpFrontSpeed = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _jumpBackSpeed  = 1f;
    [SerializeField, Range(0.1f, 5f)] private float _jumpSideSpeed  = 1f;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private int _currentFacingDir = 0;

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");
    private static readonly int FacingDirHash   = Animator.StringToHash("FacingDir");
    private static readonly int AttackHash      = Animator.StringToHash("Attack");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");

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

    /// <summary>True while the animator is in (or blending into) an attack state.</summary>
    public bool IsAttacking =>
        _animator != null &&
        (_animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
         (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsTag("Attack")));

    /// <summary>
    /// Orients the sprite to a world-space XZ direction (front/back/side + flip).
    /// Shared by movement and aim-based attacks. Ignores Y; no-op on a zero vector.
    /// </summary>
    public void FaceWorldDirection(Vector3 worldDir)
    {
        if (_animator == null || _spriteRenderer == null) return;

        float dx = worldDir.x;
        float dz = worldDir.z;
        if (Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dz) < 0.0001f) return;

        if (Mathf.Abs(dz) >= Mathf.Abs(dx))
        {
            // Z dominant: front (z<0) or back (z>0).
            _currentFacingDir = dz < 0f ? 0 : 1;
            // Front-walk sprite has a built-in right lean; mirror it on any leftward component.
            _spriteRenderer.flipX = _currentFacingDir == 0 && dx < 0f;
        }
        else
        {
            // X dominant: side view, flip for left.
            _currentFacingDir = 2;
            _spriteRenderer.flipX = dx < 0f;
        }

        _animator.SetInteger(FacingDirHash, _currentFacingDir);
    }

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        _animator = _visualRoot != null
            ? _visualRoot.GetComponent<Animator>()
            : GetComponent<Animator>();
        _spriteRenderer = _visualRoot != null
            ? _visualRoot.GetComponent<SpriteRenderer>()
            : GetComponentInChildren<SpriteRenderer>();
        if (_camera == null) _camera = Camera.main;
        
    }

    private void LateUpdate()
    {
        if (_camera == null || _visualRoot == null) return;
        _visualRoot.rotation = _camera.transform.rotation;
        _visualRoot.localScale = Vector3.one * _visualScale;
    }

    private void Update()
    {
        Vector3 move  = _controller.MoveInput;
        float   speed = new Vector2(move.x, move.z).magnitude;

        _animator.SetFloat(SpeedHash, speed);
        _animator.SetBool(IsGroundedHash, _controller.IsGrounded);

        bool isJumping = !_controller.IsGrounded;
        bool isRunning = speed > 0.1f;
        float normalizedSpeed = _controller.NormalizedSpeed;

        float baseSpeed = _currentFacingDir switch
        {
            1 => isJumping ? _jumpBackSpeed  : isRunning ? _runBackSpeed  * normalizedSpeed : _idleBackSpeed,
            2 => isJumping ? _jumpSideSpeed  : isRunning ? _runSideSpeed  * normalizedSpeed : _idleSideSpeed,
            _ => isJumping ? _jumpFrontSpeed : isRunning ? _runFrontSpeed * normalizedSpeed : _idleFrontSpeed,
        };
        _animator.speed = Mathf.Max(0.1f, baseSpeed);

        if (speed > 0.01f)
        {
            FaceWorldDirection(move);
        }
    }
}
