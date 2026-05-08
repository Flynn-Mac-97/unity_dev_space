using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gridMinX = 0.3f;
    [SerializeField] private float gridMaxX = 9.7f;
    [SerializeField] private float gridMinZ = 0.3f;
    [SerializeField] private float gridMaxZ = 9.7f;

    private CharacterController _cc;
    private float _verticalVelocity;
    private const float Gravity = -20f;

    // Isometric movement axes: camera is rotated Y=45, so iso forward = (+X,+Z), iso right = (+X,-Z)
    private static readonly Vector3 IsoRight   = new Vector3( 1f, 0f, -1f).normalized;
    private static readonly Vector3 IsoForward = new Vector3( 1f, 0f,  1f).normalized;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = IsoRight * h + IsoForward * v;
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (_cc.isGrounded)
            _verticalVelocity = -2f;
        else
            _verticalVelocity += Gravity * Time.deltaTime;

        _cc.Move((move * moveSpeed + Vector3.up * _verticalVelocity) * Time.deltaTime);

        // Hard-clamp to grid bounds — prevents falling off edges
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, gridMinX, gridMaxX);
        p.z = Mathf.Clamp(p.z, gridMinZ, gridMaxZ);
        transform.position = p;
    }
}
