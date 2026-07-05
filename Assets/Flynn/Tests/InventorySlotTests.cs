using NUnit.Framework;
using UnityEngine;
using Flynn.Player;

namespace Flynn.Tests
{
    [TestFixture]
    public class InventorySlotTests
    {
        private ItemDefinition _item;

        [SetUp]
        public void SetUp()
        {
            _item = ScriptableObject.CreateInstance<ItemDefinition>();
            _item.maxStack = 3;
        }

        [TearDown]
        public void TearDown()
        {
            if (_item != null) Object.DestroyImmediate(_item);
        }

        [Test]
        public void Empty_IsEmptyTrue()
        {
            var slot = InventorySlot.Empty;
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void Empty_MaxStackIs1()
        {
            var slot = InventorySlot.Empty;
            Assert.AreEqual(1, slot.MaxStack);
        }

        [Test]
        public void Empty_SpaceLeftIs0()
        {
            var slot = InventorySlot.Empty;
            Assert.AreEqual(0, slot.SpaceLeft);
        }

        [Test]
        public void Of_SetsItemAndCount()
        {
            var slot = InventorySlot.Of(_item, 2);
            Assert.AreEqual(_item, slot.item);
            Assert.AreEqual(2, slot.count);
        }

        [Test]
        public void Of_NullItem_CountIsZero()
        {
            var slot = InventorySlot.Of(null, 5);
            Assert.AreEqual(0, slot.count);
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void Of_NegativeCount_ClampsToZero()
        {
            var slot = InventorySlot.Of(_item, -3);
            Assert.AreEqual(0, slot.count);
        }

        [Test]
        public void IsFull_TrueWhenCountEqualsMaxStack()
        {
            var slot = InventorySlot.Of(_item, 3);
            Assert.IsTrue(slot.IsFull);
        }

        [Test]
        public void IsFull_FalseWhenCountBelowMaxStack()
        {
            var slot = InventorySlot.Of(_item, 2);
            Assert.IsFalse(slot.IsFull);
        }

        [Test]
        public void SpaceLeft_CorrectForPartialStack()
        {
            var slot = InventorySlot.Of(_item, 1);
            Assert.AreEqual(2, slot.SpaceLeft);
        }

        [Test]
        public void SpaceLeft_ZeroWhenFull()
        {
            var slot = InventorySlot.Of(_item, 3);
            Assert.AreEqual(0, slot.SpaceLeft);
        }

        [Test]
        public void IsEmpty_TrueWhenCountIsZero()
        {
            var slot = InventorySlot.Of(_item, 0);
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void IsEmpty_TrueWhenItemIsNull()
        {
            var slot = new InventorySlot { item = null, count = 5 };
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void MaxStack_RespectsItemDefinitionMaxStack()
        {
            var slot = InventorySlot.Of(_item, 1);
            Assert.AreEqual(3, slot.MaxStack);
        }
    }
}
