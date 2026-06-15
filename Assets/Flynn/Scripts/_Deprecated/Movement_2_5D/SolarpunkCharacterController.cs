using UnityEngine;
using Flynn.Events;

// 2.5D force-based character controller. Moves on the XZ plane via acceleration
// toward a target velocity, so Unity physics drives interactions: PhysicMaterial
// friction (ice/mud), Rigidbody.drag (water), and AddForce zones (wind) all
// compose naturally instead of being overwritten. Small jump impulse, gravity
// handled by the Rigidbody.
[RequireComponent(typeof(Rigidbody))]
public class SolarpunkCharacterController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;        // target horizontal speed under input
    [SerializeField] private float acceleration = 40f;    // how hard we drive toward target
    [SerializeField] private float jumpForce = 6f;
    // Baseline drag is 0: accel-to-target already arrests horizontal motion when input is
    // released (target velocity 0 -> decel force), so we need no passive drag — and drag is
    // isotropic, which would damp jump/fall. Water zones raise this temporarily; ice keeps it low.
    [SerializeField] private float groundDrag = 0f;
    [SerializeField] private float groundCheckDistance = 0.12f;
    [SerializeField] private LayerMask groundLayers = ~0;
    // Ice low-grip is re-asserted by the zone every physics step. Latch it for a short grace
    // window so a single missed OnTriggerStay (e.g. player center crossing the trigger edge
    // while the capsule is still on the ice) doesn't snap the ground brake back on for a step
    // and bleed the slide. Grace ends well before the player is truly off the ice.
    [SerializeField] private float lowGripGrace = 0.2f;

    public Vector3 MoveInput => moveInput;
    public bool IsGrounded => isGrounded;
    /// <summary>Horizontal speed normalized 0-1 relative to moveSpeed.</summary>
    public float NormalizedSpeed => rb != null
        ? Mathf.Clamp01(new Vector2(rb.velocity.x, rb.velocity.z).magnitude / moveSpeed)
        : 0f;

    /// <summary>Multiplies steering force each physics step. Ice sets this low (slidey, weak
    /// steering, momentum dominates) — kept &gt;0 so the player can always escape ice. Auto-resets to 1.</summary>
    public float SteeringControl { get; set; } = 1f;

    /// <summary>Multiplies the target move speed each physics step. Water/Mud set this below 1 to
    /// slow the player noticeably (lowering the target, since drag/friction barely dents accel-to-target).
    /// Auto-resets to 1.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Set false each physics step by a mud zone while grounded on it: blocks jumping
    /// ("mud stops vertical movement"). Auto-resets to true.</summary>
    public bool CanJump { get; set; } = true;

    /// <summary>Set true each physics step by an ice zone: the controller stops braking toward
    /// zero when there's no input, so momentum carries (glide). Steering still applies (scaled by
    /// SteeringControl) so the player can nudge off the ice. Auto-resets to false.</summary>
    public bool LowGrip { get; set; } = false;

    /// <summary>Set false to block horizontal movement (e.g. during a swing attack).
    /// Auto-resets to true each physics step so you only need to hold it false while swinging.</summary>
    public bool CanMove { get; set; } = true;

    public Vector3 RbVelocityDirection { get; private set; }

    private Rigidbody rb;
    private Collider bodyCollider;
    private bool isGrounded;
    private bool wasGrounded;
    private Vector3 moveInput;
    private bool jumpQueued;
    private float lowGripTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        rb.drag = groundDrag;
        rb.freezeRotation = true;
    }

    private void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
        RbVelocityDirection = rb.velocity.normalized;
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        if (Input.GetButtonDown("Jump")) jumpQueued = true;
    }



    private void FixedUpdate()
    {
        UpdateGrounded();

        // Accelerate toward the input-driven target velocity (XZ only). External forces
        // (wind AddForce) and surface friction are left to the solver. SpeedMultiplier
        // lowers the target (water/mud slow); SteeringControl scales the force (ice = slidey,
        // weak steering so momentum carries, but never 0 so the player can't get stuck).
        Vector3 horizVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 inputDir = CanMove ? new Vector3(moveInput.x, 0f, moveInput.z) : Vector3.zero;
        bool idle = inputDir.sqrMagnitude < 0.01f;
        if (!CanMove)
        {
            // Kill horizontal velocity immediately when movement is locked
            Vector3 v = rb.velocity;
            v.x = 0f;
            v.z = 0f;
            rb.velocity = v;
        }
        else
        {
            // Latch low-grip: refresh the timer while the zone asserts it, otherwise decay it.
            // The brake stays off for the whole grace window, surviving single-step trigger gaps.
            if (LowGrip) lowGripTimer = lowGripGrace;
            else lowGripTimer -= Time.fixedDeltaTime;
            bool lowGrip = lowGripTimer > 0f;
            if (lowGrip)
            {
                // Ice: momentum carries, input only steers. With input, push in the input direction
                // WITHOUT the "- horizVel" brake term (that term is what kills the slide on normal
                // ground), so existing momentum is preserved and the velocity vector rotates toward
                // the input instead of being reset to it. Speed is capped to moveSpeed so the additive
                // force can't run away. Idle = no force, the frictionless surface keeps the glide.
                if (!idle)
                {
                    rb.AddForce(inputDir * moveSpeed * acceleration * SteeringControl, ForceMode.Acceleration);
                    Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                    float maxSpeed = moveSpeed * SpeedMultiplier;
                    if (hv.magnitude > maxSpeed)
                    {
                        hv = hv.normalized * maxSpeed;
                        rb.velocity = new Vector3(hv.x, rb.velocity.y, hv.z);
                    }
                }
            }
            else
            {
                // Normal ground: accelerate toward the input-driven target velocity (XZ only).
                Vector3 targetVel = inputDir * moveSpeed * SpeedMultiplier;
                Vector3 dv = targetVel - horizVel;
                rb.AddForce(dv * acceleration * SteeringControl, ForceMode.Acceleration);
            }
        }

        if (jumpQueued && isGrounded && CanJump)
        {
            Vector3 v = rb.velocity; v.y = 0f; rb.velocity = v;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
            PublishEvent(new PlayerJumped(transform.position));
        }
        jumpQueued = false;

        // Per-step terrain modifiers; zones re-assert them in OnTriggerStay (which fires
        // after FixedUpdate within the same physics step).
        SteeringControl = 1f;
        SpeedMultiplier = 1f;
        CanJump = true;
        CanMove = true;
        LowGrip = false;

        // Detect landing (was airborne, now grounded).
        if (!wasGrounded && isGrounded)
            PublishEvent(new PlayerLanded(transform.position));
        wasGrounded = isGrounded;
    }

    private void UpdateGrounded()
    {
        if (bodyCollider == null)
        {
            isGrounded = false;
            return;
        }

        Vector3 origin = bodyCollider.bounds.center;
        origin.y = bodyCollider.bounds.min.y + 0.02f;

        isGrounded = Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private static void PublishEvent<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Ground check ray
        if (bodyCollider != null)
        {
            Vector3 origin = bodyCollider.bounds.center;
            origin.y = bodyCollider.bounds.min.y + 0.02f;
            Debug.DrawLine(origin, origin + Vector3.down * groundCheckDistance,
                isGrounded ? Color.green : Color.red);
        }

        // Velocity vector
        if (rb != null)
        {
            Debug.DrawRay(transform.position, rb.velocity, Color.cyan);
        }
    }
#endif
}
