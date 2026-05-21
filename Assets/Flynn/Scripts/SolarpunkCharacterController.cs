using UnityEngine;
//basic 2.5d character controller moving in 4 directions, with small jump.
//Just capsule for now with 3d rigidbody and character mover, no animations or anything. Will add those later.
public class SolarpunkCharacterController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.12f;
    [SerializeField] private LayerMask groundLayers = ~0;

    public Vector3 MoveInput => moveInput;
    public bool IsGrounded => isGrounded;
    /// <summary>Horizontal speed normalized 0-1 relative to moveSpeed.</summary>
    public float NormalizedSpeed => rb != null
        ? Mathf.Clamp01(new Vector2(rb.velocity.x, rb.velocity.z).magnitude / moveSpeed)
        : 0f;

    private Rigidbody rb;
    private Collider bodyCollider;
    private bool isGrounded;
    private Vector3 moveInput;
    private bool jumpQueued;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");

        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpQueued = true;
        }
    }

    private void FixedUpdate()
    {
        UpdateGrounded();

        Vector3 velocity = rb.velocity;
        velocity.x = moveInput.x * moveSpeed;
        velocity.z = moveInput.z * moveSpeed;
        velocity.y = rb.velocity.y; // Preserve vertical velocity for jumping/falling
        rb.velocity = velocity;

        if (jumpQueued && isGrounded)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        jumpQueued = false;
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
}
