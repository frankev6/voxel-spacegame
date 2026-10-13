using System;
using System.Collections.Generic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class GalaxyMapModelTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();

        sealed class Session : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Fixture = new(3, 0, 42, false);
            internal readonly World World = new("Galaxy map fixture");
            internal readonly GalaxyState State = new();
            internal readonly GalaxyService Service;

            internal Session(bool startGame = true)
            {
                foreach (var definition in Fixture.Generation.AuthoredStarSystems)
                    TestContentFactory.Edit(definition, so => TestContentFactory.Set(so, "connectProcedurally", false));
                TestContentFactory.Edit(Fixture.Start, so =>
                {
                    TestContentFactory.Set(so, "isStory", true);
                    TestContentFactory.Set(so, "danger", 2);
                });
                TestContentFactory.Edit(Fixture.Generation.AuthoredStarSystems[1],
                    so => TestContentFactory.Set(so, "hiddenUntilKnown", true));
                TestContentFactory.Edit(Fixture.Generation,
                    so => so.FindProperty("authoredLanes").GetArrayElementAtIndex(0).FindPropertyRelative("hiddenUntilKnown").boolValue = true);
                var catalog = ContentCatalog.Build(Fixture.Definitions, new ValidationReport("Galaxy map"));
                Service = new GalaxyService(catalog, State, World, new GalaxyFrameTicker());
                if (startGame) Service.NewGame(42);
            }

            internal StarSystemId Id(int authoredIndex)
            {
                Assert.IsTrue(Service.TryResolveStarSystem(Fixture.Generation.AuthoredStarSystems[authoredIndex].Id, out var id));
                return id;
            }
            public void Dispose() { Service.Dispose(); State.Dispose(); World.Dispose(); Fixture.Dispose(); }
        }

        // Probes for the two optional interfaces; galaxy generation/state use the real service throughout.
        sealed class LaneAccess : ILaneAccess
        {
            public uint Revision { get; set; }
            internal bool Open;
            internal int FuelCost = 7, Queries;
            public bool IsLaneOpen(int laneIndex) { Queries++; return Open; }
            public string GetLockedMessage(int laneIndex) => "Gate sealed";
            public int GetFuelCost(int laneIndex) => FuelCost;
        }
        sealed class Ownership : IStarSystemOwnership
        {
            internal readonly List<StarSystemId> Queries = new();
            public byte GetOwnerFaction(StarSystemId id) => 1;
            public string GetOwnerColorToken(StarSystemId id) { Queries.Add(id); return "vestari"; }
        }

        static StarSystemMapEntry Find(GalaxyMapModel map, StarSystemId id)
        {
            for (int i = 0; i < map.StarSystems.Count; i++)
                if (map.StarSystems[i].Id.Equals(id)) return map.StarSystems[i];
            Assert.Fail("Expected visible star system: " + id);
            return default;
        }
        static int LaneIndex(Session session, int a, int b)
        {
            Assert.IsTrue(session.Service.Galaxy.TryFindLane(session.Id(a), session.Id(b), out int index));
            return index;
        }

        [Test] public void UnknownHidden()
        {
            using var session = new Session(false);
            var access = new LaneAccess(); var owner = new Ownership();
            var map = new GalaxyMapModel(session.Service, access, owner);
            Assert.IsEmpty(map.StarSystems); Assert.IsEmpty(map.Lanes); Assert.AreEqual(0u, map.Revision);
            session.Service.NewGame(42); map.Refresh();
            Assert.AreEqual(1, map.StarSystems.Count); Assert.IsEmpty(map.Lanes);
            var start = Find(map, session.Id(0));
            Assert.IsTrue(start.Known && start.Visited && start.Current && start.IsStory);
            Assert.IsFalse(start.IsRouteTarget); Assert.AreEqual(2, start.Danger);
            Assert.AreEqual(session.Fixture.Start.DisplayName, start.Name);
            Assert.AreEqual(session.Service.Galaxy.Get(start.Id).PositionLy, start.PositionLy);
            Assert.AreEqual("vestari", start.OwnerColorToken);
            CollectionAssert.AreEqual(new[] { start.Id }, owner.Queries);
            Assert.AreEqual(0, access.Queries, "Invisible lanes must not query access or fuel.");
            Assert.Throws<NotSupportedException>(() => ((IList<StarSystemMapEntry>)map.StarSystems).Clear());

            session.Service.Reveal(session.Id(1)); map.Refresh();
            Assert.AreEqual(2, map.StarSystems.Count); Assert.IsEmpty(map.Lanes);
            Assert.IsFalse(Find(map, session.Id(1)).Visited);
            session.Service.Reveal(session.Id(2)); map.Refresh();
            Assert.AreEqual(3, map.StarSystems.Count); Assert.AreEqual(1, map.Lanes.Count);
            var lane = map.Lanes[0]; var source = session.Service.Galaxy.Lanes[lane.LaneIndex];
            Assert.AreEqual(LaneIndex(session, 1, 2), lane.LaneIndex);
            Assert.AreEqual(source.A, lane.A); Assert.AreEqual(source.B, lane.B);
            Assert.IsFalse(lane.Open); Assert.AreEqual("Gate sealed", lane.LockedMessage); Assert.AreEqual(7, lane.FuelCost);
            foreach (var system in session.Service.Galaxy.StarSystems)
                Assert.AreEqual(0, session.Service.ContentGenerationCount(system.Id), "Map metadata must not generate body content.");
        }

        [Test] public void HiddenLane_RevealedByVisits()
        {
            using var session = new Session();
            var map = new GalaxyMapModel(session.Service, null, null);
            session.Service.Reveal(session.Id(1)); map.Refresh();
            Assert.IsEmpty(map.Lanes, "Knowing both endpoints is insufficient for a hidden lane.");
            session.Service.MarkVisited(session.Id(1)); map.Refresh();
            Assert.AreEqual(2, map.Lanes.Count, "Visiting the second endpoint reveals the hidden lane and its normal neighbour.");
            Assert.IsFalse(Find(map, session.Id(2)).Visited);
            int hiddenIndex = LaneIndex(session, 0, 1);
            bool hiddenFound = false;
            foreach (var lane in map.Lanes)
            {
                if (lane.LaneIndex == hiddenIndex) hiddenFound = true;
                Assert.IsTrue(lane.Open); Assert.AreEqual("", lane.LockedMessage); Assert.AreEqual(0, lane.FuelCost);
            }
            Assert.IsTrue(hiddenFound);
            foreach (var entry in map.StarSystems) Assert.AreEqual("", entry.OwnerColorToken);
        }

        [Test] public void Refresh_NoChange_NoAlloc()
        {
            using var session = new Session();
            session.Service.MarkVisited(session.Id(1));
            var access = new LaneAccess(); var owner = new Ownership();
            var map = new GalaxyMapModel(session.Service, access, owner);
            var systems = map.StarSystems; var lanes = map.Lanes;
            uint revision = map.Revision; int laneQueries = access.Queries, ownerQueries = owner.Queries.Count;
            map.Refresh(); map.Refresh();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) map.Refresh();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated); Assert.AreEqual(revision, map.Revision);
            Assert.AreSame(systems, map.StarSystems); Assert.AreSame(lanes, map.Lanes);
            Assert.AreEqual(laneQueries, access.Queries); Assert.AreEqual(ownerQueries, owner.Queries.Count);
        }

        [Test] public void Revision_ChangesOnVisit()
        {
            using var session = new Session(); var access = new LaneAccess();
            var map = new GalaxyMapModel(session.Service, access, null);
            uint revision = map.Revision;
            session.State.RouteDestination = session.Id(2); map.Refresh();
            Assert.AreEqual(revision, map.Revision, "Changing an unknown route target changes no visible entry.");
            session.Service.Reveal(session.Id(1)); map.Refresh();
            Assert.AreEqual(++revision, map.Revision);
            session.Service.MarkVisited(session.Id(1)); map.Refresh();
            Assert.AreEqual(++revision, map.Revision); Assert.IsTrue(Find(map, session.Id(1)).Visited);
            Assert.IsTrue(Find(map, session.Id(2)).IsRouteTarget);
            session.Service.MarkVisited(session.Id(1)); map.Refresh(); Assert.AreEqual(revision, map.Revision);

            uint stateRevision = session.State.Revision;
            session.State.CurrentStarSystem = session.Id(1); map.Refresh();
            Assert.AreEqual(stateRevision, session.State.Revision); Assert.AreEqual(++revision, map.Revision);
            Assert.IsFalse(Find(map, session.Id(0)).Current); Assert.IsTrue(Find(map, session.Id(1)).Current);
            session.State.RouteDestination = session.Id(1); map.Refresh();
            Assert.AreEqual(++revision, map.Revision);
            Assert.IsTrue(Find(map, session.Id(1)).IsRouteTarget); Assert.IsFalse(Find(map, session.Id(2)).IsRouteTarget);
            access.Revision++; map.Refresh(); Assert.AreEqual(revision, map.Revision, "A stamp alone is not a visible change.");
            access.Open = true; access.Revision++; map.Refresh(); Assert.AreEqual(++revision, map.Revision);
            foreach (var lane in map.Lanes) { Assert.IsTrue(lane.Open); Assert.AreEqual("", lane.LockedMessage); }
            access.FuelCost++; access.Revision++; map.Refresh(); Assert.AreEqual(++revision, map.Revision);

            var systems = map.StarSystems; var lanes = map.Lanes;
            // Restoring rebuilds GalaxyDef without changing the state stamp or the visible entries.
            session.Service.OnSessionRestored(); map.Refresh(); Assert.AreEqual(revision, map.Revision);
            TestContentFactory.Edit(session.Fixture.Start, so => TestContentFactory.Set(so, "danger", 3));
            session.Service.OnSessionRestored(); map.Refresh();
            Assert.AreEqual(++revision, map.Revision); Assert.AreEqual(3, Find(map, session.Id(0)).Danger);
            session.Service.NewGame(42); map.Refresh(); Assert.AreEqual(++revision, map.Revision);
            Assert.AreEqual(1, map.StarSystems.Count); Assert.IsEmpty(map.Lanes);
            Assert.AreSame(systems, map.StarSystems); Assert.AreSame(lanes, map.Lanes);
        }
    }
}
