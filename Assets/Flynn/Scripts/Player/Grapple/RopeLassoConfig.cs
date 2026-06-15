using UnityEngine;

/// <summary>
/// Designer-tunable values for the wrench grapple (swing physics). One asset lives
/// in Configs/Player/. Referenced by RopeLassoController so all grapple feel is
/// data-driven in one place.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Rope Lasso Config", fileName = "RopeLassoConfig")]
public class RopeLassoConfig : ScriptableObject
{
    [Header("Charge (hold Q)")]
    [Tooltip("Seconds of hold to reach a 'ready' charge (reticle feedback). The lasso fires on release regardless of charge.")]
    public float chargeTime = 0.15f;

    [Header("Range (world units; ~2 units per tile)")]
    [Tooltip("Minimum reach. A target nearer than this still fires.")]
    public float minRange = 2f;
    [Tooltip("Maximum reach. Targets beyond this are ignored.")]
    public float maxRange = 10f;

    [Header("Hook travel")]
    [Tooltip("Seconds for the hook projectile to travel from hand to target.")]
    public float hookTravelTime = 0.12f;

    [Header("Swing physics")]
    [Tooltip("Tangential acceleration applied from WASD input while swinging (pumping the swing).")]
    public float swingForce = 30f;
    [Tooltip("Maximum swing velocity (units/sec). Prevents runaway speed on long swings.")]
    public float maxSwingSpeed = 18f;
    [Tooltip("Minimum rope length — the player can shorten the rope to this via scroll wheel.")]
    public float minRopeLength = 1.5f;
    [Tooltip("Maximum rope length — caps the rope at connection time and limits scroll-wheel extension.")]
    public float maxRopeLength = 12f;
    [Tooltip("How fast the scroll wheel adjusts rope length (units/sec per scroll notch).")]
    public float ropeAdjustSpeed = 4f;
    [Tooltip("Impulse toward the anchor when the hook first connects — gives a satisfying yank (XZ only).")]
    public float snapForce = 5f;

    [Header("Self-pull (latch an anchor, dash horizontally to it)")]
    [Tooltip("Horizontal acceleration applied to the player each step while self-pulling toward an anchor.")]
    public float selfPullForce = 60f;
    [Tooltip("Max horizontal speed the self-pull can reach (units/sec).")]
    public float maxSelfPullSpeed = 16f;
    [Tooltip("Horizontal distance to the anchor's X at which the self-pull ends.")]
    public float selfPullStopRadius = 0.6f;

    [Header("Winch (pulling objects)")]
    [Tooltip("Acceleration applied toward the target each physics step while reeling an object.")]
    public float pullForce = 55f;
    [Tooltip("Speed cap on the reeled body so the winch can't run away (units/sec).")]
    public float maxPullSpeed = 14f;
    [Tooltip("Distance to the target at which the winch ends.")]
    public float stopRadius = 0.8f;
    [Tooltip("Safety cutoff: max seconds a single grapple can run (applies to both swing and pull).")]
    public float pullTimeout = 5f;

    [Header("Self-winch (legacy)")]
    [Tooltip("SteeringControl fed to the character controller during pull — kept for backward compat. Swing mode hard-sets steering to 0.")]
    [Range(0f, 1f)] public float steeringDuringPull = 0.1f;
}
