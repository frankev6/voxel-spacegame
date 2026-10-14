using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Economy;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    public sealed class GateInteractionTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }

        sealed class Loader : ITravelLoader
        {
            public StarSystemId Loaded { get; set; }
            public bool IsReady => true;
            public bool IsArrivalReady => false;
            public bool IsAreaReady(double3 position) => false;
            public bool BeginStarSystemChange(StarSystemId target, double3 position, float yaw, StarSystemEntryReason reason)
                => throw new NotSupportedException("Gate interaction cases stop at the accepted spool request.");
        }
        sealed class Audio : IAudioService
        {
            internal int Cues;
            public void PlayCue(ContentId cue) => Cues++;
            public void PlayUi(UiSound sound) => throw new NotSupportedException();
            public void PlayCueAt(ContentId cue, float3 position, float volume) => throw new NotSupportedException();
            public void PlayStinger(ContentId cue) => throw new NotSupportedException();
            public void SetMusicOverride(ContentId cue, float seconds) => throw new NotSupportedException();
            public void ClearMusicOverride(float seconds) => throw new NotSupportedException();
        }
        sealed class Fixture : IDisposable
        {
            internal readonly Loader Loader = new();
            internal readonly Audio Audio = new();
            internal readonly LaneAccessTests.Fixture Base;
            internal readonly EconomyService Economy;
            internal readonly List<TravelNotice> Notices = new();
            internal readonly BodySpec Gate;
            internal readonly StarSystemId Target;
            internal readonly int FuelIndex;
            internal TravelService Travel => Base.Travel;
            internal Fixture(bool hidden = false)
            {
                Base = new LaneAccessTests.Fixture(generate: false, loader: Loader, audio: Audio);
                if (hidden)
                    foreach (var definition in Base.Content.Definitions)
                        if (definition is AuthoredStarSystemDefinition system && system != Base.Content.Start)
                        {
                            using var so = new SerializedObject(system);
                            TestContentFactory.Set(so, "hiddenUntilKnown", true); so.ApplyModifiedPropertiesWithoutUndo();
                        }
                Base.Galaxy.NewGame(42); Loader.Loaded = Base.State.CurrentStarSystem;
                foreach (var body in Base.Galaxy.GetContent(Loader.Loaded).Bodies)
                    if (body.Id.Role == BodyRole.LaneGate) { Gate = body; break; }
                Assert.AreEqual(BodyRole.LaneGate, Gate.Id.Role);
                Target = GalaxyDef.Neighbour(Base.Galaxy.Galaxy.Lanes[Gate.LaneIndex], Loader.Loaded);
                var config = TestContentFactory.Create<EconomyConfigDefinition>("config_gate_economy");
                Economy = new EconomyService(Base.Catalog, config, () => 42, () => 0);
                Base.Logic.EconomyOverride = Economy;
                FuelIndex = Base.Catalog.IndexOf(Travel.Config.FuelItem); Economy.GiveItems(FuelIndex, 20);
                var em = Base.World.EntityManager;
                em.CreateEntity(typeof(SimClock));
                var player = em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
                em.SetComponentData(player, LocalTransform.Identity);
                Base.Set(true); EventBus<TravelNotice>.Subscribe(OnNotice);
            }
            void OnNotice(TravelNotice notice) => Notices.Add(notice);
            internal void Interact(InteractionKind kind = InteractionKind.LaneGate, StarSystemId system = default)
            {
                EventBus<BodyInteracted>.Publish(new BodyInteracted(system.IsValid ? system.Value : Loader.Loaded.Value,
                    Gate.Id.LocalKey, (byte)kind, 0));
                EventHub.DrainAll();
            }
            public void Dispose() { EventBus<TravelNotice>.Unsubscribe(OnNotice); Base.Dispose(); }
        }

        [Test] public void Gate_RequestsNeighbour()
        {
            using var f = new Fixture(); f.Interact();
            Assert.AreEqual(TravelPhase.Spooling, f.Travel.Phase);
            Assert.AreEqual(TravelKind.LaneJump, f.Travel.Kind);
            Assert.AreEqual(f.Target, f.Travel.Target); Assert.IsTrue(f.Travel.CoverRequested);
            Assert.AreEqual(1, f.Audio.Cues); Assert.AreEqual(0, f.Notices.Count);
            Assert.AreEqual(20, f.Economy.GetItemCount(f.FuelIndex), "Gate interaction must not pay before commit.");
        }
        [Test] public void OtherKinds_Ignored()
        {
            using var f = new Fixture();
            f.Interact(InteractionKind.Dock); f.Interact(system: f.Target);
            Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase);
            Assert.AreEqual(0, f.Audio.Cues); Assert.AreEqual(0, f.Notices.Count);
            Assert.IsFalse(f.Travel.CoverRequested);
            Assert.AreEqual(0, f.Base.Galaxy.ContentGenerationCount(f.Target));
        }
        [Test] public void Locked_OneNotice()
        {
            using var f = new Fixture(); f.Base.Set(false); f.Interact();
            Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase); Assert.AreEqual(0, f.Audio.Cues);
            Assert.AreEqual(1, f.Notices.Count);
            Assert.AreEqual(TravelNoticeKind.JumpRefused, f.Notices[0].Kind);
            Assert.AreEqual(f.Target.Value, f.Notices[0].StarSystemValue);
            Assert.AreEqual("Gate is asleep.", f.Notices[0].Text);
        }
        [Test] public void Busy_Silent()
        {
            using var f = new Fixture(); f.Interact(); f.Interact(); f.Interact();
            Assert.AreEqual(TravelPhase.Spooling, f.Travel.Phase); Assert.AreEqual(f.Target, f.Travel.Target);
            Assert.AreEqual(1, f.Audio.Cues); Assert.AreEqual(0, f.Notices.Count);
        }
        [Test] public void GateInfo_Uncharted()
        {
            using var f = new Fixture(hidden: true);
            Assert.IsFalse(f.Base.State.IsKnown(f.Target));
            Assert.IsTrue(f.Travel.TryGetGateInfo(f.Gate.Id.LocalKey, out var info));
            Assert.IsFalse(info.Known); Assert.AreEqual("Uncharted", info.TargetName);
            Assert.AreEqual(f.Target, info.Target); Assert.IsFalse(info.OnRoute);
            f.Base.Galaxy.Reveal(f.Target);
            Assert.IsTrue(f.Travel.TryGetGateInfo(f.Gate.Id.LocalKey, out info));
            Assert.IsTrue(info.Known); Assert.AreEqual(f.Base.Galaxy.GetStarSystemName(f.Target), info.TargetName);
        }
        [Test] public void GateInfo_Fields()
        {
            using var f = new Fixture();
            Assert.IsTrue(f.Travel.TryGetGateInfo(f.Gate.Id.LocalKey, out var info));
            Assert.AreEqual(f.Gate.LaneIndex, info.LaneIndex); Assert.AreEqual(f.Target, info.Target);
            Assert.IsTrue(info.Known); Assert.IsTrue(info.Open); Assert.AreEqual("", info.LockedMessage);
            Assert.AreEqual(f.Travel.GetFuelCost(info.LaneIndex), info.FuelCost);
            Assert.AreEqual(f.Economy.GetItemCount(f.FuelIndex), info.FuelHeld); Assert.IsTrue(info.CanAfford);
            Assert.IsFalse(info.OnRoute);
            // TRV-010 owns the actual route; this case requires its implementation in the integration batch.
            Assert.IsTrue(f.Travel.SetDestination(f.Target));
            Assert.IsTrue(f.Travel.TryGetNextHop(out var hop));
            Assert.IsTrue(f.Travel.TryGetGateInfo(f.Gate.Id.LocalKey, out info));
            Assert.AreEqual(hop.LaneIndex == info.LaneIndex, info.OnRoute); Assert.IsTrue(info.OnRoute);
            f.Travel.ClearDestination();
            Assert.IsTrue(f.Economy.TryTakeItems(f.FuelIndex, 20)); f.Base.Set(false);
            Assert.IsTrue(f.Travel.TryGetGateInfo(f.Gate.Id.LocalKey, out info));
            Assert.AreEqual(0, info.FuelHeld); Assert.IsFalse(info.CanAfford); Assert.IsFalse(info.Open);
            Assert.AreEqual("Gate is asleep.", info.LockedMessage); Assert.IsFalse(info.OnRoute);
            Assert.IsFalse(f.Travel.TryGetGateInfo(new BodyId(f.Loader.Loaded, BodyRole.Star, 0, 0).LocalKey, out _));
            Assert.IsFalse(f.Travel.TryGetGateInfo(ulong.MaxValue, out _));
        }
    }
}
