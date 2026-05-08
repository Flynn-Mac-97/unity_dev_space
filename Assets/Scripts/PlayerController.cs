using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gridMinX = 0.3f;
    [SerializeField] private float gridMaxX = 9.7f;
    [SerializeField] private float gridMinZ = 0.3f;
    [SerializeField] private float gridMaxZ = 9.7f;

    [Header("Prototype Bounce")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float bounceAmplitude = 0.14f;
    [SerializeField] private float bounceCadence = 4.2f;
    [SerializeField] private float stretchAmount = 0.04f;
    [SerializeField] private float leanDegrees = 6f;
    [SerializeField] private float leanOffset = 0.035f;
    [SerializeField] private float animationSmoothing = 14f;

    private CharacterController _cc;
    private float _verticalVelocity;
    private const float Gravity = -20f;

    private bool _hasVisualRoot;
    private Vector3 _baseVisualLocalPosition;
    private Vector3 _baseVisualLocalScale;
    private Quaternion _baseVisualLocalRotation;
    private float _bouncePhase;

    // Isometric movement axes: camera is rotated Y=45, so iso forward = (+X,+Z), iso right = (+X,-Z)
    private static readonly Vector3 IsoRight   = new Vector3( 1f, 0f, -1f).normalized;
    private static readonly Vector3 IsoForward = new Vector3( 1f, 0f,  1f).normalized;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        if (visualRoot == null)
        {
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
                visualRoot = sprite.transform;
        }

        _hasVisualRoot = visualRoot != null;
        if (_hasVisualRoot)
        {
            _baseVisualLocalPosition = visualRoot.localPosition;
            _baseVisualLocalScale = visualRoot.localScale;
            _baseVisualLocalRotation = visualRoot.localRotation;
        }
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

        UpdatePrototypeBounce(move);
    }

    private void UpdatePrototypeBounce(Vector3 move)
    {
        if (!_hasVisualRoot) return;

        float moveStrength = Mathf.Clamp01(move.magnitude);
        bool isMoving = moveStrength > 0.01f;

        float targetX = _baseVisualLocalPosition.x;
        float targetY = _baseVisualLocalPosition.y;
        Vector3 targetScale = _baseVisualLocalScale;
        Quaternion targetRotation = _baseVisualLocalRotation;

        if (isMoving)
        {
            _bouncePhase += Time.deltaTime * bounceCadence * Mathf.Lerp(0.7f, 1.0f, moveStrength);
            float wave = Mathf.Sin(_bouncePhase * Mathf.PI * 2f);

            targetY += wave * bounceAmplitude;

            float squash = Mathf.Abs(wave) * stretchAmount;
            targetScale = new Vector3(
                _baseVisualLocalScale.x * (1f + squash),
                _baseVisualLocalScale.y * (1f - squash),
                _baseVisualLocalScale.z);

            // Lean and offset are derived from world X movement for a quick, readable directional cue.
            float directional = Mathf.Clamp(move.x, -1f, 1f);
            targetX += directional * leanOffset;
            float zAngle = -directional * leanDegrees;
            targetRotation = _baseVisualLocalRotation * Quaternion.Euler(0f, 0f, zAngle);
        }

        float t = 1f - Mathf.Exp(-animationSmoothing * Time.deltaTime);

        Vector3 targetPosition = new Vector3(
            targetX,
            targetY,
            _baseVisualLocalPosition.z);

        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, t);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, t);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRotation, t);
    }
}
