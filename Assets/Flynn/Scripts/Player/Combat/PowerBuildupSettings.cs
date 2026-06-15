using UnityEngine;

/// <summary>
/// Configurable power buildup system. Designers tune charge timing, the "perfect zone"
/// window, and how charge scales battery cost and damage for swings and throws.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Player/Power Buildup Settings", fileName = "PowerBuildupSettings")]
public class PowerBuildupSettings : ScriptableObject
{
    [Header("Charge Timing")]
    [Tooltip("Seconds to fill the bar from 0 to 1.")]
    public float maxChargeTime = 2.0f;

    [Tooltip("Optional: rate at which charge decays per second after the bar is full (0 = no decay).")]
    public float decayRate = 0f;

    [Header("Perfect Zone")]
    [Tooltip("Charge fraction where the perfect zone starts (right side of bar).")]
    [Range(0f, 1f)] public float perfectZoneStart = 0.75f;
    [Tooltip("Charge fraction where the perfect zone ends.")]
    [Range(0f, 1f)] public float perfectZoneEnd = 0.90f;

    [Header("Swing Battery Cost (scaled by charge)")]
    public float lightSwingBatteryCost = 2f;
    public float heavySwingBatteryCost = 8f;

    [Header("Throw Battery Cost (scaled by charge)")]
    public float lightThrowBatteryCost = 5f;
    public float heavyThrowBatteryCost = 12f;

    [Header("Swing Damage (scaled by charge)")]
    [Min(1)] public int lightSwingDamage = 1;
    [Min(1)] public int heavySwingDamage = 3;

    [Header("Throw Damage (scaled by charge)")]
    [Min(1)] public int lightThrowDamage = 1;
    [Min(1)] public int heavyThrowDamage = 3;

    [Header("Swing Animation")]
    [Min(0.1f)] public float lightSwingAnimSpeed = 1.0f;
    [Min(0.1f)] public float heavySwingAnimSpeed = 2.5f;
}
