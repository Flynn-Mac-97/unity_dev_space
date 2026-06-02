using UnityEngine;

/// <summary>
/// Designer-tunable values for the wrench multitool's swing + throw abilities.
/// One asset lives in Configs/Player/. Referenced by the wrench controllers and
/// the aim reticle so all wrench feel is data-driven in one place.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Wrench Config", fileName = "WrenchConfig")]
public class WrenchConfig : ScriptableObject
{
    [Header("Swing (Left Click)")]
    [Tooltip("Seconds of hold to reach a full-power (heavy) swing.")]
    public float swingChargeTime = 0.6f;
    [Tooltip("Charge fraction (0-1) at/above which a swing counts as heavy.")]
    [Range(0f, 1f)] public float heavySwingThreshold = 0.6f;
    public float lightSwingCooldown = 0.45f;
    public float heavySwingCooldown = 0.8f;

    [Header("Harvest Damage")]
    [Tooltip("Damage points applied to a ResourceNode on a light swing.")]
    [Min(1)] public int lightSwingDamage = 1;
    [Tooltip("Damage points applied to a ResourceNode on a heavy swing.")]
    [Min(1)] public int heavySwingDamage = 2;

    [Header("Throw (Right Click)")]
    [Tooltip("Seconds of hold to reach max throw distance.")]
    public float throwChargeTime = 0.8f;
    public float minThrowDistance = 3f;
    public float maxThrowDistance = 9f;
    [Tooltip("Outbound travel speed (units/sec).")]
    public float throwSpeed = 12f;
    [Tooltip("Visual spin speed of the thrown wrench (deg/sec).")]
    public float spinSpeed = 720f;
    [Tooltip("Seconds the wrench hovers at the apex before boomeranging back.")]
    public float returnDelay = 0.4f;
    [Tooltip("Homing speed on the return leg (units/sec).")]
    public float returnSpeed = 14f;
    [Tooltip("Distance to the catch point at which a returning wrench is caught.")]
    public float catchRadius = 0.6f;
}
