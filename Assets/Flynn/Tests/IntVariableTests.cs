using NUnit.Framework;
using UnityEngine;
using Flynn.Common;

namespace Flynn.Tests
{
    [TestFixture]
    public class IntVariableTests
    {
        private IntVariable _var;

        [SetUp]
        public void SetUp()
        {
            _var = ScriptableObject.CreateInstance<IntVariable>();
            SetPrivateField(_var, "_initialValue", 50);
        }

        [TearDown]
        public void TearDown()
        {
            if (_var != null) Object.DestroyImmediate(_var);
        }

        [Test]
        public void Value_ReturnsInitialValueOnFirstAccess()
        {
            Assert.AreEqual(50, _var.Value);
        }

        [Test]
        public void Add_IncreasesValue()
        {
            _var.Add(10);
            Assert.AreEqual(60, _var.Value);
        }

        [Test]
        public void Add_NegativeDecreasesValue()
        {
            _var.Add(-20);
            Assert.AreEqual(30, _var.Value);
        }

        [Test]
        public void Value_Setter_UpdatesValue()
        {
            _var.Value = 75;
            Assert.AreEqual(75, _var.Value);
        }

        [Test]
        public void OnChanged_FiresWhenValueChanges()
        {
            int received = -1;
            _var.OnChanged += v => received = v;

            _var.Value = 80;
            Assert.AreEqual(80, received);
        }

        [Test]
        public void OnChanged_DoesNotFireWhenValueUnchanged()
        {
            int fireCount = 0;
            _var.OnChanged += _ => fireCount++;

            _var.Value = 50; // Same as initial
            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void OnChanged_FiresOncePerChange()
        {
            int fireCount = 0;
            _var.OnChanged += _ => fireCount++;

            _var.Value = 60;
            _var.Value = 70;
            _var.Value = 80;
            Assert.AreEqual(3, fireCount);
        }

        [Test]
        public void Add_TriggersOnChanged()
        {
            int received = -1;
            _var.OnChanged += v => received = v;

            _var.Add(25);
            Assert.AreEqual(75, received);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
