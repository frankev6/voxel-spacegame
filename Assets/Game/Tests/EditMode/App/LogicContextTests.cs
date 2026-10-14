using System;
using Game.App.Services;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Presentation.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Ai;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Narrative;
using Unity.Entities;
using Game.Core.Events;
using Game.Data.Logic;
using Game.Narrative;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class LogicContextTests
    {
        [Test] public void ScopeStack_DepthLimit()
        {
            using var environment = new LogicTestEnvironment();
            var facts = new FactStore(environment.Facts);
            var owners = new FakeLogicContext(environment.Facts) { UniversalTime = 23 };
            ulong seed = 7;
            var context = new LogicContext(facts, owners, owners, owners, () => seed);
            Assert.AreSame(facts, context.Facts); Assert.AreSame(owners, context.World);
            for (int i = 1; i <= 8; i++)
            {
                context.PushScope(new LogicScope(LogicUsage.QuestEffect, i, 0, 0, false, default));
                Assert.AreEqual(i, context.Scope.QuestIndex);
            }
            Assert.Throws<InvalidOperationException>(() => context.PushScope(default));
            Assert.AreEqual(8, context.Scope.QuestIndex);
            for (int i = 8; i > 0; i--) { Assert.AreEqual(i, context.Scope.QuestIndex); context.PopScope(); }
            Assert.AreEqual(LogicUsage.None, context.Scope.Usage);
            Assert.Throws<InvalidOperationException>(() => context.PopScope());
            seed = 9; Assert.AreEqual(9ul, context.CampaignSeed);
            Action continuation = () => { }; context.BlockingCommandCompleted = continuation;
            Assert.AreSame(continuation, ((ILogicEffectHost)context).BlockingCommandCompleted);
            context.EffectRejected(); var trace = new LogicTraceEntry[1];
            Assert.AreEqual(1, facts.Trace.CopyTo(trace));
            Assert.AreEqual(LogicTraceKind.EffectRejected, trace[0].Kind); Assert.AreEqual(23, trace[0].UniversalTime);
        }
        [Test] public void Bootstrap_FailsOnBindingErrors()
        {
            var definitions = FactCatalogTests.BaseContent();
            definitions.Add(TestContentFactory.Create<MissingFactOwner>("test_binding"));
            var report = new ValidationReport("bootstrap fixture");
            var content = ContentCatalog.Build(definitions, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            bool contextCreated = false;
            var error = Assert.Throws<ContentValidationException>(() => NarrativeBootstrap.Build(content, definitions,
                facts => { contextCreated = true; return new FakeLogicContext(facts.Catalog); }));
            StringAssert.Contains("missing_boot_fact", error.Message); Assert.IsFalse(contextCreated);
            definitions.RemoveAt(definitions.Count - 1);
            definitions.Add(FactCatalogTests.Facts("facts_bad_boot",
                new FactDeclaration { Name = "duplicate_fact", Kind = FactKind.Int },
                new FactDeclaration { Name = "duplicate_fact", Kind = FactKind.Int }));
            content = ContentCatalog.Build(definitions, new ValidationReport("duplicates"));
            Assert.Throws<ContentValidationException>(() => NarrativeBootstrap.Build(content, definitions,
                facts => { contextCreated = true; return new FakeLogicContext(facts.Catalog); }));
            Assert.IsFalse(contextCreated);
        }
        [Test] public void TickBridge_RunsAfterDrainOncePerFrame()
        {
            Services.Reset(); EventHub.Reset();
            using var environment = new LogicTestEnvironment();
            using var world = new World("narrative bridge fixture");
            var facts = new FactStore(environment.Facts);
            var service = new NarrativeService(facts, new FakeLogicContext(environment.Facts));
            int slot = facts.Catalog.SlotOf("story_count"), observed = -1;
            var probe = new NarrativeServiceTests.Probe(0, new LogicDependency(LogicDependencyKind.Fact, slot))
                { Action = () => observed = facts.GetBySlot(slot) };
            service.Register(probe); service.BeginNewGame(); Services.Register(service);
            var em = world.EntityManager;
            em.CreateEntity(typeof(GameplayActive), typeof(WorldSceneActive));
            em.CreateEntity(typeof(PlayerShip)); var combat = em.CreateEntity(typeof(PlayerCombatStatus));
            var group = world.GetOrCreateSystemManaged<PresentationBridgeGroup>();
            group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<NarrativeTickBridgeSystem>());
            group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<ManagedEventDrainSystem>());
            group.SortSystems();
            EventBus<SessionReset>.Subscribe(_ => facts.SetBySlot(slot, 4, FactWriteSource.WorldEvent));
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Loaded));
            group.Update();
            Assert.AreEqual(4, observed); Assert.AreEqual(1, probe.AfterCalls); Assert.IsTrue(probe.PlayerFree);
            Assert.IsTrue(service.PlayerCanTalk);
            em.SetComponentData(combat, new PlayerCombatStatus { HostilesEngaging = 1 }); group.Update();
            Assert.AreEqual(2, probe.AfterCalls); Assert.IsFalse(probe.PlayerFree);
        }
        [TearDown] public void Cleanup() { EventHub.Reset(); Services.Reset(); TestContentFactory.DestroyAll(); }

        [ContentIdPrefix("test_")]
        public sealed class MissingFactOwner : ContentDefinition, ILogicSourceOwner
        {
            public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, ValidationScope scope) { }
            public void BindLogic(LogicBinder binder) => binder.BindFact("missing_boot_fact");
        }
    }
}
