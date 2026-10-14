using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    /// <summary>TRV-010's five required cases: two/three live systems, real facts/watchers/planner, direct ticks.</summary>
    public sealed class DestinationTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }
        sealed class Loader : ITravelLoader
        {
            public StarSystemId Loaded { get; set; }
            public bool IsReady => true;
            public bool IsArrivalReady => true;
            public bool IsAreaReady(double3 positionM) => true;
            public bool BeginStarSystemChange(StarSystemId target, double3 positionM, float yaw, StarSystemEntryReason reason)
                => throw new NotSupportedException("Destination tests never execute travel.");
        }
        sealed class Fixture : IDisposable
        {
            internal readonly Loader Load = new();
            internal readonly LaneAccessTests.Fixture Base;
            internal TravelService Travel => Base.Travel;
            internal GalaxyService Galaxy => Base.Galaxy;
            internal GalaxyState State => Base.State;
            internal readonly StarSystemId Start, Middle, Target;
            internal Fixture(bool generate = true, int systems = 2)
            {
                Base = new LaneAccessTests.Fixture(generate: false, loader: Load, authoredCount: systems);
                if (!generate) return;
                // Three collinear systems must use both adjacent lanes, not the default 12 Ly shortcut.
                TestContentFactory.Edit(Base.Content.Generation, serialized => TestContentFactory.Set(serialized, "maxLaneLengthLy", 8f));
                Galaxy.NewGame(42);
                Start = State.CurrentStarSystem; Load.Loaded = Start;
                Middle = StarSystemId.FromAuthored(Base.Content.Generation.AuthoredStarSystems[1].Id.Hash);
                Target = StarSystemId.FromAuthored(Base.Content.Generation.AuthoredStarSystems[systems - 1].Id.Hash);
                foreach (var system in Galaxy.Galaxy.StarSystems) State.AddKnown(system.Id);
                Base.Set(true);
            }
            internal void Tick(uint tick = 1) => Travel.Tick(new GalaxyFrameContext(Base.World.EntityManager, 0, tick, 0, (int)tick));
            internal int Lane(StarSystemId a, StarSystemId b)
            { Assert.IsTrue(Galaxy.Galaxy.TryFindLane(a, b, out int lane)); return lane; }
            public void Dispose() => Base.Dispose();
        }

        [Test] public void CanSet_Reasons()
        {
            using (var empty = new Fixture(false))
            {
                Assert.IsFalse(empty.Travel.CanSetDestination(new StarSystemId(1), out string reason)); Assert.AreEqual("No galaxy is loaded.", reason);
            }
            using var f = new Fixture();
            Assert.IsFalse(f.Travel.CanSetDestination(default, out string unknown)); Assert.AreEqual("Uncharted star system.", unknown);
            Assert.IsFalse(f.Travel.CanSetDestination(f.Start, out string same)); Assert.AreEqual("You are already there.", same);
            f.State.Clear(); f.State.AddKnown(f.Start);
            Assert.IsFalse(f.Travel.CanSetDestination(f.Target, out string uncharted)); Assert.AreEqual("Uncharted star system.", uncharted);
            f.State.AddKnown(f.Target); f.Base.Set(false);
            Assert.IsFalse(f.Travel.CanSetDestination(f.Target, out string locked)); Assert.AreEqual("Gate is asleep.", locked);
            Assert.IsFalse(f.Travel.SetDestination(f.Target, out _)); Assert.IsFalse(f.Travel.Destination.IsValid);
            Assert.AreEqual(0, f.Travel.Stats.RouteComputations, "Validation queries do not publish or compute the displayed route.");
            f.Base.Set(true); int lane = f.Lane(f.Start, f.Target);
            var edge = f.Galaxy.Galaxy.Lanes[lane]; edge.Flags |= LaneFlags.HiddenUntilKnown; f.Galaxy.Galaxy.Lanes[lane] = edge;
            Assert.IsFalse(f.Travel.CanSetDestination(f.Target, out string absent)); Assert.AreEqual("No known route.", absent);
            f.State.AddVisited(f.Start); f.State.AddVisited(f.Target);
            Assert.IsTrue(f.Travel.CanSetDestination(f.Target, out string ok)); Assert.AreEqual("", ok);
        }

        [Test] public void Arrival_ClearsAndNotifies()
        {
            using var f = new Fixture(); var notices = new List<TravelNotice>(); EventBus<TravelNotice>.Subscribe(notices.Add);
            Assert.IsTrue(f.Travel.SetDestination(f.Target, out _)); f.Tick(); Assert.AreEqual(0, notices.Count);
            f.Load.Loaded = f.Target;
            EventBus<StarSystemEntered>.Publish(new StarSystemEntered(f.Target.Value, StarSystemEntryReason.Jump, 1)); EventHub.DrainAll();
            Assert.AreEqual(f.Target, f.Travel.Destination, "The event refreshes watchers; the loaded stamp updates destination in Tick.");
            f.Tick(); Assert.IsFalse(f.Travel.Destination.IsValid); Assert.AreEqual(RouteStatus.None, f.Travel.RouteStatus);
            Assert.AreEqual(0, f.Travel.Route.Count); Assert.AreEqual(0, f.Travel.RouteFuelCost); Assert.IsFalse(f.Travel.TryGetNextHop(out _));
            EventBus<TravelNotice>.Drain(); Assert.AreEqual(1, notices.Count);
            Assert.AreEqual(TravelNoticeKind.DestinationReached, notices[0].Kind);
            Assert.AreEqual(f.Target.Value, notices[0].StarSystemValue);
            Assert.AreEqual("Arrived at " + f.Galaxy.GetStarSystemName(f.Target) + ".", notices[0].Text);
            f.Tick(2); f.Tick(3); EventBus<TravelNotice>.Drain(); Assert.AreEqual(1, notices.Count);
        }

        [Test] public void LaneClose_BlocksAndRestores()
        {
            using var f = new Fixture(); var notices = new List<TravelNotice>(); EventBus<TravelNotice>.Subscribe(notices.Add);
            Assert.IsTrue(f.Travel.SetDestination(f.Target, out _)); Assert.AreEqual(RouteStatus.Ok, f.Travel.RouteStatus);
            f.Base.Set(false); f.Tick(); EventBus<TravelNotice>.Drain();
            Assert.AreEqual(RouteStatus.Blocked, f.Travel.RouteStatus); Assert.AreEqual("Gate is asleep.", f.Travel.RouteBlockedReason);
            Assert.AreEqual(0, f.Travel.Route.Count); Assert.AreEqual(0, f.Travel.RouteFuelCost); Assert.IsFalse(f.Travel.TryGetNextHop(out _));
            Assert.AreEqual(1, notices.Count); Assert.AreEqual(TravelNoticeKind.RouteBlocked, notices[0].Kind);
            Assert.AreEqual(f.Target.Value, notices[0].StarSystemValue); Assert.AreEqual(f.Travel.RouteBlockedReason, notices[0].Text);
            f.Tick(2); EventBus<TravelNotice>.Drain(); Assert.AreEqual(1, notices.Count);
            Assert.IsTrue(f.Travel.SetDestination(f.Target, out _), "Setting the same destination remains a no-op while blocked.");
            f.Base.Set(true); f.Tick(3); EventBus<TravelNotice>.Drain();
            Assert.AreEqual(RouteStatus.Ok, f.Travel.RouteStatus); Assert.AreEqual("", f.Travel.RouteBlockedReason);
            Assert.AreEqual(2, notices.Count); Assert.AreEqual(TravelNoticeKind.RouteRestored, notices[1].Kind);
            Assert.AreEqual("Route to " + f.Galaxy.GetStarSystemName(f.Target) + " is open.", notices[1].Text);
            // Visibility loss uses the same blocked/restored notice transitions, with its distinct unreachable status.
            int lane = f.Lane(f.Start, f.Target); var edge = f.Galaxy.Galaxy.Lanes[lane]; edge.Flags |= LaneFlags.HiddenUntilKnown; f.Galaxy.Galaxy.Lanes[lane] = edge;
            f.State.AddKnown(new StarSystemId(123)); f.Tick(4); EventBus<TravelNotice>.Drain();
            Assert.AreEqual(RouteStatus.Unreachable, f.Travel.RouteStatus); Assert.AreEqual("No known route.", f.Travel.RouteBlockedReason);
            Assert.AreEqual(3, notices.Count); Assert.AreEqual(TravelNoticeKind.RouteBlocked, notices[2].Kind);
            f.State.AddVisited(f.Target); f.Tick(5); EventBus<TravelNotice>.Drain();
            Assert.AreEqual(RouteStatus.Ok, f.Travel.RouteStatus); Assert.AreEqual(4, notices.Count); Assert.AreEqual(TravelNoticeKind.RouteRestored, notices[3].Kind);
        }

        [Test] public void Recompute_OnlyOnStampChange()
        {
            using var f = new Fixture(); var route = f.Travel.Route; f.Tick(); Assert.AreEqual(0, f.Travel.Stats.RouteComputations);
            Assert.IsTrue(f.Travel.SetDestination(f.Target, out _)); Assert.AreEqual(1, f.Travel.Stats.RouteComputations);
            f.Tick(); f.Tick(2); Assert.AreEqual(1, f.Travel.Stats.RouteComputations); Assert.AreSame(route, f.Travel.Route);
            Assert.IsTrue(f.Travel.SetDestination(f.Target, out _)); Assert.AreEqual(1, f.Travel.Stats.RouteComputations);
            f.State.AddVisited(f.Target); f.Base.Set(false); f.Tick(3); Assert.AreEqual(2, f.Travel.Stats.RouteComputations, "Multiple changed stamps coalesce in one tick.");
            f.Tick(4); Assert.AreEqual(2, f.Travel.Stats.RouteComputations);
            f.Base.Set(true); f.Tick(5); Assert.AreEqual(3, f.Travel.Stats.RouteComputations);
            f.Galaxy.OnSessionRestored(); f.Tick(6); Assert.AreEqual(4, f.Travel.Stats.RouteComputations);
            f.Tick(7); Assert.AreEqual(4, f.Travel.Stats.RouteComputations);
            f.State.RouteDestination = f.Start; f.Load.Loaded = f.Target; f.Tick(8); Assert.AreEqual(5, f.Travel.Stats.RouteComputations);
            CollectionAssert.AreEqual(new[] { f.Start }, f.Travel.Route); Assert.GreaterOrEqual(f.Travel.Stats.LastRouteMs, 0);
            f.Travel.ClearDestination(); f.Tick(9); Assert.AreEqual(5, f.Travel.Stats.RouteComputations);
            Assert.AreEqual(RouteStatus.None, f.Travel.RouteStatus); Assert.AreEqual("", f.Travel.RouteBlockedReason); Assert.AreEqual(0, route.Count);
            f.State.RouteDestination = new StarSystemId(123); f.Tick(10); Assert.IsFalse(f.Travel.Destination.IsValid);
            Assert.AreEqual(RouteStatus.None, f.Travel.RouteStatus); Assert.AreEqual(5, f.Travel.Stats.RouteComputations);
        }

        [Test] public void NextHop_Gate()
        {
            using var f = new Fixture(systems: 3);
            Assert.IsTrue(f.Travel.SetDestination(f.Target, out _)); CollectionAssert.AreEqual(new[] { f.Middle, f.Target }, f.Travel.Route);
            Assert.IsTrue(f.Travel.TryGetNextHop(out var hop)); int first = f.Lane(f.Start, f.Middle), second = f.Lane(f.Middle, f.Target);
            Assert.AreEqual(f.Middle, hop.Next); Assert.AreEqual(first, hop.LaneIndex);
            Assert.AreEqual(f.Travel.GetFuelCost(first) + f.Travel.GetFuelCost(second), f.Travel.RouteFuelCost);
            bool found = false;
            foreach (var body in f.Galaxy.GetContent(f.Start).Bodies)
                if (body.Id.Role == BodyRole.LaneGate && body.LaneIndex == first)
                { Assert.AreEqual(body.Id.LocalKey, hop.GateKey); Assert.AreEqual(OrbitMath.Position(body, 0), hop.GatePositionM); found = true; }
            Assert.IsTrue(found); f.Tick(600); Assert.IsTrue(f.Travel.TryGetNextHop(out var later)); Assert.AreEqual(hop.GatePositionM, later.GatePositionM);
            Assert.AreEqual(1, f.Travel.Stats.RouteComputations); f.Travel.ClearDestination(); Assert.IsFalse(f.Travel.TryGetNextHop(out _));
        }
    }
}
