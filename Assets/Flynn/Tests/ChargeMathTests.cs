using NUnit.Framework;
using Flynn.Player.Combat;

namespace Flynn.Tests
{
    public class ChargeMathTests
    {
        private const float Delay = 1f;   // holdToChargeDelay
        private const float Max = 2f;     // maxChargeTime
        private const float ZoneStart = 0.75f;
        private const float ZoneEnd = 0.9f;

        // ── Normalized ────────────────────────────────────────────────────

        [Test]
        public void Normalized_BeforeDelay_IsZero()
        {
            Assert.AreEqual(0f, ChargeMath.Normalized(0.5f, Delay, Max));
            Assert.AreEqual(0f, ChargeMath.Normalized(0.99f, Delay, Max));
        }

        [Test]
        public void Normalized_AtDelay_IsZero()
        {
            Assert.AreEqual(0f, ChargeMath.Normalized(1f, Delay, Max));
        }

        [Test]
        public void Normalized_RampsLinearly()
        {
            Assert.AreEqual(0.5f, ChargeMath.Normalized(2f, Delay, Max), 1e-4f);
            Assert.AreEqual(1f, ChargeMath.Normalized(3f, Delay, Max), 1e-4f);
        }

        [Test]
        public void Normalized_ClampsAtOne()
        {
            Assert.AreEqual(1f, ChargeMath.Normalized(10f, Delay, Max));
        }

        [Test]
        public void Normalized_ZeroMaxTime_DoesNotDivide()
        {
            Assert.AreEqual(0f, ChargeMath.Normalized(0.5f, Delay, 0f));
            Assert.AreEqual(1f, ChargeMath.Normalized(1.5f, Delay, 0f));
        }

        // ── IsCharging ────────────────────────────────────────────────────

        [Test]
        public void IsCharging_RespectsDelay()
        {
            Assert.IsFalse(ChargeMath.IsCharging(0.9f, Delay));
            Assert.IsTrue(ChargeMath.IsCharging(1f, Delay));
        }

        // ── InPerfectZone ─────────────────────────────────────────────────

        [Test]
        public void PerfectZone_BoundsInclusive()
        {
            Assert.IsTrue(ChargeMath.InPerfectZone(ZoneStart, ZoneStart, ZoneEnd));
            Assert.IsTrue(ChargeMath.InPerfectZone(ZoneEnd, ZoneStart, ZoneEnd));
            Assert.IsTrue(ChargeMath.InPerfectZone(0.8f, ZoneStart, ZoneEnd));
        }

        [Test]
        public void PerfectZone_OutsideIsFalse()
        {
            Assert.IsFalse(ChargeMath.InPerfectZone(0.74f, ZoneStart, ZoneEnd));
            Assert.IsFalse(ChargeMath.InPerfectZone(0.91f, ZoneStart, ZoneEnd));
            Assert.IsFalse(ChargeMath.InPerfectZone(1f, ZoneStart, ZoneEnd));
        }

        // ── Damage ────────────────────────────────────────────────────────

        [Test]
        public void Damage_QuickSwing_IsLight()
        {
            Assert.AreEqual(1, ChargeMath.Damage(0f, 1, 3, false, 1.5f));
        }

        [Test]
        public void Damage_FullCharge_IsHeavy()
        {
            Assert.AreEqual(3, ChargeMath.Damage(1f, 1, 3, false, 1.5f));
        }

        [Test]
        public void Damage_MidCharge_Lerps()
        {
            Assert.AreEqual(2, ChargeMath.Damage(0.5f, 1, 3, false, 1.5f));
        }

        [Test]
        public void Damage_Perfect_AppliesMultiplier()
        {
            // At sweetspot ~0.8: lerp(1,3,0.8)=2.6 → *1.5 = 3.9 → 4
            Assert.AreEqual(4, ChargeMath.Damage(0.8f, 1, 3, true, 1.5f));
        }

        [Test]
        public void Damage_NeverBelowOne()
        {
            Assert.AreEqual(1, ChargeMath.Damage(0f, 0, 0, false, 1f));
        }

        // ── Cost ──────────────────────────────────────────────────────────

        [Test]
        public void Cost_LerpsLightToHeavy()
        {
            Assert.AreEqual(2f, ChargeMath.Cost(0f, 2f, 8f), 1e-4f);
            Assert.AreEqual(8f, ChargeMath.Cost(1f, 2f, 8f), 1e-4f);
            Assert.AreEqual(5f, ChargeMath.Cost(0.5f, 2f, 8f), 1e-4f);
        }
    }
}
