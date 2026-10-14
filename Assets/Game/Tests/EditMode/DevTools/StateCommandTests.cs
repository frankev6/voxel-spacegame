using System;
using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Narrative;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.DevTools
{
    /// <summary>DEV-017's three required direct cases; real state owners, no scene or frame waits.</summary>
    public sealed class StateCommandTests
    {
        Fixture fixture;
        [SetUp] public void Setup() { EventHub.Reset(); fixture = new Fixture(); }
        [TearDown] public void Cleanup() { fixture?.Dispose(); fixture = null; EventHub.Reset(); TestContentFactory.DestroyAll(); }

        sealed class Fixture : IDisposable
        {
            readonly List<Action> restoreServices = new List<Action>();
            readonly World previousWorld, world;
            internal readonly ContentService Content;
            internal readonly EconomyService Economy;
            internal readonly FactStore Facts;
            internal readonly LogicContext Logic;
            internal readonly DevContext Context;
            internal readonly DevCommandRegistry Commands = DevCommandRegistry.CreateDefault();
            internal readonly ScenarioProbeRegistry Probes = ScenarioProbeRegistry.CreateDefault();
            internal Fixture()
            {
                var definitions = FactCatalogTests.BaseContent();
                definitions.Add(TestContentFactory.Item("item_fuel", so => TestContentFactory.Set(so, "maxStack", 10)));
                definitions.Add(FactCatalogTests.Facts("facts_dev",
                    new FactDeclaration { Name = "story_count", Kind = FactKind.Int, Min = -5, Max = 5, FormerNames = new[] { "story_old" } },
                    new FactDeclaration { Name = "story_flag", Kind = FactKind.Bool },
                    new FactDeclaration { Name = "story_zeta", Kind = FactKind.Int }));
                Content = ContentService.CreateForTests(definitions);
                var report = new ValidationReport("state commands");
                Facts = new FactStore(FactCatalog.Build(Content.Catalog, RefResolverRegistry.CreateDefault(Content.Catalog), report));
                Assert.IsFalse(report.HasErrors, report.ToText());
                var config = TestContentFactory.Create<EconomyConfigDefinition>("config_economy");
                Economy = new EconomyService(Content.Catalog, config, () => 7, () => 23);
                // Only unused narrative/world facades are read fixtures; evaluated facts and economy are real owners.
                var facades = new FakeLogicContext(Facts.Catalog) { UniversalTime = 23 };
                Logic = new LogicContext(Facts, Economy, facades, facades, () => 7); Facts.BindTraceWorld(facades);
                Register(Content); Register(Economy); Register(Facts); Register<ILogicContext>(Logic);
                previousWorld = World.DefaultGameObjectInjectionWorld;
                world = new World("DEV-017 direct fixture"); World.DefaultGameObjectInjectionWorld = world;
                Context = new DevContext(null);
            }
            void Register<T>(T value) where T : class
            {
                Services.TryGet<T>(out var previous);
                if (previous != null) Services.Unregister(previous);
                Services.Register(value);
                restoreServices.Add(() => { Services.Unregister(value); if (previous != null) Services.Register(previous); });
            }
            internal string Ok(string line)
            {
                var result = Commands.Execute(line, Context);
                Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text); return result.Text;
            }
            internal string Fail(string line)
            {
                var result = Commands.Execute(line, Context);
                Assert.AreEqual(DevResultKind.Failed, result.Kind, result.Text); return result.Text;
            }
            internal ScenarioValue Probe(string source) => ScenarioParser.ParseExpression(source, Probes).Evaluate(Context);
            public void Dispose()
            {
                Context.Dispose(); World.DefaultGameObjectInjectionWorld = previousWorld; world.Dispose();
                for (int i = restoreServices.Count - 1; i >= 0; i--) restoreServices[i]();
                Content.Dispose();
            }
        }

        [Test] public void GiveThenInv()
        {
            var f = fixture; int acquired = 0; AcquireSource source = default;
            EventBus<ItemAcquired>.Subscribe(value => { acquired += value.Count; source = value.Source; });
            StringAssert.Contains("item_fuel×5", f.Ok("give item=item_fuel count=5"));
            StringAssert.Contains("0: item_fuel×5", f.Ok("inv"));
            Assert.AreEqual(5, f.Economy.Inventory.CountOf(new ContentId("item_fuel")));
            Assert.AreEqual(0, acquired); EventBus<ItemAcquired>.Drain();
            Assert.AreEqual(5, acquired); Assert.AreEqual(AcquireSource.Dev, source);
            Assert.AreEqual(5, f.Probe("inv(\"item_fuel\")").Number);
            Assert.AreEqual(0, f.Probe("inv(\"item_removed\")").Number);
            Assert.AreEqual("Unknown item", f.Fail("give item=item_removed count=5"));
            f.Fail("give item=item_fuel count=0"); f.Fail("give item=item_fuel count=2147483648");
            f.Fail("give item=item_fuel credits=5"); f.Fail("give credits=5 count=2");
            Assert.AreEqual(5, f.Economy.Inventory.CountOf(new ContentId("item_fuel")));
            f.Economy.Inventory.SetCapacity(2); f.Ok("give item=item_fuel count=10"); f.Economy.Inventory.SetCapacity(1);
            string inventory = f.Ok("inv");
            StringAssert.Contains("0: item_fuel×10", inventory); StringAssert.Contains("1: item_fuel×5 [overflow]", inventory);
            StringAssert.Contains("capacity 1", inventory); StringAssert.Contains("credits 0", inventory);
            Assert.AreEqual(15, f.Probe("inv(\"item_fuel\")").Number);
            Assert.AreEqual("Cargo full", f.Fail("give item=item_fuel"));
            Assert.AreEqual(inventory, f.Ok("inv"), "A refused grant must not fill overflow or change any slot.");
            StringAssert.Contains("1 more occupied slots", f.Ok("inv max=1"));
            StringAssert.DoesNotContain("item_fuel", f.Ok("inv max=0")); f.Fail("inv max=-1"); f.Fail("inv max=201");
            Assert.AreEqual("credits 7", f.Ok("give credits=7")); Assert.AreEqual(7, f.Probe("credits").Number);
            f.Fail("give credits=-1"); Assert.AreEqual(7, f.Economy.Wallet.Credits);
            f.Ok("give credits=9223372036854775807"); Assert.AreEqual(Wallet.MaxCredits, f.Economy.Wallet.Credits);
            StringAssert.Contains("credits 1000000000000", f.Ok("inv"));
        }

        [Test] public void FactsSetGetEval()
        {
            var f = fixture; var changes = new List<FactChanged>(); EventBus<FactChanged>.Subscribe(changes.Add);
            Assert.AreEqual("story_count=3", f.Ok("facts set=story_count=3"));
            Assert.AreEqual(0, changes.Count); EventBus<FactChanged>.Drain();
            Assert.AreEqual(1, changes.Count); Assert.AreEqual(FactWriteSource.Debugger, changes[0].Source);
            Assert.AreEqual(3, changes[0].NewValue); Assert.AreEqual("story_count", changes[0].Id.Name);
            Assert.AreEqual("story_count=3", f.Ok("facts get=story_old"));
            int traceVersion = f.Facts.Trace.Version;
            Assert.AreEqual("true", f.Ok("facts eval=\"$story_count >= 3\""));
            Assert.AreEqual("false", f.Ok("facts eval=\"$story_count > 3\""));
            Assert.AreEqual(3, f.Probe("fact(\"story_count\")").Number);
            var condition = ScenarioParser.ParseExpression("cond(\"$story_count >= 3\")", f.Probes);
            Assert.IsTrue(condition.Evaluate(f.Context).Bool); Assert.AreEqual(traceVersion, f.Facts.Trace.Version);
            var caller = new LogicScope(LogicUsage.QuestObjective, 17, 2, 9, false, default); f.Logic.PushScope(caller);
            try
            {
                f.Ok("give item=item_fuel count=5"); f.Ok("give credits=7");
                Assert.AreEqual("true", f.Ok("facts eval=\"has_item(\\\"item_fuel\\\", 5) and credits() >= 7\""));
                Assert.AreEqual(caller.Usage, f.Logic.Scope.Usage); Assert.AreEqual(17, f.Logic.Scope.QuestIndex);
                Assert.AreEqual(2, f.Logic.Scope.QuestInstance); Assert.AreEqual(9, f.Logic.Scope.BeatIndex);
            }
            finally { f.Logic.PopScope(); }
            Assert.AreEqual("story_count=5", f.Ok("facts set=story_count=100"));
            Assert.AreEqual("story_flag=1", f.Ok("facts set=story_flag=-2"));
            Assert.AreEqual("story_count=5\nstory_flag=1\nstory_zeta=0", f.Ok("facts list=story_"));
            StringAssert.DoesNotContain("story_old", f.Ok("facts list=\"\""));
            Assert.AreEqual("sys_clue_counter=2", f.Ok("facts set=sys_clue_counter=2"));
            EventBus<FactChanged>.Drain();
            foreach (var change in changes) Assert.AreEqual(FactWriteSource.Debugger, change.Source);
            var trace = new LogicTraceEntry[32]; int count = f.Facts.Trace.CopyTo(trace);
            Assert.AreEqual(4, count);
            for (int i = 0; i < count; i++) { Assert.AreEqual(FactWriteSource.Debugger, trace[i].Source); Assert.AreEqual(23, trace[i].UniversalTime); }
            Assert.AreEqual("story_count=-1", f.Ok("facts set=story_count=-1"));
            Assert.IsFalse(condition.Evaluate(f.Context).Bool, "The cached condition reads current facts.");
            Assert.AreEqual("true", f.Ok("facts eval=\"\""));
        }

        [Test] public void BadCondition_Fails()
        {
            var f = fixture; int traceVersion = f.Facts.Trace.Version;
            StringAssert.Contains("NAR-E001", f.Fail("facts eval=\"$story_count >=\""));
            StringAssert.Contains("NAR-E006", f.Fail("facts eval=\"$story_missing >= 3\""));
            StringAssert.Contains("NAR-E007", f.Fail("facts eval=\"has_item(\\\"item_removed\\\", 1)\""));
            StringAssert.Contains("NAR-E", f.Fail("facts eval=\"fact(story_count) >= 3\""));
            StringAssert.Contains("NAR-E", f.Fail("facts eval=\"3\""));
            StringAssert.Contains("NAR-E008", f.Fail("facts eval=\"roll(6) >= 1\""));
            StringAssert.Contains("NAR-E001", Assert.Throws<InvalidOperationException>(() => f.Probe("cond(\"$story_count >=\")")).Message);
            StringAssert.Contains("NAR-E006", f.Fail("facts get=story_missing"));
            StringAssert.Contains("NAR-E006", f.Fail("facts set=story_missing=3"));
            Assert.Throws<InvalidOperationException>(() => f.Probe("fact(\"story_missing\")"));
            f.Fail("facts set=story_count=2147483648"); f.Fail("facts set=story_count=3=4"); f.Fail("facts set=story_count");
            f.Fail("facts get=story_count list=story_"); f.Fail("facts wat=story_count"); f.Fail("give"); f.Fail("give item=item_fuel wat=1");
            Assert.AreEqual(0, f.Facts.GetBySlot(f.Facts.Catalog.SlotOf("story_count"))); Assert.AreEqual(traceVersion, f.Facts.Trace.Version);
            Services.Unregister(f.Facts);
            try { StringAssert.Contains("facts unavailable", f.Fail("facts get=story_count")); }
            finally { Services.Register(f.Facts); }
            var previous = World.DefaultGameObjectInjectionWorld; World.DefaultGameObjectInjectionWorld = null;
            try
            {
                StringAssert.Contains("world unavailable", f.Fail("give credits=5"));
                StringAssert.Contains("world unavailable", f.Fail("facts set=story_count=3"));
            }
            finally { World.DefaultGameObjectInjectionWorld = previous; }
            Assert.AreEqual(0, f.Economy.Wallet.Credits); Assert.AreEqual(traceVersion, f.Facts.Trace.Version);
            Assert.AreEqual(LogicUsage.None, f.Logic.Scope.Usage);
        }
    }
}
