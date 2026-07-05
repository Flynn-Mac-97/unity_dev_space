using NUnit.Framework;
using UnityEngine;
using Flynn.Core;
using Flynn.Events;

namespace Flynn.Tests
{
    [TestFixture]
    public class GameEventBusTests
    {
        private GameObject _go;
        private GameEventBus _bus;

        // Test event struct
        private readonly struct TestEvent
        {
            public readonly int Value;
            public TestEvent(int value) { Value = value; }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestEventBus");
            _bus = _go.AddComponent<GameEventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(42)));
        }

        [Test]
        public void Subscribe_ThenPublish_HandlerReceivesEvent()
        {
            int received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Value);

            _bus.Publish(new TestEvent(99));
            Assert.AreEqual(99, received);
        }

        [Test]
        public void Unsubscribe_HandlerNotCalledAfterUnsubscribe()
        {
            int callCount = 0;
            void Handler(TestEvent e) => callCount++;

            _bus.Subscribe<TestEvent>(Handler);
            _bus.Unsubscribe<TestEvent>(Handler);
            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllCalled()
        {
            int calls1 = 0, calls2 = 0;
            _bus.Subscribe<TestEvent>(e => calls1++);
            _bus.Subscribe<TestEvent>(e => calls2++);

            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, calls1);
            Assert.AreEqual(1, calls2);
        }

        [Test]
        public void Publish_HandlerThrows_OtherHandlersStillCalled()
        {
            bool secondCalled = false;
            _bus.Subscribe<TestEvent>(e => throw new System.Exception("Test exception"));
            _bus.Subscribe<TestEvent>(e => secondCalled = true);

            _bus.Publish(new TestEvent(1));
            Assert.IsTrue(secondCalled, "Second handler should still be called after first throws");
        }

        [Test]
        public void Publish_DifferentEventTypes_DoNotInterfere()
        {
            int testCalls = 0;
            _bus.Subscribe<TestEvent>(e => testCalls++);

            // Publish a different event type
            _bus.Publish(new BatteryEmpty());
            Assert.AreEqual(0, testCalls, "Handler for TestEvent should not be called for BatteryEmpty");

            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, testCalls);
        }

        [Test]
        public void Instance_SetOnAwake()
        {
            Assert.IsNotNull(GameEventBus.Instance);
            Assert.AreSame(_bus, GameEventBus.Instance);
        }
    }
}
