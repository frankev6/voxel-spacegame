using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.App.Services;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Narrative;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    /// <summary>TRV-003: smallest live two-system galaxy, real FactStore/watcher, direct events/ticks and steady-state allocation check.</summary>
    public sealed class LaneAccessTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }
        sealed class Loader : ITravelLoader
        {
            public bool IsReady => throw new NotSupportedException("Lane access must not execute travel.");
            public StarSystemId Loaded => throw new NotSupportedException();
            public bool IsArrivalReady => throw new NotSupportedException();
            public bool IsAreaReady(double3 position) => throw new NotSupportedException();
            public bool BeginStarSystemChange(StarSystemId target, double3 position, float yaw, StarSystemEntryReason reason) => throw new NotSupportedException();
        }
        sealed class Audio : IAudioService
        {
            public void PlayUi(UiSound sound) => throw new NotSupportedException();
            public void PlayCue(ContentId cue) => throw new NotSupportedException();
            public void PlayCueAt(ContentId cue, float3 position, float volume) => throw new NotSupportedException();
            public void PlayStinger(ContentId cue) => throw new NotSupportedException();
            public void SetMusicOverride(ContentId cue, float seconds) => throw new NotSupportedException();
            public void ClearMusicOverride(float seconds) => throw new NotSupportedException();
        }
        internal sealed class Context : ILogicContext
        {
            internal readonly FactStore Store;
            internal readonly FakeLogicContext Reads;
            internal Context(FactCatalog facts) { Store = new FactStore(facts); Reads = new FakeLogicContext(facts); }
            public ILogicFacts Facts => Store;
            internal ILogicEconomy EconomyOverride;
            public ILogicEconomy Economy => EconomyOverride ?? Reads;
            public ILogicNarrative Narrative => Reads;
            public ILogicWorld World => Reads;
            public LogicScope Scope => Reads.Scope;
            public ulong CampaignSeed => Reads.CampaignSeed;
            public void PushScope(in LogicScope scope) => Reads.PushScope(scope);
            public void PopScope() => Reads.PopScope();
        }
        internal sealed class Fixture : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Content;
            internal readonly World World = new("TRV-003 lane fixture");
            internal readonly GalaxyState State = new();
            internal readonly GalaxyService Galaxy;
            internal readonly TravelService Travel;
            internal readonly ContentCatalog Catalog;
            internal readonly Context Logic;
            internal readonly FactCatalog Facts;
            readonly PauseService pause = new();
            internal Fixture(string source = "$gate_open", bool registerContext = true, bool generate = true, ITravelLoader loader = null, IPauseService pauseOverride = null, IAudioService audio = null, DockingService docking = null, int authoredCount = 2)
            {
                Content = new TestGalaxyFixtures.Fixture(authoredCount, 0, 42, false);
                var fuel = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/resource_fuel.asset");
                var cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>("Assets/Game/Content/Audio/cue_warp.asset");
                Assert.IsNotNull(fuel); Assert.IsNotNull(cue); Content.Definitions.Add(fuel); Content.Definitions.Add(cue);
                Content.Definitions.Add(TestContentFactory.Create<TravelConfigDefinition>("config_travel", so =>
                { TestContentFactory.Set(so, "fuelItem", fuel); TestContentFactory.Set(so, "departCue", cue); TestContentFactory.Set(so, "arriveCue", cue); }));
                Content.Definitions.Add(FactCatalogTests.Facts("facts_lanes", new FactDeclaration { Name = "gate_open", Kind = FactKind.Bool }));
                var condition = Content.Generation.AuthoredLanes[0].condition;
                LogicTestEnvironment.Source(condition, source); Content.Generation.AuthoredLanes[0].lockedMessage = "Gate is asleep.";
                var report = new ValidationReport("Lane fixture"); Catalog = ContentCatalog.Build(Content.Definitions, report);
                var refs = RefResolverRegistry.CreateDefault(Catalog); Facts = FactCatalog.Build(Catalog, refs, report);
                Content.Generation.CompileLogic(new LogicCompiler(Facts, refs, new LogicFunctionRegistry()), report.For("test:galaxy"));
                Content.Generation.BindLogic(new LogicBinder(Facts, refs, report)); Assert.IsFalse(report.HasErrors, report.ToText());
                Logic = new Context(Facts); if (registerContext) Services.Register<ILogicContext>(Logic);
                World.EntityManager.CreateEntity(typeof(WorldSceneActive));
                World.EntityManager.CreateEntity(typeof(Game.Simulation.Commands.PlayerControlLock));
                Galaxy = new GalaxyService(Catalog, State, World, new GalaxyFrameTicker()); if (generate) Galaxy.NewGame(42);
                Travel = new TravelService(Galaxy, loader ?? new Loader(), docking ?? new DockingService(), World, Catalog, pauseOverride ?? pause, audio ?? new Audio());
            }
            internal void Set(bool open) { Logic.Store.SetBySlot(Facts.SlotOf("gate_open"), open ? 1 : 0, FactWriteSource.Debugger); EventHub.DrainAll(); }
            public void Dispose() { Services.Unregister<ILogicContext>(Logic); Travel.Dispose(); Galaxy.Dispose(); State.Dispose(); World.Dispose(); pause.Dispose(); Content.Dispose(); }
        }
        [Test] public void Ungated_Open()
        {
            using var f = new Fixture(""); Assert.IsTrue(f.Travel.IsLaneOpen(0)); Assert.AreEqual("", f.Travel.GetLockedMessage(0));
            Assert.AreEqual(3, f.Travel.GetFuelCost(0)); Assert.IsFalse(f.Travel.IsLaneOpen(-1)); Assert.IsFalse(f.Travel.IsLaneOpen(1));
            Assert.AreEqual("", f.Travel.GetLockedMessage(1)); Assert.AreEqual(0, f.Travel.GetFuelCost(-1));
            using var empty = new Fixture("", registerContext: false, generate: false);
            Assert.IsFalse(empty.Travel.IsLaneOpen(0)); Assert.AreEqual(0, empty.Travel.GetFuelCost(0)); Assert.AreEqual("", empty.Travel.GetLockedMessage(0));
        }
        [Test] public void Gated_FollowsFact()
        {
            using var f = new Fixture(); Assert.IsFalse(f.Travel.IsLaneOpen(0)); Assert.AreEqual("Gate is asleep.", f.Travel.GetLockedMessage(0));
            f.Set(true); Assert.IsTrue(f.Travel.IsLaneOpen(0)); f.Set(false); Assert.IsFalse(f.Travel.IsLaneOpen(0));
            int fuel = f.Catalog.IndexOf(f.Travel.Config.FuelItem); f.Logic.Reads.Items[fuel] = 4; Assert.AreEqual(4, f.Travel.FuelHeld);
            // Location/time dependencies use the required explicit refresh paths; no time passes on the wall clock.
            Services.Unregister<ILogicContext>(f.Logic);
            using var time = new Fixture("game_hours() >= 1"); Assert.IsFalse(time.Travel.IsLaneOpen(0));
            time.Logic.Reads.UniversalTime = 3600;
            time.Travel.Tick(new GalaxyFrameContext(time.World.EntityManager, 3600, 599, 0, 1)); Assert.IsFalse(time.Travel.IsLaneOpen(0));
            time.Travel.Tick(new GalaxyFrameContext(time.World.EntityManager, 3600, 600, 0, 2)); Assert.IsTrue(time.Travel.IsLaneOpen(0));
            time.Logic.Reads.UniversalTime = 0; EventBus<StarSystemEntered>.Publish(new StarSystemEntered(time.State.CurrentStarSystem.Value, StarSystemEntryReason.NewGame, 1));
            EventHub.DrainAll(); Assert.IsFalse(time.Travel.IsLaneOpen(0));
        }
        [Test] public void Revision_OncePerFlip()
        {
            using var f = new Fixture(); uint start = f.Travel.Revision;
            f.Set(true); Assert.AreEqual(start + 1, f.Travel.Revision); f.Set(true); Assert.AreEqual(start + 1, f.Travel.Revision);
            f.Set(false); Assert.AreEqual(start + 2, f.Travel.Revision); Assert.AreEqual(start + 2, f.Travel.Revision);
            f.Content.Generation.AuthoredLanes[0].condition.SetCompiled(null, true); f.Galaxy.OnSessionRestored();
            LogAssert.Expect(LogType.Error, new Regex("Lane condition failed compilation")); Assert.IsFalse(f.Travel.IsLaneOpen(0));
            Assert.AreEqual(start + 3, f.Travel.Revision);
        }
        [Test] public void NoContext_ClosedThenRebuilt()
        {
            using var f = new Fixture(registerContext: false);
            LogAssert.Expect(LogType.Warning, new Regex("Narrative context is unavailable")); Assert.IsFalse(f.Travel.IsLaneOpen(0));
            uint revision = f.Travel.Revision; Assert.IsFalse(f.Travel.IsLaneOpen(0));
            f.Set(true); Services.Register<ILogicContext>(f.Logic); Assert.IsTrue(f.Travel.IsLaneOpen(0)); Assert.AreEqual(revision + 1, f.Travel.Revision);
            Assert.AreEqual(revision + 1, f.Travel.Revision); Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount);
        }
        [Test] public void FuelMath_Values()
        {
            foreach (var value in new[] { (6f, 3), (5.2f, 3), (.5f, 1) }) Assert.AreEqual(value.Item2, FuelMath.Cost(new LaneDef { LengthLy = value.Item1 }, .5f, 1, 0));
            Assert.AreEqual(0, FuelMath.Cost(new LaneDef { LengthLy = 6, Kind = LaneKind.AncientGate }, .5f, 1, 0));
        }
        [Test] public void Regeneration_DisposesWatchers()
        {
            using var f = new Fixture(); Assert.IsFalse(f.Travel.IsLaneOpen(0)); Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount); uint revision = f.Travel.Revision;
            f.Galaxy.OnSessionRestored(); Assert.IsFalse(f.Travel.IsLaneOpen(0)); Assert.AreEqual(revision + 1, f.Travel.Revision);
            Assert.AreEqual(1, EventBus<FactChanged>.SubscriberCount, "Rebuild removes the old watcher subscription.");
            f.Set(true); Assert.AreEqual(revision + 2, f.Travel.Revision);
            f.Travel.Dispose(); Assert.AreEqual(0, EventBus<FactChanged>.SubscriberCount); Assert.AreEqual(0, EventBus<StarSystemEntered>.SubscriberCount);
            f.Set(false); Assert.AreEqual(revision + 2, f.Travel.Revision);
        }
        [Test] public void Queries_NoAlloc()
        {
            using var f = new Fixture(); for (int i = 0; i < 8; i++) { f.Travel.IsLaneOpen(0); f.Travel.GetLockedMessage(0); f.Travel.GetFuelCost(0); _ = f.Travel.Revision; }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) { f.Travel.IsLaneOpen(0); f.Travel.GetLockedMessage(0); f.Travel.GetFuelCost(0); _ = f.Travel.Revision; }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
