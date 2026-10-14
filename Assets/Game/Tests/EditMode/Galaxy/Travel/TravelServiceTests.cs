using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Simulation.Commands;
using Game.Tests.EditMode.Data;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    public sealed class TravelServiceTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }
        sealed class Loader : ITravelLoader
        {
            public bool IsReady { get; set; } = true;
            public StarSystemId Loaded { get; set; }
            public bool IsArrivalReady { get; set; }
            public bool Accept = true;
            public int Calls;
            public StarSystemId Requested;
            public bool IsAreaReady(double3 p) => IsArrivalReady;
            public bool BeginStarSystemChange(StarSystemId target, double3 p, float yaw, StarSystemEntryReason reason)
            { Calls++; Requested = target; if (Accept) IsReady = false; return Accept; }
        }
        sealed class Pause : IPauseService
        {
            readonly HashSet<uint> tokens = new(); uint next;
            public bool IsPaused => tokens.Count != 0;
            public int ActiveTokenCount => tokens.Count;
            public PauseToken Acquire(string reason) { uint id = ++next; tokens.Add(id); return new PauseToken(this, id); }
            public void Release(PauseToken token) => tokens.Remove(token.Id);
            public void GetActiveReasons(List<string> into) { into.Clear(); foreach (var _ in tokens) into.Add("Travel"); }
        }
        sealed class Audio : IAudioService
        {
            public int Cues;
            public void PlayCue(ContentId cue) => Cues++;
            public void PlayUi(UiSound sound) { }
            public void PlayCueAt(ContentId cue, float3 p, float volume) { }
            public void PlayStinger(ContentId cue) { }
            public void SetMusicOverride(ContentId cue, float seconds) { }
            public void ClearMusicOverride(float seconds) { }
        }
        sealed class Economy : ILogicEconomy
        {
            public int Held = 20, Takes;
            public int GetItemCount(int item) => Held;
            public bool TryTakeItems(int item, int count) { Takes++; if (Held < count) return false; Held -= count; return true; }
            public long GetCredits() => 0;
            public void GiveItems(int item, int count) => Held += count;
            public void GiveCredits(long amount) { }
            public bool TryTakeCredits(long amount) => false;
            public void GiveLoot(int table, int picks, ulong salt) { }
            public event Action<int> ItemCountChanged { add { } remove { } }
            public event Action CreditsChanged { add { } remove { } }
        }
        sealed class Fixture : IDisposable
        {
            public readonly Loader Loader = new(); public readonly Pause Pause = new();
            public readonly Audio Audio = new(); public readonly Economy Economy = new();
            public readonly DockingService Docking = new();
            public readonly LaneAccessTests.Fixture Base;
            public readonly Entity Player, Clock;
            public TravelService Travel => Base.Travel;
            public EntityManager Em => Base.World.EntityManager;
            public StarSystemId Target;
            public int Jumps, Notices;
            int frame;
            public Fixture(bool generate = true)
            {
                Base = new LaneAccessTests.Fixture(generate: generate, loader: Loader, pauseOverride: Pause, audio: Audio, docking: Docking);
                Base.Logic.EconomyOverride = Economy; Base.Set(true);
                Player = Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
                Em.SetComponentData(Player, LocalTransform.Identity);
                Clock = Em.CreateEntity(typeof(SimClock));
                if (generate)
                { Loader.Loaded = Base.Galaxy.Galaxy.StarSystems[0].Id; Target = Base.Galaxy.Galaxy.StarSystems[1].Id; }
                EventBus<LaneJumped>.Subscribe(OnJump); EventBus<TravelNotice>.Subscribe(OnNotice);
            }
            void OnJump(LaneJumped e) { Assert.AreEqual(Target.Value, e.ToStarSystemValue); Jumps++; }
            void OnNotice(TravelNotice e) { Assert.AreEqual(TravelNoticeKind.JumpAborted, e.Kind); Notices++; }
            public void Tick(uint tick, float seconds = 0)
            { Em.SetComponentData(Clock, new SimClock { Tick = tick }); Travel.Tick(new GalaxyFrameContext(Em, 0, tick, seconds, ++frame)); EventHub.DrainAll(); }
            public void Start() => Assert.IsTrue(Travel.RequestJump(Target, JumpSource.Gate).Accepted);
            public void Commit() { Start(); Tick(30); Assert.AreEqual(TravelPhase.Transit, Travel.Phase); }
            public void Ready() { Loader.Loaded = Target; Loader.IsReady = Loader.IsArrivalReady = true; Tick(30, .25f); Tick(30, .25f); }
            public PlayerControlLockFlags Lock { get { using var q = Em.CreateEntityQuery(typeof(PlayerControlLock)); return q.GetSingleton<PlayerControlLock>().Flags; } }
            public void Dispose() { Base.Dispose(); EventBus<LaneJumped>.Unsubscribe(OnJump); EventBus<TravelNotice>.Unsubscribe(OnNotice); }
        }
        [Test] public void Refusals_OrderAndText()
        {
            using (var empty = new Fixture(false)) Check(empty, JumpRefusal.NoGalaxy, "No galaxy is loaded.");
            using var f = new Fixture();
            using var q = f.Em.CreateEntityQuery(typeof(WorldSceneActive)); f.Em.DestroyEntity(q);
            Check(f, JumpRefusal.NotInWorld, "You can only travel in space."); f.Em.CreateEntity(typeof(WorldSceneActive));
            f.Loader.IsReady = false; Check(f, JumpRefusal.Busy, "Already travelling."); f.Loader.IsReady = true;
            var target = f.Target; f.Target = default; Check(f, JumpRefusal.UnknownTarget, "Unknown destination.");
            f.Target = f.Loader.Loaded; Check(f, JumpRefusal.SameStarSystem, "You are already there."); f.Target = target;
            f.Em.AddComponent<Hulk>(f.Player); Check(f, JumpRefusal.NoPlayerShip, "Your ship cannot jump."); f.Em.RemoveComponent<Hulk>(f.Player);
            f.Docking.Dock(new BodyId(f.Loader.Loaded, BodyRole.Station, 0, 0)); Check(f, JumpRefusal.Docked, "Undock first."); f.Docking.Undock();
            var lane = f.Base.Galaxy.Galaxy.Lanes[0]; var removed = lane; removed.B = default;
            f.Base.Galaxy.Galaxy.Lanes[0] = removed; Check(f, JumpRefusal.NoLane, "No lane leads there from here."); f.Base.Galaxy.Galaxy.Lanes[0] = lane;
            f.Base.Set(false); Check(f, JumpRefusal.LaneLocked, "Gate is asleep."); f.Base.Set(true);
            f.Economy.Held = 0; Check(f, JumpRefusal.NotEnoughFuel, "Not enough fuel: 3 needed, 0 in the hold.");
            Assert.IsTrue(f.Travel.CanJump(f.Target, JumpSource.Dev).Accepted);
        }
        static void Check(Fixture f, JumpRefusal refusal, string text)
        { var r = f.Travel.CanJump(f.Target, JumpSource.Gate); Assert.IsFalse(r.Accepted); Assert.AreEqual(refusal, r.Refusal); Assert.AreEqual(text, r.Reason); }
        [Test] public void CanJump_NoSideEffects()
        {
            using var f = new Fixture(); f.Docking.Dock(new BodyId(f.Loader.Loaded, BodyRole.Station, 0, 0));
            int generated = f.Base.Galaxy.ContentGenerationCount(f.Target), subscribers = EventBus<FactChanged>.SubscriberCount;
            for (int i = 0; i < 3; i++) Assert.IsTrue(f.Travel.CanJump(f.Target, JumpSource.Narrative).Accepted);
            Assert.IsTrue(f.Docking.IsDocked); Assert.AreEqual(0, f.Loader.Calls); Assert.AreEqual(0, f.Audio.Cues);
            Assert.AreEqual(0, f.Pause.ActiveTokenCount); Assert.AreEqual(0, f.Economy.Takes); Assert.AreEqual(PlayerControlLockFlags.None, f.Lock);
            Assert.AreEqual(generated, f.Base.Galaxy.ContentGenerationCount(f.Target)); Assert.AreEqual(subscribers, EventBus<FactChanged>.SubscriberCount);
        }
        [Test] public void GateJump_FullTimeline()
        {
            using var f = new Fixture(); f.Start(); Assert.IsTrue(f.Travel.CoverRequested);
            Assert.AreEqual(PlayerControlLockFlags.Movement | PlayerControlLockFlags.Actions, f.Lock);
            f.Tick(29); Assert.AreEqual(0, f.Loader.Calls); f.Tick(30); f.Tick(30);
            Assert.AreEqual(1, f.Loader.Calls); Assert.AreEqual(1, f.Economy.Takes); Assert.AreEqual(17, f.Economy.Held);
            Assert.AreEqual(1, f.Pause.ActiveTokenCount); f.Ready();
            Assert.AreEqual(TravelPhase.Arriving, f.Travel.Phase); Assert.AreEqual(0, f.Pause.ActiveTokenCount); Assert.IsFalse(f.Travel.CoverRequested);
            Assert.AreEqual(1, f.Jumps); Assert.AreEqual(2, f.Audio.Cues); f.Tick(59); Assert.AreEqual(TravelPhase.Arriving, f.Travel.Phase);
            f.Tick(60); Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase); Assert.AreEqual(PlayerControlLockFlags.None, f.Lock);
            Assert.AreEqual(1, f.Travel.Stats.JumpsCompleted);
        }
        [Test] public void Reveal_WaitsForArea()
        {
            using var f = new Fixture();
            using (var config = new UnityEditor.SerializedObject(f.Travel.Config))
            { config.FindProperty("revealDelayFrames").intValue = 0; config.ApplyModifiedPropertiesWithoutUndo(); }
            f.Commit(); f.Tick(30, .5f);
            using (var config = new UnityEditor.SerializedObject(f.Travel.Config))
            { config.FindProperty("revealDelayFrames").intValue = 2; config.ApplyModifiedPropertiesWithoutUndo(); } Assert.AreEqual(TravelPhase.Transit, f.Travel.Phase);
            f.Loader.Loaded = f.Target; f.Loader.IsReady = f.Loader.IsArrivalReady = true; f.Tick(30);
            Assert.AreEqual(TravelPhase.Transit, f.Travel.Phase); f.Loader.IsArrivalReady = false; f.Tick(30);
            f.Loader.IsArrivalReady = true; f.Tick(30); Assert.AreEqual(TravelPhase.Transit, f.Travel.Phase);
            f.Tick(30); Assert.AreEqual(TravelPhase.Arriving, f.Travel.Phase);
        }
        [Test] public void Timeout_LogsOnce()
        {
            using var f = new Fixture(); f.Commit(); LogAssert.Expect(LogType.Error, new Regex("TRV.TransitTimeout"));
            f.Tick(30, 15); f.Tick(30, 1); Assert.AreEqual(1, f.Travel.Stats.TransitTimeouts);
            Assert.IsTrue(f.Travel.CoverRequested); f.Loader.Loaded = f.Target; f.Loader.IsReady = true;
            f.Tick(30); Assert.AreEqual(TravelPhase.Arriving, f.Travel.Phase);
        }
        [Test] public void Abort_NoFuelNoToken()
        {
            for (int reason = 0; reason < 3; reason++)
            {
                using var f = new Fixture(); f.Start();
                if (reason == 0) f.Em.AddComponent<Hulk>(f.Player);
                if (reason == 1) f.Economy.Held = 0;
                if (reason == 2) f.Loader.Accept = false;
                f.Tick(30); Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase); Assert.AreEqual(0, f.Economy.Takes);
                Assert.AreEqual(0, f.Pause.ActiveTokenCount); Assert.AreEqual(PlayerControlLockFlags.None, f.Lock); Assert.AreEqual(1, f.Notices);
            }
        }
        [Test] public void SceneExit_EachPhase()
        {
            for (int phase = 0; phase < 4; phase++)
            {
                using var f = new Fixture(); if (phase > 0) f.Start(); if (phase > 1) f.Tick(30); if (phase > 2) f.Ready();
                f.Travel.OnSceneExiting(default); EventHub.DrainAll();
                Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase); Assert.AreEqual(0, f.Pause.ActiveTokenCount);
                Assert.AreEqual(PlayerControlLockFlags.None, f.Lock); Assert.IsFalse(f.Travel.CoverRequested); Assert.AreEqual(0, f.Notices);
            }
        }
        [Test] public void SaveBlock_WhileTravelling()
        {
            using var f = new Fixture(); Assert.AreEqual(SaveBlockReason.None, f.Travel.GetSaveBlock()); f.Start();
            Assert.AreEqual(SaveBlockReason.Transitioning, f.Travel.GetSaveBlock()); f.Tick(30);
            Assert.AreEqual(SaveBlockReason.Transitioning, f.Travel.GetSaveBlock()); f.Ready();
            Assert.AreEqual(SaveBlockReason.Transitioning, f.Travel.GetSaveBlock()); f.Tick(60); Assert.AreEqual(SaveBlockReason.None, f.Travel.GetSaveBlock());
        }
        [Test, Category("Performance")] public void Tick_Idle_NoAlloc()
        {
            using var f = new Fixture(); var ctx = new GalaxyFrameContext(f.Em, 0, 0, 0, 0);
            for (int i = 0; i < 8; i++) f.Travel.Tick(ctx);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long baseline = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) f.Travel.Tick(ctx);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - baseline; watch.Stop();
            if (bytes != 0 || watch.Elapsed.TotalMilliseconds / 64 > .02)
                Assert.Warn($"Travel idle: {bytes} B; {watch.Elapsed.TotalMilliseconds / 64:F6} ms/tick; targets 0 B / 0.02 ms.");
            Game.Tests.Common.Perf.PerfRun.Gate("TravelIdle", new[] { watch.Elapsed.TotalMilliseconds / 64 }, .02);
        }
    }
}
