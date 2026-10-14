using System;
using System.Text.RegularExpressions;
using Game.App.Services;
using Game.App.Session;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Simulation.Commands;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Galaxy.Travel;
using Game.Voxel.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.App
{
    /// <summary>TRV-011: compiled dialog effect routes once to the real two-system travel owner, without running a load.</summary>
    public sealed class LogicWorldAdapterTravelTests
    {
        sealed class Loader : ITravelLoader
        {
            public bool IsReady => true;
            public StarSystemId Loaded { get; set; }
            public bool IsArrivalReady => true;
            public bool IsAreaReady(double3 position) => true;
            public bool BeginStarSystemChange(StarSystemId target, double3 position, float yaw, StarSystemEntryReason reason)
                => throw new InvalidOperationException("Routing cases must not execute the streaming timeline.");
        }
        sealed class Audio : IAudioService
        {
            internal int Cues;
            public void PlayCue(ContentId cue) => Cues++;
            public void PlayCueAt(ContentId cue, float3 position, float volume) { }
            public void PlayUi(UiSound sound) { }
            public void PlayStinger(ContentId cue) { }
            public void SetMusicOverride(ContentId cue, float seconds) { }
            public void ClearMusicOverride(float seconds) { }
        }
        sealed class Fixture : IDisposable
        {
            internal readonly Audio Sound = new();
            internal readonly DockingService Docking = new();
            internal readonly LaneAccessTests.Fixture Base;
            internal readonly LogicWorldAdapter Adapter;
            internal readonly LogicContext Context;
            internal readonly StarSystemId Target;
            internal readonly Entity Player;
            internal readonly string Command;
            readonly GameSession session;
            readonly BlueprintLibrary blueprints;
            internal Fixture()
            {
                var loader = new Loader();
                Base = new LaneAccessTests.Fixture(loader: loader, audio: Sound, docking: Docking);
                loader.Loaded = Base.Galaxy.Galaxy.StartStarSystem;
                foreach (var system in Base.Galaxy.Galaxy.StarSystems)
                    if (!system.Id.Equals(loader.Loaded)) Target = system.Id;
                Assert.IsTrue(Target.IsValid);
                var definition = Base.Galaxy.Galaxy.Authored[Base.Galaxy.Galaxy.Get(Target).AuthoredIndex - 1];
                Command = "request_lane_jump " + definition.IdString;
                Player = Base.World.EntityManager.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
                Base.World.EntityManager.SetComponentData(Player, LocalTransform.Identity);
                session = new GameSession(Base.World, TestContentFactory.Create<GameConfigDefinition>("config_test_travel_adapter"));
                blueprints = new BlueprintLibrary(Base.Catalog, default);
                Adapter = new LogicWorldAdapter(Base.Catalog, session, Base.Galaxy, blueprints, Sound, null, Base.World,
                    TestContentFactory.Create<NarrativeConfigDefinition>("config_test_travel_narrative"));
                Context = new LogicContext(Base.Logic.Store, Base.Logic.Reads, Base.Logic.Reads, Adapter, () => 42);
                Services.Register(Base.Travel);
            }
            internal void ApplyDialogCommand()
            {
                var refs = RefResolverRegistry.CreateDefault(Base.Catalog);
                var report = new ValidationReport("TRV-011 dialog command");
                var compiler = new LogicCompiler(Base.Facts, refs, new LogicFunctionRegistry());
                Assert.IsTrue(compiler.TryCompileCommand(Command,
                    new LogicCompileOptions(LogicUsage.DialogCommand, "test:travel", "command"), report, out var effect), report.ToText());
                effect.Validate(report); effect.Bind(new LogicBinder(Base.Facts, refs, report));
                Assert.IsFalse(report.HasErrors, report.ToText());
                Assert.AreEqual(EffectFlow.Terminal, effect.Flow);
                var scope = new LogicScope(LogicUsage.DialogCommand, 0, 0, 0, false, default);
                Context.PushScope(scope);
                try { effect.Apply(Context); }
                finally { Context.PopScope(); }
            }
            public void Dispose()
            {
                Services.Unregister(Base.Travel);
                Adapter.Dispose(); blueprints.Dispose(); session.Dispose(); Base.Dispose();
            }
        }
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup()
        { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }

        [Test] public void RequestLaneJump_Routes()
        {
            using var f = new Fixture();
            // The lane is fact-locked and the ship docked: Narrative keeps its specified bypass/undock semantics.
            Assert.IsFalse(f.Base.Travel.IsLaneOpen(0));
            f.Docking.Dock(new BodyId(f.Base.Galaxy.State.CurrentStarSystem, BodyRole.Station, 0, 0));
            f.ApplyDialogCommand();
            Assert.AreEqual(TravelPhase.Spooling, f.Base.Travel.Phase);
            Assert.AreEqual(f.Target, f.Base.Travel.Target);
            Assert.AreEqual(1, f.Sound.Cues, "Exactly one accepted RequestJump emits exactly one depart cue.");
            Assert.IsFalse(f.Docking.IsDocked);
            Assert.AreEqual(0, EventBus<TravelNotice>.PendingCount);
            CollectionAssert.DoesNotContain(LogicWorldAdapter.UnwiredMembers, nameof(LogicWorldAdapter.RequestLaneJump));
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void Refused_WarnsAndNotifies()
        {
            using var f = new Fixture();
            f.Base.World.EntityManager.DestroyEntity(f.Player);
            int notices = 0; TravelNotice observed = default;
            Action<TravelNotice> handler = notice => { notices++; observed = notice; };
            EventBus<TravelNotice>.Subscribe(handler);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("\\[Narrative\\] request_lane_jump refused: Your ship cannot jump\\."));
                f.ApplyDialogCommand();
                Assert.AreEqual(1, EventBus<TravelNotice>.PendingCount);
                EventHub.DrainAll();
                Assert.AreEqual(1, notices);
                Assert.AreEqual(TravelNoticeKind.JumpRefused, observed.Kind);
                Assert.AreEqual(f.Target.Value, observed.StarSystemValue);
                Assert.AreEqual("Your ship cannot jump.", observed.Text);
                Assert.AreEqual(TravelPhase.Idle, f.Base.Travel.Phase);
                Assert.AreEqual(0, f.Sound.Cues);
                LogAssert.NoUnexpectedReceived();
            }
            finally { EventBus<TravelNotice>.Unsubscribe(handler); }
        }
    }
}
