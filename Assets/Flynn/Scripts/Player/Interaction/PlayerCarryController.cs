using UnityEngine;

/// <summary>
/// Push/pull objects with E. When the player grabs a <see cref="Grabbable"/>, they
/// lock onto whichever face they approached from. The object stays on the ground on
/// that side of the player while they walk — walking toward the crate pushes it,
/// walking away pulls it, walking sideways translates both. The crate never rotates
/// around the player. Press E again to release.
/// </summary>
[RequireComponent(typeof(PlayerMouseAimer))]
public class PlayerCarryController : MonoBehaviour
{
    [SerializeField] private KeyCode _grabKey = KeyCode.E;

    [Header("Push/pull positioning")]
    [Tooltip("Extra gap between the player and the held object to prevent overlap with the player's capsule.")]
    [SerializeField] private float _skinWidth = 0.15f;
    [Tooltip("How fast the held object tracks its target position.")]
    [SerializeField] private float _trackingSpeed = 20f;

    [Header("Movement")]
    [Tooltip("Speed multiplier applied to the player while carrying an object (0–1, lower = slower).")]
    [SerializeField, Range(0.1f, 1f)] private float _carrySpeedMultiplier = 0.6f;

    private PlayerMouseAimer _aimer;
    private Grabbable _heldObject;
    private Rigidbody _playerRb;
    private SolarpunkCharacterController _controller;

    // The locked hold side: unit vector from crate center toward the player at grab time,
    // snapped to the nearest cardinal axis (±X or ±Z).
    private Vector3 _holdSide;

    // Distance from the player center to the crate center along the hold-side axis.
    private float _holdDistance;

    // The crate's ground Y so it stays on the floor.
    private float _crateGroundY;

    /// <summary>True while carrying an object.</summary>
    public bool IsCarrying => _heldObject != null;

    private void Awake()
    {
        _aimer = GetComponent<PlayerMouseAimer>();
        _playerRb = GetComponent<Rigidbody>();
        _controller = GetComponent<SolarpunkCharacterController>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_grabKey)) return;

        if (IsCarrying)
        {
            Release();
            return;
        }

        Grabbable target = FindGrabbableTarget();
        if (target != null && !target.IsHeld)
            Grab(target);
    }

    private void LateUpdate()
    {
        if (!IsCarrying || _heldObject == null) return;

        // Position the crate on the locked hold side of the player.
        Vector3 playerPos = transform.position;
        Vector3 crateTarget = playerPos - _holdSide * _holdDistance;
        crateTarget.y = _crateGroundY;

        _heldObject.transform.position = Vector3.Lerp(
            _heldObject.transform.position, crateTarget, _trackingSpeed * Time.deltaTime);

        // Keep the crate upright and unmoving rotationally
        _heldObject.transform.rotation = Quaternion.identity;
    }

    private void FixedUpdate()
    {
        // Slow the player while carrying
        if (_controller != null && IsCarrying)
            _controller.SpeedMultiplier = _carrySpeedMultiplier;
    }

    /// <summary>Find a Grabbable under the cursor within interaction range.</summary>
    private Grabbable FindGrabbableTarget()
    {
        if (_aimer == null) return null;

        IInteractionPromptProvider interactable = _aimer.HoveredInteractable;
        if (interactable is Grabbable grabbable)
            return grabbable;

        GameObject hovered = _aimer.HoveredGameObject;
        if (hovered != null)
        {
            Grabbable g = hovered.GetComponentInParent<Grabbable>();
            if (g != null)
            {
                float dist = Vector3.Distance(transform.position, g.transform.position);
                if (dist <= _aimer.InteractionRange)
                    return g;
            }
        }

        return null;
    }

    private void Grab(Grabbable target)
    {
        _heldObject = target;

        // Determine which face the player is on
        Vector3 toPlayer = transform.position - target.transform.position;
        toPlayer.y = 0f;
        _holdSide = SnapToCardinal(toPlayer);

        // Calculate the hold distance: player half-extent + crate half-extent + skin.
        // The skin width keeps the player's capsule clearly outside the crate so
        // the physics solver never sees an overlap.
        float playerRadius = 0.23f;
        float crateExtent = GetExtentsAlong(target, _holdSide);
        _holdDistance = playerRadius + crateExtent + _skinWidth;

        // Remember the crate's Y so it stays grounded
        _crateGroundY = target.transform.position.y;

        _heldObject.OnGrab();
    }

    private void Release()
    {
        if (_heldObject == null) return;

        _heldObject.OnRelease();
        _heldObject = null;

        if (_controller != null) _controller.SpeedMultiplier = 1f;
    }

    /// <summary>
    /// Force-release the carried object (e.g. if it's destroyed or the player enters
    /// a state where carrying isn't allowed). Called externally.
    /// </summary>
    public void ForceRelease()
    {
        if (_heldObject != null) _heldObject.OnRelease();
        _heldObject = null;
        if (_controller != null) _controller.SpeedMultiplier = 1f;
    }

    private static Vector3 SnapToCardinal(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return Vector3.right;

        float absX = Mathf.Abs(dir.x);
        float absZ = Mathf.Abs(dir.z);

        if (absX >= absZ)
            return new Vector3(Mathf.Sign(dir.x), 0f, 0f);
        else
            return new Vector3(0f, 0f, Mathf.Sign(dir.z));
    }

    private static float GetExtentsAlong(Grabbable target, Vector3 axis)
    {
        Collider col = target.GetComponent<Collider>();
        if (col != null)
        {
            if (Mathf.Abs(axis.x) > 0.5f) return col.bounds.extents.x;
            if (Mathf.Abs(axis.z) > 0.5f) return col.bounds.extents.z;
        }
        return 0.5f;
    }
}
