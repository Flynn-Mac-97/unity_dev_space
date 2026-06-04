using UnityEngine;

/// <summary>
/// Drives the procedural cube character rig (twin-stick). Replaces the sprite-sheet
/// <see cref="FlynnAnimationDriver"/> on the cube player while satisfying the same
/// <see cref="IPlayerVisual"/> contract, so wrench/throw/rope/reticle keep working.
///
/// Rig layout (assigned in the Inspector):
///   BodyRig  — yaws to follow the mouse aim point (torso + arms).
///   LegRig   — yaws to the movement direction when moving, blends back to body when idle.
///   FootL/R  — translated in an opposite-phase step cycle along LegRig's local forward.
///   ArmR     — pitched back on charge, swept forward on release (procedural swing pose).
///
/// Decoupling BodyRig (→ mouse) from LegRig (→ move) is what produces the twin-stick look:
/// the torso aims while the legs walk in the direction you actually travel.
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class ProceduralCharacterDriver : MonoBehaviour, IPlayerVisual
{
    [Header("Sources")]
    [SerializeField] private SolarpunkCharacterController _controller;

    [Header("Rig")]
    [Tooltip("Torso pivot. Yaws to face the mouse aim point. Holds the body cube + arms.")]
    [SerializeField] private Transform _bodyRig;
    [Tooltip("Leg pivot. Yaws to the movement direction; blends to body facing when idle.")]
    [SerializeField] private Transform _legRig;
    [SerializeField] private Transform _footL;
    [SerializeField] private Transform _footR;
    [Tooltip("Right arm, swept procedurally during a swing.")]
    [SerializeField] private Transform _armR;
    [Tooltip("Hand position used as the throw/rope origin (VisualCenter). Falls back to body centre.")]
    [SerializeField] private Transform _handSocket;

    [Header("Facing")]
    [Tooltip("Rest / idle facing in world space (default -Z = toward the camera / screen-down).")]
    [SerializeField] private Vector3 _restForward = Vector3.back;
    [Tooltip("How far the body turns toward the movement heading off the rest facing. " +
             "1 = face the move direction fully (up = away from camera); " +
             "lower softens it (0.5 → pure left/right leans ~45°). Idle always returns to rest.")]
    [SerializeField, Range(0f, 1f)] private float _leanFactor = 1f;
    [Tooltip("How fast the body/legs slerp toward their target facing (per second).")]
    [SerializeField] private float _turnSpeed = 14f;

    [Header("Step cycle")]
    [Tooltip("Stride length: how far each foot travels fore/aft from neutral (world units).")]
    [SerializeField] private float _stride = 0.12f;
    [Tooltip("Foot lift height at the top of a step (world units).")]
    [SerializeField] private float _stepLift = 0.06f;
    [Tooltip("Step cycles per second at full speed.")]
    [SerializeField] private float _stepFrequency = 2.2f;

    [Header("Visual centre")]
    [Tooltip("Used only when no hand socket is assigned: body-centre height above the root.")]
    [SerializeField] private float _visualHalfHeight = 0.4f;

    [Header("Swing pose")]
    [Tooltip("Arm pitch (deg, local X) at full windup charge.")]
    [SerializeField] private float _windupAngle = -70f;
    [Tooltip("Arm pitch (deg, local X) at the forward end of a swing.")]
    [SerializeField] private float _swingThroughAngle = 60f;
    [Tooltip("Seconds the release sweep takes.")]
    [SerializeField] private float _swingDuration = 0.25f;

    private const float MoveEpsilon = 0.05f;

    private enum SwingPhase { None, Charging, Releasing }
    private SwingPhase _swingPhase = SwingPhase.None;
    private float _chargePitch;       // current windup pitch while charging
    private float _swingTimer;        // counts down through the release sweep
    private Quaternion _armRest = Quaternion.identity;

    private Vector3 _footLRest;
    private Vector3 _footRRest;
    private float _stepPhase;
    private float _stepWeight;        // 0 idle → 1 moving, smoothed so feet settle

    // ── IPlayerVisual ───────────────────────────────────────────────────────────

    public Vector3 VisualCenter =>
        _handSocket != null ? _handSocket.position
                            : transform.position + Vector3.up * _visualHalfHeight;

    public Vector3 VisualGroundCenter =>
        new Vector3(VisualCenter.x, transform.position.y, VisualCenter.z);

    public bool IsAttacking => _swingPhase != SwingPhase.None;

    public void TriggerAttack(int animIndex)
    {
        // No charge: snap straight into a release sweep.
        if (_swingPhase == SwingPhase.None) ReleaseSwing(animIndex, 1f);
    }

    public void BeginChargePose(int animIndex)
    {
        _swingPhase = SwingPhase.Charging;
        _chargePitch = 0f;
    }

    public void UpdateChargePose(int animIndex, float charge01)
    {
        if (_swingPhase != SwingPhase.Charging) return;
        _chargePitch = Mathf.Lerp(0f, _windupAngle, Mathf.Clamp01(charge01));
    }

    public void ReleaseSwing(int animIndex, float animSpeed)
    {
        _swingPhase = SwingPhase.Releasing;
        // Higher animSpeed → shorter sweep (faster follow-through).
        _swingTimer = _swingDuration / Mathf.Max(0.05f, animSpeed);
    }

    public void CancelSwing()
    {
        _swingPhase = SwingPhase.None;
        _swingTimer = 0f;
        _chargePitch = 0f;
    }

    public void FaceWorldDirection(Vector3 worldDir)
    {
        // No-op: facing is fully movement-driven (lean-to-move). Kept for the IPlayerVisual
        // contract — the wrench/rope controllers still call it, but it must not fight the lean.
    }

    // ── Unity messages ────────────────────────────────────────────────────────

    private float _restYaw;

    private void Awake()
    {
        if (_controller == null) _controller = GetComponent<SolarpunkCharacterController>();
        Vector3 rest = _restForward; rest.y = 0f;
        if (rest.sqrMagnitude < 0.0001f) rest = Vector3.back;
        _restYaw = Quaternion.LookRotation(rest.normalized, Vector3.up).eulerAngles.y;
        if (_footL != null) _footLRest = _footL.localPosition;
        if (_footR != null) _footRRest = _footR.localPosition;
        if (_armR != null) _armRest = _armR.localRotation;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateBodyFacing(dt);
        UpdateLegFacing(dt);
        UpdateStepCycle(dt);
        UpdateSwingPose(dt);
    }

    // ── Rig logic ───────────────────────────────────────────────────────────────

    private void UpdateBodyFacing(float dt)
    {
        if (_bodyRig == null) return;

        Vector3 move = _controller.MoveInput;
        move.y = 0f;

        // Turn toward the movement heading in all directions (up = facing away from camera).
        // _leanFactor scales how much of the rest→move angular delta is applied; 1 = face the
        // move direction fully. Idle → rest facing.
        float targetYaw = _restYaw;
        if (move.sqrMagnitude > MoveEpsilon * MoveEpsilon)
        {
            float moveYaw = Quaternion.LookRotation(move.normalized, Vector3.up).eulerAngles.y;
            targetYaw = _restYaw + Mathf.DeltaAngle(_restYaw, moveYaw) * _leanFactor;
        }

        Quaternion target = Quaternion.Euler(0f, targetYaw, 0f);
        _bodyRig.rotation = Quaternion.Slerp(_bodyRig.rotation, target, _turnSpeed * dt);
    }

    private void UpdateLegFacing(float dt)
    {
        // Legs share the body facing; the step cycle runs along that shared forward.
        if (_legRig != null && _bodyRig != null) _legRig.rotation = _bodyRig.rotation;
    }

    private void UpdateStepCycle(float dt)
    {
        float speed01 = Mathf.Clamp01(_controller != null ? _controller.NormalizedSpeed : 0f);
        bool moving = speed01 > 0.05f;

        // Ease the step amplitude in/out so feet settle cleanly when stopping.
        _stepWeight = Mathf.MoveTowards(_stepWeight, moving ? 1f : 0f, dt * 6f);
        if (moving) _stepPhase += dt * _stepFrequency * Mathf.Lerp(0.4f, 1f, speed01) * Mathf.PI * 2f;

        float s = Mathf.Sin(_stepPhase) * _stepWeight;
        ApplyFoot(_footL, _footLRest, s);
        ApplyFoot(_footR, _footRRest, -s);
    }

    private void ApplyFoot(Transform foot, Vector3 rest, float phase)
    {
        if (foot == null) return;
        // phase ∈ [-1,1]: fore/aft along local Z, lift only on the forward (positive) half.
        foot.localPosition = rest
            + Vector3.forward * (phase * _stride)
            + Vector3.up * (Mathf.Max(0f, phase) * _stepLift);
    }

    private void UpdateSwingPose(float dt)
    {
        if (_armR == null) return;

        float pitch;
        switch (_swingPhase)
        {
            case SwingPhase.Charging:
                pitch = _chargePitch;
                break;
            case SwingPhase.Releasing:
                _swingTimer -= dt;
                float t = _swingDuration > 0f
                    ? 1f - Mathf.Clamp01(_swingTimer / (_swingDuration))
                    : 1f;
                // Sweep from the windup angle through to the follow-through angle.
                pitch = Mathf.Lerp(_windupAngle, _swingThroughAngle, t);
                if (_swingTimer <= 0f) _swingPhase = SwingPhase.None;
                break;
            default:
                pitch = 0f;
                break;
        }

        Quaternion target = _armRest * Quaternion.Euler(pitch, 0f, 0f);
        // Snap while actively swinging, ease back to rest when idle.
        _armR.localRotation = _swingPhase == SwingPhase.None
            ? Quaternion.Slerp(_armR.localRotation, target, 12f * dt)
            : target;
    }
}
