using UnityEngine;

/// <summary>
/// The visual/animation surface the player's combat &amp; interaction controllers drive,
/// decoupled from how the body is actually rendered. Implemented by both the legacy
/// sprite-sheet driver (<see cref="FlynnAnimationDriver"/>) and the procedural cube-rig
/// driver (<see cref="ProceduralCharacterDriver"/>) so the same wrench/throw/rope/reticle
/// logic works against either body without edits.
///
/// Controllers resolve it via <c>GetComponent&lt;IPlayerVisual&gt;()</c> in Awake.
/// </summary>
public interface IPlayerVisual
{
    /// <summary>World-space "hand" / chest origin for thrown projectiles and the rope end.</summary>
    Vector3 VisualCenter { get; }

    /// <summary>VisualCenter projected to the character's foot Y (ground-plane indicators).</summary>
    Vector3 VisualGroundCenter { get; }

    /// <summary>True while an attack/swing is playing (gates re-triggering).</summary>
    bool IsAttacking { get; }

    /// <summary>Fire the attack pose for the given tool index (1=pick 2=axe 3=hammer 4=wrench).</summary>
    void TriggerAttack(int animIndex);

    /// <summary>Enter the windup hold for the given tool index.</summary>
    void BeginChargePose(int animIndex);

    /// <summary>Scrub the windup pose with charge fraction 0→1 (also live-swaps the tool).</summary>
    void UpdateChargePose(int animIndex, float charge01);

    /// <summary>Release the swing at the given playback speed (higher = faster follow-through).</summary>
    void ReleaseSwing(int animIndex, float animSpeed);

    /// <summary>Enter the looping windup animation (first half of swing) while charging.</summary>
    void BeginSwingWindup();

    /// <summary>Cancel the windup animation and return to idle.</summary>
    void CancelSwingWindup();

    /// <summary>Fire the swing directly without windup (quick tap).</summary>
    void TriggerSwing();

    /// <summary>Abort a windup and return to neutral.</summary>
    void CancelSwing();

    /// <summary>Orient the body toward a world-space XZ direction. Ignores Y; no-op on zero.</summary>
    void FaceWorldDirection(Vector3 worldDir);
}
