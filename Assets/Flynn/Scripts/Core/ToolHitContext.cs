using UnityEngine;

/// <summary>
/// Unified hit context for all tool-based interactions. Carries enough information
/// for ResourceNode to validate the hit, apply damage, and for FeedbackManager/AudioManager
/// to play appropriate effects. Used by both melee swings and thrown wrench impacts.
/// </summary>
public readonly struct ToolHitContext
{
    /// <summary>The GameObject that initiated the hit (the player or the ThrownWrench).</summary>
    public readonly GameObject Source;

    /// <summary>Which tool produced this hit.</summary>
    public readonly ToolType ToolType;

    /// <summary>Swing or Throw — determines damage calculation and feedback.</summary>
    public readonly ToolActionType Action;

    /// <summary>Raw damage points before tool-effectiveness multiplier is applied.</summary>
    public readonly int Damage;

    /// <summary>World-space point where the hit landed.</summary>
    public readonly Vector3 HitPoint;

    /// <summary>Approximate direction from source to hit point (for knockback / VFX).</summary>
    public readonly Vector3 HitDirection;

    /// <summary>Charge level 0-1 at time of impact (0 for instant swing, 0-1 for throw).</summary>
    public readonly float Charge;

    public ToolHitContext(
        GameObject source,
        ToolType toolType,
        ToolActionType action,
        int damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        float charge = 0f)
    {
        Source = source;
        ToolType = toolType;
        Action = action;
        Damage = damage;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        Charge = charge;
    }

    /// <summary>Helper to convert an ItemType to ToolType (they share the same value space).</summary>
    public static ToolType FromItemType(ItemType itemType) => (ToolType)(int)itemType;
}
