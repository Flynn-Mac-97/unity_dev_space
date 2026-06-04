using UnityEngine;

/// <summary>
/// Wrench rope/lasso (winch grapple). Hold Q to charge, release to fire. Grapples exactly
/// what the cursor is over (resolved by <see cref="PlayerMouseAimer"/>); the marker kind decides mode:
///  - a movable <see cref="RopePullable"/> → reel the object UP toward the player;
///  - a <see cref="RopeAnchor"/> → reel the PLAYER to the exact clicked surface point.
/// Winching is force-based (AddForce) so it composes with the rest of the physics. Blocked
/// while standing on mud (reuses the controller's CanJump signal). Wrench must be the active
/// item, like swing/throw. A LineRenderer draws the rope from the hand to the far end while reeling.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerMouseAimer))]
public class RopeLassoController : MonoBehaviour
{
    [SerializeField] private RopeLassoConfig _config;
    [Tooltip("Rope visual. A child LineRenderer with 2 positions; enabled only while reeling.")]
    [SerializeField] private LineRenderer _ropeLine;

    private PlayerInventory _inventory;
    private PlayerMouseAimer _aimer;
    private IPlayerVisual _animDriver;
    private SolarpunkCharacterController _controller;
    private Rigidbody _rb;

    private float _chargeTimer;
    private bool _charging;

    // Active winch state.
    private bool _pulling;
    private bool _selfPull;        // true: reel player to anchor; false: reel object to player.
    private Rigidbody _pullBody;   // the body receiving force.
    private Vector3 _anchorPoint;  // fixed far end for a self-pull.
    private float _pullElapsed;

    public bool IsCharging => _charging;
    public float ChargeNormalized => _config != null && _config.chargeTime > 0f
        ? Mathf.Clamp01(_chargeTimer / _config.chargeTime)
        : 0f;
    public bool IsPulling => _pulling;

    /// <summary>Billboard visual centre, so the rope leaves from the sprite's hand, not the feet.</summary>
    public Vector3 HandPoint => _animDriver != null ? _animDriver.VisualCenter : transform.position;

    private void Awake()
    {
        _inventory  = GetComponent<PlayerInventory>();
        _aimer      = GetComponent<PlayerMouseAimer>();
        _animDriver = GetComponent<IPlayerVisual>();
        _controller = GetComponent<SolarpunkCharacterController>();
        _rb         = GetComponent<Rigidbody>();
        if (_ropeLine != null)
        {
            _ropeLine.positionCount = 2;
            _ropeLine.enabled = false;
        }
    }

    private void Update()
    {
        // Mud blocks the lasso: the Mud TerrainEffectZone clears CanJump while grounded on it.
        bool onMud = _controller != null && !_controller.CanJump;
        bool usable = _inventory.ActiveItemType == ItemType.Wrench && !_pulling && !onMud;
        if (!usable)
        {
            _charging = false;
            _chargeTimer = 0f;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _charging = true;
            _chargeTimer = 0f;
        }
        else if (_charging && Input.GetKey(KeyCode.Q))
        {
            _chargeTimer += Time.deltaTime;
        }
        else if (_charging && Input.GetKeyUp(KeyCode.Q))
        {
            FireLasso();
            _charging = false;
            _chargeTimer = 0f;
        }
    }

    private void FireLasso()
    {
        if (_config == null) return;

        // Grapple exactly what the cursor is over (resolved by PlayerMouseAimer). The kind of
        // marker decides the mode: a RopePullable = reel it up to the player; a RopeAnchor =
        // reel the player to the exact clicked surface point. Range is gated here against that
        // precise hit point so min/maxRange are honoured regardless of object size.
        RopeAnchor anchor = _aimer.HoveredAnchor;
        RopePullable pullable = _aimer.HoveredPullable;
        if (anchor == null && pullable == null) return;     // nothing hovered → no grapple

        Vector3 grapplePoint = _aimer.HoveredGrapplePoint;
        float dist = Vector3.Distance(transform.position, grapplePoint);
        if (dist < _config.minRange || dist > _config.maxRange) return;

        if (pullable != null)
            BeginPull(selfPull: false, body: pullable.Body, anchorPoint: Vector3.zero);
        else
            BeginPull(selfPull: true, body: _rb, anchorPoint: grapplePoint);
    }

    private void BeginPull(bool selfPull, Rigidbody body, Vector3 anchorPoint)
    {
        if (body == null) return;
        _pulling = true;
        _selfPull = selfPull;
        _pullBody = body;
        _anchorPoint = anchorPoint;
        _pullElapsed = 0f;

        Vector3 far = selfPull ? anchorPoint : body.position;
        if (_animDriver != null) _animDriver.FaceWorldDirection(far - transform.position);
    }

    private void FixedUpdate()
    {
        if (!_pulling) return;
        if (_pullBody == null) { EndPull(); return; }

        _pullElapsed += Time.fixedDeltaTime;

        // Self-pull reels toward the fixed anchor; object-pull reels toward the (moving) player hand.
        Vector3 target = _selfPull ? _anchorPoint : HandPoint;
        Vector3 to = target - _pullBody.position;

        if (to.magnitude <= _config.stopRadius || _pullElapsed >= _config.pullTimeout)
        {
            EndPull();
            return;
        }

        _pullBody.AddForce(to.normalized * _config.pullForce, ForceMode.Acceleration);
        if (_pullBody.velocity.magnitude > _config.maxPullSpeed)
            _pullBody.velocity = _pullBody.velocity.normalized * _config.maxPullSpeed;

        // Suppress steering so movement input doesn't fight a self-reel (reasserted each step,
        // like a terrain zone; the controller auto-resets it).
        if (_selfPull && _controller != null)
            _controller.SteeringControl = _config.steeringDuringPull;
    }

    private void EndPull()
    {
        _pulling = false;
        _pullBody = null;
    }

    // Draw the rope from the hand to the far end while reeling.
    private void LateUpdate()
    {
        if (_ropeLine == null) return;
        if (_pulling && _pullBody != null)
        {
            _ropeLine.enabled = true;
            _ropeLine.SetPosition(0, HandPoint);
            _ropeLine.SetPosition(1, _selfPull ? _anchorPoint : _pullBody.position);
        }
        else
        {
            _ropeLine.enabled = false;
        }
    }
}
