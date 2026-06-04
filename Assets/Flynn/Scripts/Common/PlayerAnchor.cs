using UnityEngine;

/// <summary>
/// Runtime handle to the player's Transform. <see cref="PlayerAnchorRegistrar"/> on the
/// player sets this on enable. Lets runtime-spawned objects (dropped items flying to the
/// player) locate the player through an Inspector-assigned SO reference instead of
/// <c>FindObjectOfType</c>/<c>GameObject.Find</c> (both banned by the Flynn Architect rules).
///
/// Never serialise a scene reference into this asset — <see cref="Current"/> is non-serialized
/// and only ever set at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Player Anchor", fileName = "PlayerAnchor")]
public class PlayerAnchor : ScriptableObject
{
    /// <summary>The live player Transform, or null when no player is registered.</summary>
    public Transform Current { get; private set; }

    public bool HasPlayer => Current != null;

    public void Set(Transform t) => Current = t;

    /// <summary>Clear only if <paramref name="t"/> is the currently registered transform.</summary>
    public void Clear(Transform t) { if (Current == t) Current = null; }
}
