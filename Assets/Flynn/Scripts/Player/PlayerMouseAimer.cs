using UnityEngine;

/// <summary>
/// Single-responsibility component that owns all mouse → world target detection.
/// Every interaction reads what's under the cursor from here; nothing keeps its own
/// proximity registry. One Physics raycast per concern, identified by component:
///   1. WorldAimPoint — where the mouse cursor lands in world space (plane at
///      player Y, with Physics fallback for ground hits).
///   2. HoveredResource — the ResourceNode under the cursor, gated by _interactionRange.
///   3. HoveredPickup — the WorldItemPickup under the cursor, gated by _interactionRange.
///   4. HoveredAnchor / HoveredPullable / HoveredGrapplePoint — the rope-lasso target
///      under the cursor and the exact surface point hit. NOT range-gated here: range is
///      a designer concern owned by RopeLassoConfig (min/maxRange), so the controller
///      applies it. Anchor and pullable are mutually exclusive (one raycast).
///
/// Other systems read the public properties; this component only ever reads — it writes nothing.
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class PlayerMouseAimer : MonoBehaviour
{
    [Tooltip("Layers that the mouse-to-world raycast should hit (ground, terrain, etc.).")]
    [SerializeField] private LayerMask _aimLayers = ~0;

    [Tooltip("Layers checked when raycasting for ResourceNodes. Should include whatever layer your resource prefabs live on. Triggers are always excluded.")]
    [SerializeField] private LayerMask _resourceLayers = ~0;

    [Tooltip("Layers checked when raycasting for WorldItemPickups. Triggers are always excluded.")]
    [SerializeField] private LayerMask _pickupLayers = ~0;

    [Tooltip("Layers checked when raycasting for rope-lasso targets (RopeAnchor / RopePullable). Triggers are always excluded.")]
    [SerializeField] private LayerMask _grappleLayers = ~0;

    [Tooltip("Layers checked when raycasting for the wrench swing target (ResourceNode / HittableSurface). Triggers are always excluded.")]
    [SerializeField] private LayerMask _meleeLayers = ~0;

    [Tooltip("Layers checked for generic interaction-prompt providers (pickups, NPCs, inspectables...). Triggers excluded.")]
    [SerializeField] private LayerMask _interactLayers = ~0;

    [SerializeField] private float _maxAimDistance = 30f;

    [Tooltip("Max distance from the player to a resource or pickup for it to count as interactable.")]
    [SerializeField] private float _interactionRange = 3f;

    [Tooltip("Max distance from the player to a melee surface for a wrench swing to register its tool/hit.")]
    [SerializeField] private float _meleeRange = 2.5f;

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
    /// The WorldItemPickup the mouse ray is pointing at, or null. (Legacy; new world objects use
    /// <see cref="WorldItem"/> / <see cref="HoveredKeyPickup"/>.) Range-gated by <see cref="_interactionRange"/>.
    /// </summary>
    public WorldItemPickup HoveredPickup { get; private set; }

    /// <summary>
    /// The non-auto-collect <see cref="WorldItem"/> under the cursor (e.g. the wrench), or null.
    /// These require the pickup key — auto-collecting items magnet themselves and never appear here.
    /// Range-gated by <see cref="_interactionRange"/>.
    /// </summary>
    public WorldItem HoveredKeyPickup { get; private set; }

    /// <summary>The RopeAnchor the mouse ray is pointing at, or null. Not range-gated (the controller gates).</summary>
    public RopeAnchor HoveredAnchor { get; private set; }

    /// <summary>The RopePullable the mouse ray is pointing at, or null. Not range-gated (the controller gates).</summary>
    public RopePullable HoveredPullable { get; private set; }

    /// <summary>Exact world surface point hit on the current grapple target. Valid only when an anchor or pullable is hovered.</summary>
    public Vector3 HoveredGrapplePoint { get; private set; }

    /// <summary>
    /// Animator AttackIndex of the surface under the cursor within <see cref="_meleeRange"/>.
    /// Defaults to 4 (wrench / "air") when nothing meleeable is in reach. The wrench swing
    /// reads this to decide which tool to transform into.
    /// </summary>
    public int SwingAnimIndex { get; private set; } = 4;

    /// <summary>
    /// The ResourceNode under the cursor within melee reach, or null. Used by the swing to
    /// apply harvest damage. Non-resource surfaces (HittableSurface) drive the tool anim but
    /// leave this null (no damage).
    /// </summary>
    public ResourceNode MeleeResource { get; private set; }

    /// <summary>
    /// The interaction-prompt provider the player is currently targeting (pickup, grapple point,
    /// NPC, …), or null. Generic — the HUD reads this and renders whatever prompt it returns.
    /// </summary>
    public IInteractionPromptProvider HoveredInteractable { get; private set; }

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
        UpdateHoveredKeyPickup();
        UpdateHoveredGrapple();
        UpdateMeleeTarget();
        UpdateHoveredInteractable();
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
        ResourceNode node = RaycastForComponent<ResourceNode>(_resourceLayers, out _);
        // Gate on player distance so out-of-reach resources are never highlighted.
        HoveredResource = node != null && WithinInteractionRange(node.transform.position) ? node : null;
    }

    private void UpdateHoveredPickup()
    {
        WorldItemPickup pickup = RaycastForComponent<WorldItemPickup>(_pickupLayers, out _);
        HoveredPickup = pickup != null && WithinInteractionRange(pickup.transform.position) ? pickup : null;
    }

    private void UpdateHoveredKeyPickup()
    {
        WorldItem item = RaycastForComponent<WorldItem>(_pickupLayers, out _);
        // Only key-pickup items (e.g. the wrench) surface here; auto-collect items magnet themselves.
        bool needsKey = item != null && item.Item != null && !item.Item.AutoCollects;
        HoveredKeyPickup = needsKey && WithinInteractionRange(item.transform.position) ? item : null;
    }

    private void UpdateHoveredGrapple()
    {
        // One raycast against the grapple layers. Whichever marker the hit collider carries
        // decides the kind; range is left to the controller (RopeLassoConfig owns it).
        RopeAnchor anchor = RaycastForComponent<RopeAnchor>(_grappleLayers, out Vector3 point);
        if (anchor != null)
        {
            HoveredAnchor = anchor;
            HoveredPullable = null;
            HoveredGrapplePoint = point;
            return;
        }

        RopePullable pullable = RaycastForComponent<RopePullable>(_grappleLayers, out point);
        HoveredAnchor = null;
        HoveredPullable = pullable;
        if (pullable != null) HoveredGrapplePoint = point;
    }

    private void UpdateHoveredInteractable()
    {
        HoveredInteractable = null;

        // 1) Generic interaction raycast within reach — pickups, NPCs, inspectables, etc.
        if (_cam != null &&
            Physics.Raycast(_cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit,
                            _maxAimDistance, _interactLayers, QueryTriggerInteraction.Ignore) &&
            Vector3.Distance(transform.position, hit.point) <= _interactionRange)
        {
            var provider = hit.collider.GetComponentInParent<IInteractionPromptProvider>();
            if (provider != null) { HoveredInteractable = provider; return; }
        }

        // 2) Fall back to the rope-lasso targets (their reach is owned by the lasso controller,
        //    so they prompt even past the generic interaction range).
        if (HoveredAnchor is IInteractionPromptProvider anchor)        HoveredInteractable = anchor;
        else if (HoveredPullable is IInteractionPromptProvider pull)   HoveredInteractable = pull;
    }

    private void UpdateMeleeTarget()
    {
        // One raycast against the melee layers. ResourceNode wins (it carries damage + its own
        // tool); otherwise a HittableSurface decides the tool; nothing in reach = wrench (air).
        MeleeResource = null;
        SwingAnimIndex = 4;
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxAimDistance, _meleeLayers, QueryTriggerInteraction.Ignore))
            return;
        if (Vector3.Distance(transform.position, hit.point) > _meleeRange)
            return;

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null)
        {
            MeleeResource = node;
            SwingAnimIndex = node.AttackAnimIndex;
            return;
        }

        HittableSurface surface = hit.collider.GetComponentInParent<HittableSurface>();
        if (surface != null)
            SwingAnimIndex = surface.AttackAnimIndex;
    }

    /// <summary>
    /// Raycast the mouse cursor against <paramref name="mask"/> (triggers ignored) and return
    /// the first <typeparamref name="T"/> found on the hit collider or its parents, or null.
    /// Outputs the world-space hit point when something is hit.
    /// </summary>
    private T RaycastForComponent<T>(LayerMask mask, out Vector3 hitPoint) where T : Component
    {
        hitPoint = default;
        if (_cam == null) return null;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxAimDistance, mask, QueryTriggerInteraction.Ignore))
            return null;

        hitPoint = hit.point;
        // Walk up the hierarchy — the collider may be on a child of the marker's root.
        return hit.collider.GetComponentInParent<T>();
    }

    private bool WithinInteractionRange(Vector3 worldPos)
        => Vector3.Distance(transform.position, worldPos) <= _interactionRange;
}
