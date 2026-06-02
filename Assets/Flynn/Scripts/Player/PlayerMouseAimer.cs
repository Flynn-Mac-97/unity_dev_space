using UnityEngine;

/// <summary>
/// Single-responsibility component that tracks two things:
///   1. WorldAimPoint — where the mouse cursor lands in world space (plane at
///      player Y, with Physics fallback for ground hits).
///   2. HoveredResource — the ResourceNode the mouse ray hits, provided the
///      player is within _interactionRange of that node. No trigger volumes
///      required — a direct Physics raycast drives detection.
///
/// Other systems (wrench controllers, future aim-assist code) read the
/// public properties; this component only ever reads — it writes nothing.
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class PlayerMouseAimer : MonoBehaviour
{
    [Tooltip("Layers that the mouse-to-world raycast should hit (ground, terrain, etc.).")]
    [SerializeField] private LayerMask _aimLayers = ~0;

    [Tooltip("Layers checked when raycasting for ResourceNodes. Should include whatever layer your resource prefabs live on. Triggers are always excluded.")]
    [SerializeField] private LayerMask _resourceLayers = ~0;

    [SerializeField] private float _maxAimDistance = 30f;

    [Tooltip("Max distance from the player to a resource node for it to count as interactable.")]
    [SerializeField] private float _interactionRange = 3f;

    [Tooltip("Distance from WorldAimPoint within which a WorldItemPickup counts as hovered.")]
    [SerializeField] private float _hoverRadius = 1.5f;

    private Camera _cam;

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>World-space position the mouse cursor is pointing at.</summary>
    public Vector3 WorldAimPoint { get; private set; }

    /// <summary>
    /// The ResourceNode the mouse ray is pointing at, or null.
    /// Only set when the hit node is within <see cref="_interactionRange"/> of the player.
    /// </summary>
    public ResourceNode HoveredResource { get; private set; }

    /// <summary>
    /// The WorldItemPickup the player is aiming at, or null. Set when the pickup
    /// is within the player's proximity trigger AND near WorldAimPoint.
    /// </summary>
    public WorldItemPickup HoveredPickup { get; private set; }

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        UpdateAimPoint();
        UpdateHoveredResource();
        UpdateHoveredPickup();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void UpdateAimPoint()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, _maxAimDistance, _aimLayers, QueryTriggerInteraction.Ignore))
        {
            WorldAimPoint = hit.point;
        }
        else
        {
            // Fallback: intersect the horizontal plane at this object's Y.
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (plane.Raycast(ray, out float dist))
                WorldAimPoint = ray.GetPoint(Mathf.Min(dist, _maxAimDistance));
        }
    }

    private void UpdateHoveredResource()
    {
        if (_cam == null) { HoveredResource = null; return; }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, _maxAimDistance, _resourceLayers, QueryTriggerInteraction.Ignore))
        {
            HoveredResource = null;
            return;
        }

        // Walk up the hierarchy — the collider may be on a child of the ResourceNode root.
        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node == null)
        {
            HoveredResource = null;
            return;
        }

        // Gate on player distance so out-of-reach resources are never highlighted.
        float dist = Vector3.Distance(transform.position, node.transform.position);
        HoveredResource = dist <= _interactionRange ? node : null;
    }

    private void UpdateHoveredPickup()
    {
        WorldItemPickup best     = null;
        float           bestDist = float.MaxValue;

        foreach (WorldItemPickup pickup in WorldItemPickup.NearbyPickups)
        {
            if (pickup == null) continue;
            float dist = Vector3.Distance(pickup.transform.position, WorldAimPoint);
            if (dist < _hoverRadius && dist < bestDist)
            {
                bestDist = dist;
                best     = pickup;
            }
        }

        HoveredPickup = best;
    }
}
