using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-006: EventBus/EventHub (SPEC-02 §5.5, §7).</summary>
    public sealed class EventBusTests
    {
        struct EvA { public int V; public EvA(int v) { V = v; } }
        struct EvB { public int V; public EvB(int v) { V = v; } }
        struct EvLoop { public int Depth; }

        readonly List<string> log = new List<string>();

        [SetUp] public void SetUp() { EventHub.Reset(); GameLog.ResetOnceKeys(); log.Clear(); }
        [TearDown] public void TearDown() { EventHub.Reset(); GameLog.ResetOnceKeys(); }

        void OnA(EvA e) => log.Add("A" + e.V);
        void OnB(EvB e) => log.Add("B" + e.V);

        [Test]
        public void Publish_IsDeferred_UntilDrain()
        {
            EventBus<EvA>.Subscribe(OnA);
            EventBus<EvA>.Publish(new EvA(1));
            CollectionAssert.IsEmpty(log);
            Assert.AreEqual(1, EventBus<EvA>.PendingCount); Assert.AreEqual(1, EventHub.PendingCount);
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1" }, log);
            Assert.AreEqual(0, EventBus<EvA>.PendingCount); Assert.AreEqual(0, EventHub.PendingCount);
        }

        [Test]
        public void DrainAll_PreservesGlobalOrder_AcrossTypes()
        {
            EventBus<EvA>.Subscribe(OnA); EventBus<EvB>.Subscribe(OnB);
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvB>.Publish(new EvB(1)); EventBus<EvA>.Publish(new EvA(2));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1", "B1", "A2" }, log);
        }

        [Test]
        public void HandlerThrows_OthersStillCalled()
        {
            EventBus<EvA>.Subscribe(_ => throw new InvalidOperationException("boom"));
            EventBus<EvA>.Subscribe(OnA);
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvA>.Publish(new EvA(2));
            for (int i = 0; i < 2; i++)   // GameLog.Exception logs an error line and the exception itself
            { LogAssert.Expect(LogType.Error, new Regex("boom")); LogAssert.Expect(LogType.Exception, new Regex("boom")); }
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1", "A2" }, log);
        }

        [Test]
        public void UnsubscribeDuringDispatch_NotCalledAgain()
        {
            Action<EvA> second = e => log.Add("second" + e.V);
            Action<EvA> first = null;
            first = e => { log.Add("first" + e.V); EventBus<EvA>.Unsubscribe(second); EventBus<EvA>.Unsubscribe(first); };
            EventBus<EvA>.Subscribe(first); EventBus<EvA>.Subscribe(second);
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvA>.Publish(new EvA(2));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "first1" }, log, "second was removed before its turn; first only once");
            Assert.AreEqual(0, EventBus<EvA>.SubscriberCount);
        }

        [Test]
        public void SubscribeDuringDispatch_GetsOnlyLaterEvents()
        {
            bool added = false;
            EventBus<EvA>.Subscribe(e => { log.Add("old" + e.V); if (!added) { added = true; EventBus<EvA>.Subscribe(x => log.Add("new" + x.V)); } });
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvA>.Publish(new EvA(2));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "old1", "old2", "new2" }, log);
        }

        [Test]
        public void PublishInHandler_DeliveredSameDrain()
        {
            EventBus<EvB>.Subscribe(OnB);
            EventBus<EvA>.Subscribe(e => { log.Add("A" + e.V); EventBus<EvB>.Publish(new EvB(e.V * 10)); });
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvA>.Publish(new EvA(2));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1", "A2", "B10", "B20" }, log, "cascaded events follow in the next pass");
            Assert.AreEqual(0, EventHub.PendingCount);
        }

        [Test]
        public void InfiniteCascade_StopsAfter8Passes_Warns()
        {
            int calls = 0;
            EventBus<EvLoop>.Subscribe(e => { calls++; EventBus<EvLoop>.Publish(new EvLoop { Depth = e.Depth + 1 }); });
            EventBus<EvLoop>.Publish(new EvLoop());
            LogAssert.Expect(LogType.Warning, new Regex("events still pending after 8 passes"));
            EventHub.DrainAll();
            Assert.AreEqual(EventHub.MaxCascadePasses, calls);
            Assert.AreEqual(1, EventHub.PendingCount, "the rest waits for the next frame");
            EventHub.DrainAll();   // warn-once: no second warning
            Assert.AreEqual(2 * EventHub.MaxCascadePasses, calls);
        }

        [Test]
        public void ReentrantDrainAll_NoOp()
        {
            EventBus<EvA>.Subscribe(e => { log.Add("A" + e.V); EventHub.DrainAll(); });
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvA>.Publish(new EvA(2));
            LogAssert.Expect(LogType.Warning, new Regex("DrainAll called during a drain"));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1", "A2" }, log);
            Assert.IsFalse(EventHub.IsDraining);
        }

        [Test]
        public void TypedDrain_ThenDrainAll_NoDuplicates()
        {
            EventBus<EvA>.Subscribe(OnA); EventBus<EvB>.Subscribe(OnB);
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvB>.Publish(new EvB(1)); EventBus<EvA>.Publish(new EvA(2));
            EventBus<EvA>.Drain();
            CollectionAssert.AreEqual(new[] { "A1", "A2" }, log);
            EventBus<EvA>.Publish(new EvA(3));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1", "A2", "B1", "A3" }, log, "stale hub entries are skipped");
            EventBus<EvB>.Subscribe(e => EventBus<EvA>.Drain());
            EventBus<EvB>.Publish(new EvB(2));
            LogAssert.Expect(LogType.Warning, new Regex("Drain called inside EventHub.DrainAll"));
            EventHub.DrainAll();
        }

        [Test]
        public void DuplicateSubscribe_Ignored()
        {
            EventBus<EvA>.Subscribe(OnA);
            LogAssert.Expect(LogType.Warning, new Regex("duplicate subscription"));
            EventBus<EvA>.Subscribe(OnA);
            Assert.AreEqual(1, EventBus<EvA>.SubscriberCount);
            EventBus<EvA>.Publish(new EvA(1));
            EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A1" }, log);
            Assert.Throws<ArgumentNullException>(() => EventBus<EvA>.Subscribe(null));
            EventBus<EvB>.Unsubscribe(OnB);   // never subscribed: no-op
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            EventBus<EvA>.Subscribe(OnA); EventBus<EvB>.Subscribe(OnB);
            EventBus<EvA>.Publish(new EvA(1)); EventBus<EvB>.Publish(new EvB(1));
            EventHub.Reset();
            Assert.AreEqual(0, EventBus<EvA>.SubscriberCount); Assert.AreEqual(0, EventBus<EvA>.PendingCount);
            Assert.AreEqual(0, EventBus<EvB>.SubscriberCount); Assert.AreEqual(0, EventBus<EvB>.PendingCount);
            Assert.AreEqual(0, EventHub.PendingCount);
            EventHub.DrainAll();
            CollectionAssert.IsEmpty(log);
            EventBus<EvA>.Subscribe(OnA); EventBus<EvA>.Publish(new EvA(5)); EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "A5" }, log, "the bus works again after a reset");
        }

        [Test]
        public void PublishDrain_DoesNotAllocate()
        {
            int sum = 0;
            Action<EvA> h1 = e => sum += e.V, h2 = e => sum -= e.V, h3 = e => sum ^= e.V;
            EventBus<EvA>.Subscribe(h1); EventBus<EvA>.Subscribe(h2); EventBus<EvA>.Subscribe(h3);
            void Round() { for (int i = 0; i < 1000; i++) EventBus<EvA>.Publish(new EvA(i)); EventHub.DrainAll(); }
            Round(); Round();   // grow the queues
            Assert.That(() => { Round(); }, Is.Not.AllocatingGCMemory());
        }
    }
}
