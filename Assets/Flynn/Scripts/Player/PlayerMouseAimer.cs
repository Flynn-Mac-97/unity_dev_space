using UnityEngine;

/// <summary>
/// Single-responsibility component that owns all mouse → world target detection. Every interaction
/// reads what's under the cursor from here; nothing keeps its own proximity registry.
///
/// It no longer casts its own rays: <see cref="MousePointer"/> does ONE cursor raycast per frame
/// (against its combined world mask) and this component derives every hovered target from that one
/// shared hit. The object under the cursor is resolved once; each concern asks it for the component
/// it cares about and applies its own range gate. Consequence of the single union cast: the closest
/// object under the cursor wins across all layers (uniform occlusion) — there are no per-concern
/// masks that can see a target through something in front of it.
///
/// Other systems read the public properties; this component only ever reads — it writes nothing.
/// Requires a <see cref="MousePointer"/> in the scene whose mask covers ground + resources + pickups
/// + grapple + melee surfaces (the union of what used to be per-concern masks here).
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class PlayerMouseAimer : MonoBehaviour
{
    [SerializeField] private float _maxAimDistance = 30f;

    [Tooltip("Max distance from the player to a resource or pickup for it to count as interactable.")]
    [SerializeField] private float _interactionRange = 3f;

    [Tooltip("Max distance from the player to a melee surface for a wrench swing to register its tool/hit.")]
    [SerializeField] private float _meleeRange = 2.5f;

    private MousePointer _pointer;

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

    private void Update()
    {
        if (_pointer == null) _pointer = MousePointer.Instance;
        if (_pointer == null) return; // no pointer in scene → nothing to aim with this frame

        GameObject hovered = _pointer.HoverObject;
        bool hasHit = _pointer.HasHit;
        Vector3 point = _pointer.WorldPoint;

        UpdateAimPoint(hasHit, point);
        UpdateHoveredResource(hovered);
        UpdateHoveredPickup(hovered);
        UpdateHoveredKeyPickup(hovered);
        UpdateHoveredGrapple(hovered, point);
        UpdateMeleeTarget(hovered, hasHit, point);
        UpdateHoveredInteractable(hovered, hasHit, point);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void UpdateAimPoint(bool hasHit, Vector3 point)
    {
        if (hasHit)
        {
            WorldAimPoint = point;
            return;
        }
        // Miss: intersect the horizontal plane at this object's Y so aim still resolves over open ground.
        var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (plane.Raycast(_pointer.WorldRay, out float dist))
            WorldAimPoint = _pointer.WorldRay.GetPoint(Mathf.Min(dist, _maxAimDistance));
    }

    private void UpdateHoveredResource(GameObject hovered)
    {
        ResourceNode node = Find<ResourceNode>(hovered);
        HoveredResource = node != null && WithinInteractionRange(node.transform.position) ? node : null;
    }

    private void UpdateHoveredPickup(GameObject hovered)
    {
        WorldItemPickup pickup = Find<WorldItemPickup>(hovered);
        HoveredPickup = pickup != null && WithinInteractionRange(pickup.transform.position) ? pickup : null;
    }

    private void UpdateHoveredKeyPickup(GameObject hovered)
    {
        WorldItem item = Find<WorldItem>(hovered);
        // Only key-pickup items (e.g. the wrench) surface here; auto-collect items magnet themselves.
        bool needsKey = item != null && item.Item != null && !item.Item.AutoCollects;
        HoveredKeyPickup = needsKey && WithinInteractionRange(item.transform.position) ? item : null;
    }

    private void UpdateHoveredGrapple(GameObject hovered, Vector3 point)
    {
        // Whichever marker the hovered object carries decides the kind; range is left to the
        // controller (RopeLassoConfig owns it). Anchor and pullable are mutually exclusive.
        RopeAnchor anchor = Find<RopeAnchor>(hovered);
        if (anchor != null)
        {
            HoveredAnchor = anchor;
            HoveredPullable = null;
            HoveredGrapplePoint = point;
            return;
        }

        RopePullable pullable = Find<RopePullable>(hovered);
        HoveredAnchor = null;
        HoveredPullable = pullable;
        if (pullable != null) HoveredGrapplePoint = point;
    }

    private void UpdateMeleeTarget(GameObject hovered, bool hasHit, Vector3 point)
    {
        // ResourceNode wins (it carries damage + its own tool); otherwise a HittableSurface decides
        // the tool; nothing in reach = wrench (air).
        MeleeResource = null;
        SwingAnimIndex = 4;
        if (!hasHit || hovered == null) return;
        if (Vector3.Distance(transform.position, point) > _meleeRange) return;

        ResourceNode node = Find<ResourceNode>(hovered);
        if (node != null)
        {
            MeleeResource = node;
            SwingAnimIndex = node.AttackAnimIndex;
            return;
        }

        HittableSurface surface = Find<HittableSurface>(hovered);
        if (surface != null)
            SwingAnimIndex = surface.AttackAnimIndex;
    }

    private void UpdateHoveredInteractable(GameObject hovered, bool hasHit, Vector3 point)
    {
        HoveredInteractable = null;

        // 1) Generic interaction target within reach — pickups, NPCs, inspectables, etc.
        if (hasHit && hovered != null && Vector3.Distance(transform.position, point) <= _interactionRange)
        {
            var provider = hovered.GetComponentInParent<IInteractionPromptProvider>();
            if (provider != null) { HoveredInteractable = provider; return; }
        }

        // 2) Fall back to the rope-lasso targets (their reach is owned by the lasso controller,
        //    so they prompt even past the generic interaction range).
        if (HoveredAnchor is IInteractionPromptProvider anchor)        HoveredInteractable = anchor;
        else if (HoveredPullable is IInteractionPromptProvider pull)   HoveredInteractable = pull;
    }

    /// <summary>Component of type T on the hovered object or its parents, or null.</summary>
    private static T Find<T>(GameObject hovered) where T : Component
        => hovered != null ? hovered.GetComponentInParent<T>() : null;

    private bool WithinInteractionRange(Vector3 worldPos)
        => Vector3.Distance(transform.position, worldPos) <= _interactionRange;
}
