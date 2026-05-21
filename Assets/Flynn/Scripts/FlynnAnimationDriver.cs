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

    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int FacingDirHash  = Animator.StringToHash("FacingDir");

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
            if (Mathf.Abs(move.z) >= Mathf.Abs(move.x))
            {
                // Z dominant: front (z<0) or back (z>0)
                _currentFacingDir = move.z < 0f ? 0 : 1;
                _spriteRenderer.flipX = false;
            }
            else
            {
                // X dominant: side view, flip for left movement
                _currentFacingDir = 2;
                _spriteRenderer.flipX = move.x < 0f;
            }

            _animator.SetInteger(FacingDirHash, _currentFacingDir);
        }
    }
}
