using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using Game.Narrative;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data.Logic
{
    /// <summary>NAR-020: four required checks using compiled/bound conditions and the deferred fact bus.</summary>
    [Category("Narrative")]
    public sealed class LogicWatcherTests
    {
        [SetUp] public void SetUp() => EventHub.Reset();
        [TearDown] public void TearDown() { EventHub.Reset(); DestroyAll(); }

        [Test] public void FlipOnly()
        {
            using var fixture = new Fixture();
            var condition = fixture.Condition("$intro_awake and has_item(\"resource_fuel\", 1)");
            var flips = new List<bool>();
            using var watcher = new LogicWatcher(condition, fixture, value => flips.Add(value));
            Assert.IsFalse(watcher.Value); Assert.AreEqual(0, flips.Count);
            Assert.AreEqual(1, condition.Evaluations); Assert.AreEqual(1, condition.Collections);
            Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount);
            Assert.AreEqual(1, fixture.EconomyFixture.ItemSubscribers); Assert.AreEqual(0, fixture.EconomyFixture.CreditSubscribers);

            fixture.EconomyFixture.SetItem(fixture.Fuel, 1); Assert.IsFalse(watcher.Value); Assert.AreEqual(0, flips.Count);
            fixture.Store.SetBySlot(fixture.Awake, 1, FactWriteSource.Debugger);
            Assert.IsFalse(watcher.Value, "Fact delivery is deferred until the bus drains.");
            EventHub.DrainAll(); Assert.IsTrue(watcher.Value);
            fixture.EconomyFixture.SetItem(fixture.Fuel, 2);
            watcher.Refresh();
            CollectionAssert.AreEqual(new[] { true }, flips, "Relevant changes that preserve the result do not call back.");
            fixture.Store.SetBySlot(fixture.Awake, 0, FactWriteSource.Debugger); EventHub.DrainAll();
            fixture.EconomyFixture.SetItem(fixture.Fuel, 0);
            fixture.Store.SetBySlot(fixture.Awake, 1, FactWriteSource.Debugger); EventHub.DrainAll();
            Assert.IsFalse(watcher.Value);
            fixture.EconomyFixture.SetItem(fixture.Fuel, 1); Assert.IsTrue(watcher.Value);
            CollectionAssert.AreEqual(new[] { true, false, true }, flips);
            Assert.AreEqual(1, condition.Collections, "Dependencies are collected once, despite short-circuit evaluations.");

            int creditFlips = 0;
            using (var credits = new LogicWatcher(fixture.Condition("credits() >= 5"), fixture, _ => creditFlips++))
            {
                Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount, "A credits-only watcher does not subscribe to facts.");
                Assert.AreEqual(1, fixture.EconomyFixture.ItemSubscribers); Assert.AreEqual(1, fixture.EconomyFixture.CreditSubscribers);
                fixture.EconomyFixture.SetCredits(4); Assert.IsFalse(credits.Value); Assert.AreEqual(0, creditFlips);
                fixture.EconomyFixture.SetCredits(5); Assert.IsTrue(credits.Value); Assert.AreEqual(1, creditFlips);
                fixture.EconomyFixture.SetCredits(6); Assert.IsTrue(credits.Value); Assert.AreEqual(1, creditFlips);
                fixture.EconomyFixture.SetCredits(0); Assert.IsFalse(credits.Value); Assert.AreEqual(2, creditFlips);
            }
            Assert.AreEqual(0, fixture.EconomyFixture.CreditSubscribers);
            using var constant = new LogicWatcher(fixture.Condition("true"), fixture, _ => Assert.Fail("Constant result cannot flip."));
            Assert.IsTrue(constant.Value); constant.Refresh();
            Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount); Assert.AreEqual(1, fixture.EconomyFixture.ItemSubscribers);
        }

        [Test] public void IgnoresUnrelatedFacts()
        {
            using var fixture = new Fixture();
            // Reverse catalog order and repeat a dependency to exercise the normalized key set.
            var condition = fixture.Condition("$story_count >= 1 and $intro_awake"); condition.RepeatDependency = true;
            int callbacks = 0;
            using var watcher = new LogicWatcher(condition, fixture, _ => callbacks++);
            fixture.Store.SetBySlot(fixture.Unrelated, 1, FactWriteSource.Debugger); EventHub.DrainAll();
            fixture.EconomyFixture.SetItem(fixture.Fuel, 1); fixture.EconomyFixture.SetCredits(1);
            Assert.AreEqual(1, condition.Evaluations); Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, fixture.EconomyFixture.ItemSubscribers); Assert.AreEqual(0, fixture.EconomyFixture.CreditSubscribers);
            fixture.Store.SetBySlot(fixture.Count, 1, FactWriteSource.Debugger); EventHub.DrainAll();
            Assert.AreEqual(2, condition.Evaluations); Assert.IsFalse(watcher.Value);
            fixture.Store.SetBySlot(fixture.Awake, 1, FactWriteSource.Debugger); EventHub.DrainAll();
            Assert.IsTrue(watcher.Value); Assert.AreEqual(3, condition.Evaluations); Assert.AreEqual(1, callbacks);
            Assert.AreEqual(1, condition.Collections);

            var itemCondition = fixture.Condition("has_item(\"resource_fuel\", 2)");
            using var itemWatcher = new LogicWatcher(itemCondition, fixture, _ => callbacks++);
            fixture.EconomyFixture.SetItem(fixture.Fuel + 100, 2);
            Assert.AreEqual(1, itemCondition.Evaluations, "Other item indices do not trigger evaluation.");
            fixture.EconomyFixture.SetItem(fixture.Fuel, 2); Assert.IsTrue(itemWatcher.Value); Assert.AreEqual(2, itemCondition.Evaluations);
            Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount, "An item-only watcher does not subscribe to facts.");

            var timeCondition = fixture.Condition("game_hours() >= 1");
            using var timeWatcher = new LogicWatcher(timeCondition, fixture, _ => callbacks++);
            fixture.Reads.UniversalTime = 3600;
            Assert.IsFalse(timeWatcher.Value); timeWatcher.Refresh(); Assert.IsTrue(timeWatcher.Value);
            Assert.AreEqual(2, timeCondition.Evaluations);
            Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount); Assert.AreEqual(1, fixture.EconomyFixture.ItemSubscribers);
        }

        [Test] public void Dispose_Unsubscribes()
        {
            using var fixture = new Fixture();
            var condition = fixture.Condition("$intro_awake and has_item(\"resource_fuel\", 1) and credits() >= 1");
            int callbacks = 0;
            var watcher = new LogicWatcher(condition, fixture, _ => callbacks++);
            fixture.EconomyFixture.SetItem(fixture.Fuel, 1); fixture.EconomyFixture.SetCredits(1);
            fixture.Store.SetBySlot(fixture.Awake, 1, FactWriteSource.Debugger);
            int evaluations = condition.Evaluations;
            watcher.Dispose(); watcher.Dispose();
            Assert.AreEqual(0, EventBus<FactChanged>.SubscriberCount);
            Assert.AreEqual(0, fixture.EconomyFixture.ItemSubscribers); Assert.AreEqual(0, fixture.EconomyFixture.CreditSubscribers);
            EventHub.DrainAll(); fixture.EconomyFixture.SetItem(fixture.Fuel, 2); fixture.EconomyFixture.SetCredits(2);
            watcher.Refresh();
            Assert.AreEqual(evaluations, condition.Evaluations); Assert.AreEqual(0, callbacks); Assert.IsFalse(watcher.Value);

            // Dispose during a flip, including an already-captured economy multicast invocation.
            LogicWatcher self = null, follower = null;
            var credits = fixture.Condition("credits() >= 3");
            using var first = new LogicWatcher(credits, fixture, _ => { callbacks++; self.Dispose(); follower.Dispose(); });
            self = first;
            var later = fixture.Condition("credits() >= 3");
            using var second = new LogicWatcher(later, fixture, _ => Assert.Fail("A disposed watcher must ignore a captured invocation."));
            follower = second;
            fixture.EconomyFixture.SetCredits(3); fixture.EconomyFixture.SetCredits(0);
            Assert.AreEqual(1, callbacks); Assert.AreEqual(2, credits.Evaluations); Assert.AreEqual(1, later.Evaluations);
            Assert.IsTrue(self.Value); Assert.AreEqual(0, fixture.EconomyFixture.CreditSubscribers); self.Dispose();
        }

        [Test] public void Events_DoNotAllocate()
        {
            using var fixture = new Fixture();
            var condition = fixture.Condition("$intro_awake and has_item(\"resource_fuel\", 1)");
            int callbacks = 0;
            using var watcher = new LogicWatcher(condition, fixture, _ => callbacks++);
            var change = new FactChanged(fixture.Store.Catalog.IdAt(fixture.Unrelated), fixture.Unrelated, 0, 1, FactWriteSource.Debugger);
            // Two fixed warm-up batches grow both hub buffers and the channel queue, outside measurement.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < 1000; i++) EventBus<FactChanged>.Publish(change);
                EventHub.DrainAll();
            }
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) EventBus<FactChanged>.Publish(change);
            EventHub.DrainAll();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "1,000 unrelated fact publications and deliveries must allocate zero bytes after queue warm-up.");
            Assert.AreEqual(1, condition.Evaluations); Assert.AreEqual(1, condition.Collections); Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, EventHub.PendingCount); Assert.AreEqual(0, EventBus<FactChanged>.PendingCount);
        }

        sealed class CountingCondition : ICondition
        {
            readonly ICondition inner;
            public int Evaluations, Collections;
            public bool RepeatDependency;
            public CountingCondition(ICondition inner) { this.inner = inner; }
            public bool Evaluate(ILogicContext context) { Evaluations++; return inner.Evaluate(context); }
            public void CollectDependencies(List<LogicDependency> into)
            { Collections++; inner.CollectDependencies(into); if (RepeatDependency && into.Count != 0) into.Add(into[0]); }
            public void Validate(ValidationReport report) => inner.Validate(report);
            public void Bind(LogicBinder binder) => inner.Bind(binder);
        }

        sealed class Fixture : ILogicContext, IDisposable
        {
            readonly LogicTestEnvironment environment = new LogicTestEnvironment();
            public readonly FactStore Store;
            public readonly FakeLogicContext Reads;
            public readonly NotifyingEconomy EconomyFixture;
            public readonly int Awake, Count, Unrelated, Fuel;
            public Fixture()
            {
                Store = new FactStore(environment.Facts); Reads = new FakeLogicContext(environment.Facts);
                EconomyFixture = new NotifyingEconomy(Reads);
                Awake = environment.Facts.SlotOf("intro_awake"); Count = environment.Facts.SlotOf("story_count");
                Unrelated = environment.Facts.SlotOf("sys_clue_counter");
                Fuel = environment.Content.IndexOf(environment.Content.Get<ItemDefinition>(new ContentId("resource_fuel")));
            }
            public CountingCondition Condition(string source)
            {
                var compiled = LogicTestEnvironment.Source(new CompiledCondition(), source); var report = new ValidationReport(source);
                Assert.IsTrue(environment.Compiler.Compile(compiled, new LogicCompileOptions(LogicUsage.BeatCondition, "test:watcher", "condition"), report), report.ToText());
                compiled.Bind(new LogicBinder(environment.Facts, environment.Refs, report)); Assert.AreEqual(0, report.ErrorCount, report.ToText());
                return new CountingCondition(compiled);
            }
            public ILogicFacts Facts => Store;
            public ILogicEconomy Economy => EconomyFixture;
            public ILogicNarrative Narrative => Reads;
            public ILogicWorld World => Reads;
            public LogicScope Scope => Reads.Scope;
            public ulong CampaignSeed => Reads.CampaignSeed;
            public void PushScope(in LogicScope scope) => Reads.PushScope(scope);
            public void PopScope() => Reads.PopScope();
            public void Dispose() => environment.Dispose();
        }

        sealed class NotifyingEconomy : ILogicEconomy
        {
            readonly FakeLogicContext values;
            Action<int> itemChanged;
            Action creditsChanged;
            public int ItemSubscribers, CreditSubscribers;
            public NotifyingEconomy(FakeLogicContext values) { this.values = values; }
            public event Action<int> ItemCountChanged { add { itemChanged += value; ItemSubscribers++; } remove { itemChanged -= value; ItemSubscribers--; } }
            public event Action CreditsChanged { add { creditsChanged += value; CreditSubscribers++; } remove { creditsChanged -= value; CreditSubscribers--; } }
            public void SetItem(int index, int count) { values.Items[index] = count; itemChanged?.Invoke(index); }
            public void SetCredits(long credits) { values.Credits = credits; creditsChanged?.Invoke(); }
            public int GetItemCount(int itemIndex) => values.GetItemCount(itemIndex);
            public long GetCredits() => values.GetCredits();
            public void GiveItems(int itemIndex, int count) => throw new NotSupportedException();
            public bool TryTakeItems(int itemIndex, int count) => throw new NotSupportedException();
            public void GiveCredits(long amount) => throw new NotSupportedException();
            public bool TryTakeCredits(long amount) => throw new NotSupportedException();
            public void GiveLoot(int lootTableIndex, int picks, ulong salt) => throw new NotSupportedException();
        }
    }
}
