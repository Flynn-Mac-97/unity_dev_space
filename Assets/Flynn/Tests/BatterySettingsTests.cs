using NUnit.Framework;
using UnityEngine;
using Flynn.Player;

namespace Flynn.Tests
{
    [TestFixture]
    public class BatterySettingsTests
    {
        private BatterySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<BatterySettings>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Defaults_PassiveDrainEnabled()
        {
            Assert.IsTrue(_settings.enablePassiveDrain);
        }

        [Test]
        public void Defaults_PassiveDrainRateIsHalf()
        {
            Assert.AreEqual(0.5f, _settings.passiveDrainPerSecond, 0.001f);
        }

        [Test]
        public void Defaults_ActionDrainEnabled()
        {
            Assert.IsTrue(_settings.enableActionDrain);
        }

        [Test]
        public void Defaults_SwingCostIsTwo()
        {
            Assert.AreEqual(2f, _settings.swingCost, 0.001f);
        }

        [Test]
        public void Defaults_ThrowCostIsFive()
        {
            Assert.AreEqual(5f, _settings.throwCost, 0.001f);
        }

        [Test]
        public void Defaults_GrappleCostIsEight()
        {
            Assert.AreEqual(8f, _settings.grappleCost, 0.001f);
        }

        [Test]
        public void Defaults_LowBatteryThresholdIs20()
        {
            Assert.AreEqual(20, _settings.lowBatteryThreshold);
        }

        [Test]
        public void Defaults_InfiniteBatteryDebugFalse()
        {
            Assert.IsFalse(_settings.infiniteBatteryDebug);
        }
    }
}
