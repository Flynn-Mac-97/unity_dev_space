using UnityEngine;

/// <summary>
/// Immutable payload for a single player "hit" event (a swing/strike landing). Carried by
/// <see cref="PlayerHitChannel"/> from the player to the <see cref="ResourceManager"/>, which
/// re-broadcasts it to registered resources. Deliberately generic: it describes WHAT happened,
/// not WHO should react — the resource layer decides that later.
/// </summary>
public readonly struct PlayerHitInfo
{
    /// <summary>World-space point the hit landed at.</summary>
    public readonly Vector3 Point;

    /// <summary>The struck GameObject, if the player resolved one. May be null (whiffed swing).</summary>
    public readonly GameObject Target;

    /// <summary>Generic strike strength. Unused for now; here so damage can be wired in later.</summary>
    public readonly int Power;

    public PlayerHitInfo(Vector3 point, GameObject target, int power)
    {
        Point = point;
        Target = target;
        Power = power;
    }
}
