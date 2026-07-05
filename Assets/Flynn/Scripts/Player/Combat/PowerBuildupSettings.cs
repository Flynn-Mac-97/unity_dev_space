using UnityEngine;

namespace Flynn.Player.Combat
{
    /// <summary>
    /// Designer tuning for the wrench charge/throw system. One asset drives
    /// WrenchController, ThrownWrench and WrenchChargeFX.
    /// Field names must stay in sync with Configs/Player/PowerBuildupSettings.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Power Buildup Settings", fileName = "PowerBuildupSettings")]
    public class PowerBuildupSettings : ScriptableObject
    {
        [Header("Charge")]
        [Tooltip("Seconds from buildup start to full charge.")]
        public float maxChargeTime = 2f;
        [Tooltip("Charge lost per second when decay applies (0 = hold forever).")]
        public float decayRate = 0f;
        [Tooltip("Sweetspot window start, normalized 0-1. Must match PlayerHudPanel.")]
        public float perfectZoneStart = 0.75f;
        [Tooltip("Sweetspot window end, normalized 0-1.")]
        public float perfectZoneEnd = 0.9f;

        [Header("Battery Costs")]
        public float lightSwingBatteryCost = 2f;
        public float heavySwingBatteryCost = 8f;
        public float lightThrowBatteryCost = 5f;
        public float heavyThrowBatteryCost = 12f;

        [Header("Damage")]
        public int lightSwingDamage = 1;
        public int heavySwingDamage = 3;
        public int lightThrowDamage = 1;
        public int heavyThrowDamage = 3;

        [Header("Swing Animation")]
        public float lightSwingAnimSpeed = 1f;
        public float heavySwingAnimSpeed = 2.5f;

        [Header("Timing Minigame")]
        [Tooltip("Damage multiplier when released inside the sweetspot.")]
        public float perfectMultiplier = 1.5f;
        [Tooltip("Hold time before a click becomes a charge buildup.")]
        public float holdToChargeDelay = 0.3f;
        [Tooltip("Presses shorter than this are a quick swing.")]
        public float quickClickWindow = 0.25f;

        [Header("Movement While Charging")]
        [Tooltip("Speed multiplier during swing buildup.")]
        public float swingChargeSlow = 0.5f;
        [Tooltip("Speed multiplier during throw aim.")]
        public float throwAimSlow = 0.3f;

        [Header("Throw")]
        [Tooltip("Projectile speed in units/second.")]
        public float throwSpeed = 3f;
        [Tooltip("Playback speed of the shared throw clip for both verbs (0.75 = slower, readable).")]
        public float throwAnimSpeed = 0.75f;
        [Tooltip("Max flight distance at zero charge — a quick flick barely leaves the player.")]
        public float throwRangeLight = 1.5f;
        [Tooltip("Max flight distance at full charge.")]
        public float throwRangeHeavy = 5f;
        [Tooltip("Velocity retained after bouncing off a hit.")]
        public float reboundDamping = 0.3f;
        [Tooltip("Seconds the wrench rests on the ground before returning.")]
        public float restDuration = 0.5f;
        [Tooltip("Homing acceleration on the return flight, units/s^2.")]
        public float returnAccel = 20f;
    }
}
