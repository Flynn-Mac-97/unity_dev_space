using UnityEngine;

/// <summary>
/// Designer-tunable values for the wrench rope/lasso (winch grapple). One asset lives
/// in Configs/Player/. Referenced by RopeLassoController so all grapple feel is
/// data-driven in one place. The lasso reels the player toward a higher/level anchor,
/// or reels a movable object below up toward the player.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Rope Lasso Config", fileName = "RopeLassoConfig")]
public class RopeLassoConfig : ScriptableObject
{
    [Header("Charge (hold Q)")]
    [Tooltip("Seconds of hold to reach a 'ready' charge (used for reticle/feedback). The lasso fires on release regardless of charge.")]
    public float chargeTime = 0.3f;

    [Header("Range (world units; ~2 units per tile)")]
    [Tooltip("Minimum reach. A target nearer than this still fires.")]
    public float minRange = 2f;
    [Tooltip("Maximum reach. Targets beyond this are ignored (~2-3 tiles).")]
    public float maxRange = 6f;

    [Header("Winch")]
    [Tooltip("Acceleration applied toward the target each physics step while reeling.")]
    public float pullForce = 45f;
    [Tooltip("Speed cap on the reeled body so the winch can't run away (units/sec).")]
    public float maxPullSpeed = 12f;
    [Tooltip("Distance to the target at which the winch ends.")]
    public float stopRadius = 0.8f;
    [Tooltip("Safety cutoff: max seconds a single winch can run.")]
    public float pullTimeout = 2f;

    [Header("Self-winch")]
    [Tooltip("SteeringControl fed to the character controller while reeling self, so movement input doesn't fight the pull. Low = committed to the reel.")]
    [Range(0f, 1f)] public float steeringDuringPull = 0.1f;
}
