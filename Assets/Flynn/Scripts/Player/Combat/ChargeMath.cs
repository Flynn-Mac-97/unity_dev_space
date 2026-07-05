using UnityEngine;

namespace Flynn.Player.Combat
{
    /// <summary>
    /// Pure charge/sweetspot math for the wrench buildup minigame.
    /// No Unity state — fully covered by EditMode tests.
    /// </summary>
    public static class ChargeMath
    {
        /// <summary>
        /// Convert raw button-held seconds into a 0-1 charge level.
        /// Below <paramref name="holdToChargeDelay"/> the buildup has not started (returns 0).
        /// After the delay, charge ramps linearly to 1 over <paramref name="maxChargeTime"/>.
        /// </summary>
        public static float Normalized(float heldSeconds, float holdToChargeDelay, float maxChargeTime)
        {
            if (maxChargeTime <= 0f) return heldSeconds > holdToChargeDelay ? 1f : 0f;
            return Mathf.Clamp01((heldSeconds - holdToChargeDelay) / maxChargeTime);
        }

        /// <summary>True when the buildup has actually begun (past the hold delay).</summary>
        public static bool IsCharging(float heldSeconds, float holdToChargeDelay)
            => heldSeconds >= holdToChargeDelay;

        /// <summary>Sweetspot test on a normalized charge level.</summary>
        public static bool InPerfectZone(float normalized, float zoneStart, float zoneEnd)
            => normalized >= zoneStart && normalized <= zoneEnd;

        /// <summary>
        /// Damage for a release: lerp light→heavy by charge, perfect bonus on top.
        /// Quick swings pass normalized 0 → light damage.
        /// </summary>
        public static int Damage(float normalized, int light, int heavy, bool isPerfect, float perfectMultiplier)
        {
            float dmg = Mathf.Lerp(light, heavy, Mathf.Clamp01(normalized));
            if (isPerfect) dmg *= perfectMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        /// <summary>Battery cost for a release: lerp light→heavy by charge.</summary>
        public static float Cost(float normalized, float light, float heavy)
            => Mathf.Lerp(light, heavy, Mathf.Clamp01(normalized));
    }
}
