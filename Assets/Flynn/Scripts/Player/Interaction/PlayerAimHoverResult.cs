using UnityEngine;

/// <summary>
/// Single hover result produced by <see cref="PlayerMouseAimer"/> each frame.
/// Replaces the scattered HoveredXxx properties with one struct that all
/// consumers read from. Priority order for interaction:
/// WorldItem → RopeAnchor → RopePullable → ScanTarget → ResourceNode → Ground.
/// </summary>
public struct PlayerAimHoverResult
{
    /// <summary>True when the cursor is pointing at anything interactable or on the ground plane.</summary>
    public bool HasTarget;

    /// <summary>World-space point the cursor is pointing at (ground plane or hit surface).</summary>
    public Vector3 WorldPoint;

    // ── Interactable targets (null when not hovered) ────────────────────────

    /// <summary>Non-auto-collect world item (e.g. wrench) under cursor, range-gated.</summary>
    public WorldItem WorldItem;

    /// <summary>Resource node under cursor within interaction range.</summary>
    public ResourceNode ResourceNode;

    /// <summary>Resource node under cursor within melee range.</summary>
    public ResourceNode MeleeResource;

    /// <summary>Rope anchor under cursor (range-gated by lasso controller).</summary>
    public RopeAnchor RopeAnchor;

    /// <summary>Rope pullable under cursor (range-gated by lasso controller).</summary>
    public RopePullable RopePullable;

    /// <summary>Scan target under cursor (future).</summary>
    public ScanTarget ScanTarget;

    /// <summary>Grabbable object under cursor within interaction range.</summary>
    public Grabbable Grabbable;

    /// <summary>Generic interaction-prompt provider under cursor.</summary>
    public IInteractionPromptProvider Interactable;

    /// <summary>Hit point on the grapple target surface.</summary>
    public Vector3 GrapplePoint;

    // ── Range flags ────────────────────────────────────────────────────────

    /// <summary>True when the hovered target is within interaction range.</summary>
    public bool InInteractionRange;

    /// <summary>True when the hovered target is within melee range.</summary>
    public bool InMeleeRange;

    /// <summary>Animator AttackIndex for the tool under cursor (1-4, default 4 = wrench/air).</summary>
    public int SwingAnimIndex;
}
