using UnityEngine;

/// <summary>
/// Basic 2D character controller for pure XY-plane movement.
/// Uses Rigidbody2D for collision-aware movement.
/// Reads legacy Input manager axes (Horizontal, Vertical).
/// Responds to terrain zones via <see cref="TerrainStateAggregator2D"/>.
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(TerrainStateAggregator2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Maximum movement speed in units/sec.")]
    [SerializeField] private float _moveSpeed = 5f;

    [Tooltip("How quickly the character accelerates to max speed.")]
    [SerializeField] private float _acceleration = 12f;

    [Tooltip("How quickly the character decelerates to zero.")]
    [SerializeField] private float _deceleration = 10f;

    [Header("Facing")]
    [Tooltip("Flip the sprite to face movement direction.")]
    [SerializeField] private bool _flipToFaceDirection = true;

    // ── Components ────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private TerrainStateAggregator2D _terrain;

    // ── Public State ──────────────────────────────────────────────────────

    public Vector2 MoveInput { get; private set; }
    public Vector2 Velocity => _rb.velocity;
    public float NormalizedSpeed => _moveSpeed > 0f ? _rb.velocity.magnitude / _moveSpeed : 0f;
    public bool IsMoving => MoveInput.sqrMagnitude > 0.01f;
    public bool CanJump => _terrain != null && !_terrain.CurrentState.BlocksJump;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        _terrain = GetComponent<TerrainStateAggregator2D>();

        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (MoveInput.sqrMagnitude > 1f)
            MoveInput.Normalize();
    }

    private void FixedUpdate()
    {
        var terrain = _terrain.CurrentState;

        // Terrain modifiers
        float speedMul = terrain.SpeedMultiplier;
        float decelRetention = terrain.DecelerationRetention;

        Vector2 targetVelocity = MoveInput * _moveSpeed * speedMul;
        float delta = Time.fixedDeltaTime;
        float accel = _acceleration;
        float decel = _deceleration * decelRetention;

        Vector2 velocity = _rb.velocity;

        if (MoveInput.sqrMagnitude > 0.01f)
        {
            // On ice: also reduce acceleration for slidey feel
            if (terrain.LowGrip) accel *= decelRetention;
            velocity = Vector2.MoveTowards(velocity, targetVelocity, accel * delta);
        }
        else
        {
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, decel * delta);
        }

        // Apply external forces (wind, etc.)
        velocity += terrain.ExternalForce * delta;

        _rb.velocity = velocity;

        // Flip sprite to face movement direction
        if (_flipToFaceDirection && MoveInput.x != 0f)
            _sr.flipX = MoveInput.x < 0f;
    }
}
