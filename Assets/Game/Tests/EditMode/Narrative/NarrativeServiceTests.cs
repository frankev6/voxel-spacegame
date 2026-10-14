using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Narrative;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class NarrativeServiceTests
    {
        FactStore facts;
        NarrativeService service;
        int a, b;
        [SetUp] public void SetUp()
        {
            EventHub.Reset();
            facts = FactStoreTests.Create(new FactDeclaration { Name = "test_a", Kind = FactKind.Int },
                new FactDeclaration { Name = "test_b", Kind = FactKind.Int });
            a = facts.Catalog.SlotOf("test_a"); b = facts.Catalog.SlotOf("test_b");
            service = new NarrativeService(facts, new FakeLogicContext(facts.Catalog));
        }
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        void Start() { service.BeginNewGame(); service.SetWorldReady(true); service.Tick(); }
        [Test] public void OnlyDependentsEvaluated()
        {
            var probe = new Probe(0, new LogicDependency(LogicDependencyKind.Fact, a)); service.Register(probe);
            Start(); probe.Evaluations = 0;
            facts.SetBySlot(b, 1, FactWriteSource.Effect); service.Tick(); Assert.AreEqual(0, probe.Evaluations);
            facts.SetBySlot(a, 1, FactWriteSource.Effect);
            service.NotifyDependency(new LogicDependency(LogicDependencyKind.Fact, a));
            service.NotifyDependency(new LogicDependency(LogicDependencyKind.Fact, a));
            service.Tick(); Assert.AreEqual(1, probe.Evaluations); Assert.AreEqual(1, probe.LastChangeCount);
        }
        [Test] public void PassLimit_CarriesOver()
        {
            var first = new Probe(0, new LogicDependency(LogicDependencyKind.Fact, a));
            var second = new Probe(1, new LogicDependency(LogicDependencyKind.Fact, b));
            service.Register(first); service.Register(second); Start();
            first.Evaluations = second.Evaluations = 0;
            first.Action = () => facts.AddBySlot(b, 1, FactWriteSource.Effect);
            second.Action = () => facts.AddBySlot(a, 1, FactWriteSource.Effect);
            facts.AddBySlot(a, 1, FactWriteSource.Effect); service.Tick();
            Assert.AreEqual(8, first.Evaluations + second.Evaluations);
            int value = facts.GetBySlot(a); service.Tick();
            Assert.AreEqual(16, first.Evaluations + second.Evaluations);
            Assert.Greater(facts.GetBySlot(a), value);
            Assert.AreEqual(3, first.AfterCalls); Assert.AreEqual(3, second.AfterCalls);
        }
        [Test] public void NotReady_NoEvaluation()
        {
            var probe = new Probe(0, new LogicDependency(LogicDependencyKind.Item, 3)); service.Register(probe);
            service.SetWorldReady(true); service.Tick(); Assert.AreEqual(0, probe.AfterCalls);
            service.BeginNewGame(); service.SetWorldReady(false);
            service.NotifyDependency(new LogicDependency(LogicDependencyKind.Item, 3)); service.Tick();
            Assert.AreEqual(0, probe.Evaluations); Assert.AreEqual(0, probe.AfterCalls);
            service.SetWorldReady(true); service.Tick();
            Assert.AreEqual(1, probe.Evaluations); Assert.AreEqual(1, probe.LastChangeCount);
        }
        [Test] public void Restored_FullEvaluationOnFirstReadyTick()
        {
            var first = new Probe(0, new LogicDependency(LogicDependencyKind.Fact, a));
            var second = new Probe(1, new LogicDependency(LogicDependencyKind.Fact, b));
            service.Register(first); service.Register(second);
            var snapshot = new FactSnapshot(); snapshot.Entries.Add(new FactEntry("test_a", 7)); facts.Restore(snapshot);
            service.OnSessionRestored(); service.Tick();
            Assert.AreEqual(1, first.Restores); Assert.AreEqual(0, first.Evaluations); Assert.AreEqual(7, facts.GetBySlot(a));
            service.SetWorldReady(true); service.Tick(); service.Tick();
            Assert.AreEqual(1, first.FullCalls); Assert.AreEqual(1, second.FullCalls);
            Assert.AreEqual(1, first.Evaluations); Assert.AreEqual(1, second.Evaluations);
        }
        [Test] public void SystemsRunInOrder()
        {
            var order = new List<int>();
            var high = new Probe(20, default) { Action = () => order.Add(20) };
            var low = new Probe(10, default) { Action = () => order.Add(10) };
            var equal = new Probe(20, default) { Action = () => order.Add(21) };
            service.Register(high); service.Register(low); service.Register(equal);
            service.SetPlayerFree(true); Start();
            CollectionAssert.AreEqual(new[] { 10, 20, 21 }, order);
            Assert.IsTrue(high.PlayerFree); Assert.IsTrue(low.PlayerFree); Assert.IsTrue(equal.PlayerFree);
            service.SetPlayerFree(false); service.Tick(); Assert.IsFalse(high.PlayerFree);
        }
        internal sealed class Probe : INarrativeSystem
        {
            readonly DependencyIndex index;
            readonly HashSet<int> dirty = new(1);
            public int Order { get; }
            public int Evaluations, FullCalls, AfterCalls, Restores, LastChangeCount;
            public bool PlayerFree;
            public Action Action;
            public Probe(int order, LogicDependency dependency)
            {
                Order = order;
                index = new DependencyIndex(new IReadOnlyList<LogicDependency>[] { new[] { dependency } });
            }
            public void OnNewGame() { }
            public void OnRestored() => Restores++;
            public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
            {
                dirty.Clear(); index.Collect(changed, dirty); LastChangeCount = changed.Count;
                if (fullReevaluate) FullCalls++;
                if (!fullReevaluate && dirty.Count == 0) return;
                Evaluations++; Action?.Invoke();
            }
            public void AfterEvaluation(bool playerFree) { AfterCalls++; PlayerFree = playerFree; }
        }
    }
}
