using UnityEngine;
using Flynn.Events;
using Flynn.Platforming;

/// <summary>
/// Wrench grapple for the 2D side-view platformer. Hold Q to aim, release to fire a
/// hook at whatever the cursor is over:
///   • <see cref="RopeAnchor"/>   → SELF-PULL: reel the player HORIZONTALLY toward the
///       anchor on the same plane — a momentum dash (great to boost across ice).
///       It never lifts the player vertically; gravity and jump own the Y axis.
///   • <see cref="RopePullable"/> → OBJECT-PULL: reel the object toward the player
///       (the object may cross height to reach you).
///
/// Requires the wrench as the active item. Targeting is a cursor → z=0 plane raycast
/// plus <c>Physics2D.OverlapPoint</c>, so this needs no aimer component. While
/// self-pulling it suspends the controller's horizontal control via
/// <see cref="PlatformerController2D.SuspendHorizontalControl"/> and restores it on
/// release, letting the gained momentum carry.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class RopeLassoController : MonoBehaviour
{
    [SerializeField] private RopeLassoConfig _config;
    [Tooltip("Rope visual: a child LineRenderer, enabled only while grappling.")]
    [SerializeField] private LineRenderer _ropeLine;
    [Tooltip("Layers the hook can latch onto (anchors + pullables).")]
    [SerializeField] private LayerMask _grappleMask = ~0;
    [Tooltip("Camera for the cursor raycast. Falls back to Camera.main.")]
    [SerializeField] private Camera _cam;

    private Rigidbody2D _rb;
    private PlatformerController2D _controller;

    private enum State { Idle, Firing, SelfPull, ObjectPull }
    private State _state = State.Idle;

    // Charge
    private bool _charging;
    private float _chargeTimer;

    // Firing (hook travel)
    private Vector2 _fireStart;
    private Vector2 _fireTarget;
    private float _fireElapsed;

    // Active targets / run state
    private RopeAnchor _activeAnchor;
    private RopePullable _activePullable;
    private Rigidbody2D _pullBody;
    private float _runElapsed;

    // Rope visual
    private static readonly Plane PlayPlane = new Plane(Vector3.forward, Vector3.zero);
    private const int RopeSegments = 10;
    private Vector3[] _ropePoints;

    // ── Public API (HUD / reticle) ────────────────────────────────────────────
    public bool IsCharging => _charging;
    public float ChargeNormalized => _config != null && _config.chargeTime > 0f
        ? Mathf.Clamp01(_chargeTimer / _config.chargeTime) : 0f;
    public bool IsActive => _state != State.Idle;

    private Vector2 HandPoint => transform.position;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _controller = GetComponent<PlatformerController2D>();
        if (_cam == null) _cam = Camera.main;

        _ropePoints = new Vector3[RopeSegments];
        if (_ropeLine != null)
        {
            _ropeLine.positionCount = RopeSegments;
            _ropeLine.enabled = false;
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Firing: UpdateFiring(); break;
            case State.SelfPull:
            case State.ObjectPull:
                if (Input.GetKeyUp(KeyCode.Q)) EndGrapple();
                break;
        }
    }

    private void UpdateIdle()
    {
        bool usable = PlayerInventory.Instance != null
                      && PlayerInventory.Instance.ActiveItemType == ItemType.Wrench;
        if (!usable) { _charging = false; _chargeTimer = 0f; return; }

        if (Input.GetKeyDown(KeyCode.Q)) { _charging = true; _chargeTimer = 0f; }
        else if (_charging && Input.GetKey(KeyCode.Q)) _chargeTimer += Time.deltaTime;
        else if (_charging && Input.GetKeyUp(KeyCode.Q)) { Fire(); _charging = false; _chargeTimer = 0f; }
    }

    // ── Fire ──────────────────────────────────────────────────────────────────

    private void Fire()
    {
        if (_config == null || !TryGetCursorWorld(out Vector2 cursor)) return;

        Collider2D hit = Physics2D.OverlapPoint(cursor, _grappleMask);
        if (hit == null) return;

        RopeAnchor anchor = hit.GetComponentInParent<RopeAnchor>();
        RopePullable pullable = hit.GetComponentInParent<RopePullable>();
        if (anchor == null && pullable == null) return;

        Vector2 grapplePoint = hit.ClosestPoint(cursor); // latch on the collider
        float dist = Vector2.Distance(HandPoint, grapplePoint);
        if (dist < _config.minRange || dist > _config.maxRange) return;

        // Battery gate.
        var battery = RobotBattery.Instance;
        float cost = battery != null && battery.Settings != null ? battery.Settings.grappleCost : 0f;
        if (battery != null && !battery.CanSpend(cost)) return;
        if (battery != null && cost > 0f) battery.Spend(cost);

        _activeAnchor = anchor;
        _activePullable = pullable;
        _fireStart = HandPoint;
        _fireTarget = grapplePoint;
        _fireElapsed = 0f;
        _state = State.Firing;

        if (GameEventBus.Instance != null)
        {
            if (pullable != null) GameEventBus.Instance.Publish(new RopePullStarted(pullable));
            else if (anchor != null) GameEventBus.Instance.Publish(new RopeGrappleStarted(anchor));
        }
    }

    private bool TryGetCursorWorld(out Vector2 world)
    {
        world = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!PlayPlane.Raycast(ray, out float dist)) return false;
        world = ray.GetPoint(dist);
        return true;
    }

    private void UpdateFiring()
    {
        _fireElapsed += Time.deltaTime;
        float dur = _config != null ? _config.hookTravelTime : 0.12f;
        float t = dur > 0f ? Mathf.Clamp01(_fireElapsed / dur) : 1f;

        float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out for a snappy throw
        Vector3 hookPos = Vector2.Lerp(_fireStart, _fireTarget, eased);
        DrawRope(HandPoint, hookPos, 0.3f * (1f - t));

        if (t < 1f) return;

        _runElapsed = 0f;
        if (_activePullable != null)
        {
            _pullBody = _activePullable.Body;
            if (_pullBody == null) { EndGrapple(); return; }
            _state = State.ObjectPull;
        }
        else
        {
            _state = State.SelfPull;
            if (_controller != null) _controller.SuspendHorizontalControl = true;
        }
    }

    // ── Physics ─────────────────────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (_state == State.SelfPull) SelfPullStep();
        else if (_state == State.ObjectPull) ObjectPullStep();
    }

    /// <summary>Horizontal-only reel toward the anchor's X. Y is left to gravity/jump,
    /// so the player is never lifted — they dash along the same plane.</summary>
    private void SelfPullStep()
    {
        if (_rb == null) { EndGrapple(); return; }

        _runElapsed += Time.fixedDeltaTime;
        if (_config != null && _runElapsed >= _config.pullTimeout) { EndGrapple(); return; }
        if (DrainPullBattery()) { EndGrapple(); return; }

        float dx = _fireTarget.x - _rb.position.x;
        float stop = _config != null ? _config.selfPullStopRadius : 0.6f;
        if (Mathf.Abs(dx) <= stop) { EndGrapple(); return; }

        float force = _config != null ? _config.selfPullForce : 60f;
        float maxSpeed = _config != null ? _config.maxSelfPullSpeed : 16f;

        float vx = _rb.velocity.x + Mathf.Sign(dx) * force * Time.fixedDeltaTime;
        vx = Mathf.Clamp(vx, -maxSpeed, maxSpeed);
        _rb.velocity = new Vector2(vx, _rb.velocity.y); // Y untouched

        if (_controller != null) _controller.SuspendHorizontalControl = true; // hold each step
    }

    /// <summary>Reel the object toward the player (any direction, may cross height).</summary>
    private void ObjectPullStep()
    {
        if (_pullBody == null) { EndGrapple(); return; }

        _runElapsed += Time.fixedDeltaTime;
        if (_config != null && _runElapsed >= _config.pullTimeout) { EndGrapple(); return; }
        if (DrainPullBattery()) { EndGrapple(); return; }

        Vector2 to = HandPoint - _pullBody.position;
        float stop = _config != null ? _config.stopRadius : 0.8f;
        if (to.magnitude <= stop) { EndGrapple(); return; }

        float force = _config != null ? _config.pullForce : 55f;
        float maxSpeed = _config != null ? _config.maxPullSpeed : 14f;

        _pullBody.velocity += to.normalized * force * Time.fixedDeltaTime;
        if (_pullBody.velocity.magnitude > maxSpeed)
            _pullBody.velocity = _pullBody.velocity.normalized * maxSpeed;
    }

    private bool DrainPullBattery()
    {
        var b = RobotBattery.Instance;
        if (b != null && b.Settings != null && b.Settings.pullCostPerSecond > 0f)
        {
            b.DrainOverTime(b.Settings.pullCostPerSecond);
            if (b.IsEmpty) return true;
        }
        return false;
    }

    // ── End ───────────────────────────────────────────────────────────────────

    private void EndGrapple()
    {
        if (GameEventBus.Instance != null)
        {
            if (_activeAnchor != null) GameEventBus.Instance.Publish(new RopeGrappleEnded(_activeAnchor));
            if (_activePullable != null) GameEventBus.Instance.Publish(new RopePullEnded(_activePullable));
        }

        // Release horizontal control — the gained momentum carries.
        if (_controller != null) _controller.SuspendHorizontalControl = false;

        _state = State.Idle;
        _activeAnchor = null;
        _activePullable = null;
        _pullBody = null;
        if (_ropeLine != null) _ropeLine.enabled = false;
    }

    // ── Rope visual ─────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (_ropeLine == null) return;

        switch (_state)
        {
            case State.SelfPull:
                DrawRope(HandPoint, _fireTarget, 0.05f);
                break;
            case State.ObjectPull:
                if (_pullBody != null) DrawRope(HandPoint, _pullBody.position, 0.08f);
                else _ropeLine.enabled = false;
                break;
            case State.Firing:
                break; // drawn in UpdateFiring
            default:
                _ropeLine.enabled = false;
                break;
        }
    }

    /// <summary>Draw the rope start→end with a parabolic sag (slack=0 → taut line).</summary>
    private void DrawRope(Vector3 start, Vector3 end, float slack)
    {
        _ropeLine.enabled = true;
        float dist = Vector3.Distance(start, end);
        for (int i = 0; i < RopeSegments; i++)
        {
            float t = (float)i / (RopeSegments - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            p += Vector3.down * (slack * 4f * t * (1f - t) * dist);
            _ropePoints[i] = p;
        }
        _ropeLine.SetPositions(_ropePoints);
    }
}
