using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Factions;
using Game.Tests.EditMode.Data;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using UnityEditor;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    /// <summary>ENC-005/014: real generated galaxy and faction facade; bounded graphs, queries and deferred events.</summary>
    public sealed class TerritoryTests
    {
        sealed class Fixture : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Content;
            internal readonly World World = new("ENC-005/014 territory");
            internal readonly GalaxyState State = new();
            internal readonly GalaxyFrameTicker Ticker = new();
            internal readonly FactionDefinition Player, Other;
            internal readonly FactionService Factions;
            internal readonly GalaxyService Galaxy;
            internal TerritoryService Territory;
            internal readonly ContentCatalog Catalog;
            readonly List<Entity> created = new();
            internal Fixture(bool generate = true, int proceduralCount = 1, int playerSeeds = 0, int otherSeeds = 0, int radius = 2, ulong seed = 42)
            {
                Content = new TestGalaxyFixtures.Fixture(3, proceduralCount, seed, false);
                try
                {
                    // Player sorts after the other faction in content; canonical index zero must still win.
                    Player = Content.Create<FactionDefinition>("faction_z_player", so =>
                    {
                        so.FindProperty("displayName").stringValue = "Player"; so.FindProperty("isPlayerFaction").boolValue = true;
                        so.FindProperty("relationLocked").boolValue = true; so.FindProperty("basePlayerRelation").intValue = 100;
                        so.FindProperty("mapColorToken").stringValue = "--color-faction-ark";
                        SetOwned(so, Content.Generation.AuthoredStarSystems[0]);
                        so.FindProperty("territorySeeds").intValue = playerSeeds;
                        so.FindProperty("territoryRadiusHops").intValue = radius;
                    });
                    Other = Content.Create<FactionDefinition>("faction_a_other", so =>
                    {
                        so.FindProperty("displayName").stringValue = "Other";
                        so.FindProperty("mapColorToken").stringValue = "--color-faction-vestari";
                        SetOwned(so, Content.Generation.AuthoredStarSystems[1]);
                        so.FindProperty("territorySeeds").intValue = otherSeeds;
                        so.FindProperty("territoryRadiusHops").intValue = radius;
                    });
                    var config = Content.Create<EncounterConfigDefinition>("config_encounters");
                    var report = new ValidationReport("ENC-005 territory"); Catalog = ContentCatalog.Build(Content.Definitions, report);
                    Assert.IsFalse(report.HasErrors, report.ToText());
                    FactionContent.CreateSingletons(World.EntityManager, Catalog, config, created);
                    Factions = new FactionService(World, Catalog);
                    Galaxy = new GalaxyService(Catalog, State, World, Ticker);
                    if (generate) Galaxy.NewGame(seed);
                    Territory = new TerritoryService(Galaxy, Factions, Catalog); Ticker.Register(Territory);
                }
                catch { Dispose(); throw; }
            }
            internal StarSystemId Authored(int index)
            {
                Assert.IsTrue(Galaxy.TryResolveStarSystem(Content.Generation.AuthoredStarSystems[index].Id, out var id));
                return id;
            }
            internal static void Owned(FactionDefinition faction, params AuthoredStarSystemDefinition[] systems)
            {
                using var so = new SerializedObject(faction); SetOwned(so, systems); so.ApplyModifiedPropertiesWithoutUndo();
            }
            static void SetOwned(SerializedObject so, params AuthoredStarSystemDefinition[] systems)
            {
                var list = so.FindProperty("ownedStarSystems"); list.arraySize = systems.Length;
                for (int i = 0; i < systems.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            }
            // Replace only the public native graph of a real GalaxyService-generated definition.
            // The faction map, authored records, seed and production query/rebuild path stay real.
            internal double Graph(int proceduralCount, List<(int A, int B)> edges)
            {
                Ticker.Unregister(Territory); Territory.Dispose();
                var galaxy = Galaxy.Galaxy;
                var authored = new List<StarSystemDef>(); StarSystemDef prototype = default;
                foreach (var system in galaxy.StarSystems)
                    if (system.AuthoredIndex != 0) authored.Add(system); else prototype = system;
                galaxy.StarSystems.Dispose(); galaxy.StarSystems = default;
                galaxy.IndexById.Dispose(); galaxy.IndexById = default;
                galaxy.Lanes.Dispose(); galaxy.Lanes = default;
                galaxy.Adjacency.Dispose(); galaxy.Adjacency = default;
                int count = proceduralCount + authored.Count;
                galaxy.StarSystems = new NativeArray<StarSystemDef>(count, Allocator.Persistent);
                galaxy.IndexById = new NativeHashMap<uint, int>(count, Allocator.Persistent);
                for (int i = 0; i < count; i++)
                {
                    var system = i < proceduralCount ? prototype : authored[i - proceduralCount];
                    if (i < proceduralCount) { system.Id = StarSystemId.FromProceduralIndex(i); system.AuthoredIndex = 0; }
                    system.FirstAdjacency = 0; system.LaneCount = 0;
                    galaxy.StarSystems[i] = system; galaxy.IndexById.Add(system.Id.Value, i);
                }
                for (int i = 0; i < edges.Count; i++)
                    if (edges[i].A > edges[i].B) edges[i] = (edges[i].B, edges[i].A);
                edges.Sort((a, b) => a.A != b.A ? a.A.CompareTo(b.A) : a.B.CompareTo(b.B));
                var degrees = new int[count]; var cursor = new int[count];
                foreach (var edge in edges) { degrees[edge.A]++; degrees[edge.B]++; }
                int offset = 0;
                for (int i = 0; i < count; i++)
                {
                    var system = galaxy.StarSystems[i]; system.FirstAdjacency = offset; system.LaneCount = checked((ushort)degrees[i]);
                    galaxy.StarSystems[i] = system; cursor[i] = offset; offset += degrees[i];
                }
                galaxy.Lanes = new NativeArray<LaneDef>(edges.Count, Allocator.Persistent);
                galaxy.Adjacency = new NativeArray<int>(offset, Allocator.Persistent);
                // Sorted (A,B) lanes also produce neighbour-sorted adjacency for each endpoint.
                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    galaxy.Lanes[i] = new LaneDef { A = galaxy.StarSystems[edge.A].Id, B = galaxy.StarSystems[edge.B].Id, LengthLy = 1 };
                    galaxy.Adjacency[cursor[edge.A]++] = i; galaxy.Adjacency[cursor[edge.B]++] = i;
                }
                var timer = Stopwatch.StartNew();
                Territory = new TerritoryService(Galaxy, Factions, Catalog);
                timer.Stop(); Ticker.Register(Territory);
                return timer.Elapsed.TotalMilliseconds;
            }
            public void Dispose()
            {
                if (Territory != null) { Ticker.Unregister(Territory); Territory.Dispose(); }
                Factions?.Dispose(); Galaxy?.Dispose();
                if (World.IsCreated)
                {
                    World.EntityManager.CompleteAllTrackedJobs();
                    foreach (var entity in created)
                        if (World.EntityManager.Exists(entity)) World.EntityManager.GetComponentData<FactionRelations>(entity).Dispose();
                    World.Dispose();
                }
                State.Dispose(); Content.Dispose();
            }
        }
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        [Test] public void Authored_Owner()
        {
            using var f = new Fixture();
            var player = f.Authored(0); var other = f.Authored(1);
            Assert.AreEqual(0, f.Factions.Indices.IndexOf(f.Player));
            Assert.AreEqual(f.Factions.Indices.IndexOf(f.Player), f.Territory.GetOwnerFaction(player));
            Assert.AreEqual(f.Factions.Indices.IndexOf(f.Other), f.Territory.GetOwnerFaction(other));
            Assert.AreSame(f.Player, f.Territory.GetOwner(player)); Assert.AreSame(f.Other, f.Territory.GetOwner(other));
            Assert.AreEqual(f.Player.MapColorToken, f.Territory.GetOwnerColorToken(player));
            Assert.AreEqual(f.Other.MapColorToken, f.Territory.GetOwnerColorToken(other));
            Assert.AreEqual(310, f.Territory.TickOrder);
        }
        [Test] public void Unowned_255()
        {
            using var f = new Fixture(false);
            CheckUnowned(f, default);
            f.Galaxy.NewGame(42); CheckUnowned(f, f.Authored(2));
            uint unknown = uint.MaxValue;
            while (f.Galaxy.Galaxy.TryGetIndex(new StarSystemId(unknown), out _)) unknown--;
            CheckUnowned(f, new StarSystemId(unknown));
            bool procedural = false;
            foreach (var system in f.Galaxy.Galaxy.StarSystems)
                if (system.AuthoredIndex == 0) { CheckUnowned(f, system.Id); procedural = true; }
            Assert.IsTrue(procedural, "With zero territory seeds, the procedural system remains unowned.");
            var formerlyOwned = f.Authored(0); Assert.AreSame(f.Player, f.Territory.GetOwner(formerlyOwned));
            f.State.Clear(); EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Cleared)); EventHub.DrainAll();
            Assert.IsNull(f.Galaxy.Galaxy); CheckUnowned(f, formerlyOwned);
        }
        [Test] public void Procedural_Deterministic()
        {
            using (var f = new Fixture(proceduralCount: 24, playerSeeds: 2, otherSeeds: 2, radius: 3))
            {
                var ids = new List<StarSystemId>(); var owners = new List<byte>(); var proceduralOwners = new List<byte>();
                foreach (var system in f.Galaxy.Galaxy.StarSystems)
                {
                    ids.Add(system.Id); byte owner = f.Territory.GetOwnerFaction(system.Id); owners.Add(owner);
                    if (system.AuthoredIndex == 0) proceduralOwners.Add(owner);
                }
                Assert.Contains((byte)0, proceduralOwners); Assert.Contains((byte)1, proceduralOwners);
                f.Galaxy.OnSessionRestored();
                Assert.AreEqual(ids.Count, f.Galaxy.Galaxy.StarSystems.Length);
                for (int i = 0; i < ids.Count; i++)
                {
                    Assert.AreEqual(ids[i], f.Galaxy.Galaxy.StarSystems[i].Id);
                    Assert.AreEqual(owners[i], f.Territory.GetOwnerFaction(ids[i]), "Regeneration changed ownership at " + ids[i]);
                }
                Assert.AreSame(f.Player, f.Territory.GetOwner(f.Authored(0)));
                Assert.AreSame(f.Other, f.Territory.GetOwner(f.Authored(1))); CheckUnowned(f, f.Authored(2));
            }
            // More requested seeds than available systems must terminate, wrap and preserve authored ownership.
            using (var f = new Fixture(playerSeeds: 64, otherSeeds: 64, radius: 0))
            {
                f.Graph(3, new List<(int, int)>());
                for (int i = 0; i < 3; i++) Assert.AreEqual(0, f.Territory.GetOwnerFaction(StarSystemId.FromProceduralIndex(i)));
                Assert.AreSame(f.Player, f.Territory.GetOwner(f.Authored(0)));
                Assert.AreSame(f.Other, f.Territory.GetOwner(f.Authored(1))); CheckUnowned(f, f.Authored(2));
            }
        }
        [Test] public void Procedural_HopBound()
        {
            using var f = new Fixture(playerSeeds: 1, radius: 2);
            const int count = 9;
            int seed = Seed(f.Galaxy.Galaxy.Seed, 0, count), remote = seed < count / 2 ? count - 1 : 0;
            var edges = Chain(count);
            // An authored system offers a two-hop shortcut which procedural expansion must not cross.
            edges.Add((seed, count)); edges.Add((remote, count)); f.Graph(count, edges);
            for (int i = 0; i < count; i++)
                Assert.AreEqual(Math.Abs(i - seed) <= 2 ? 0 : 255, f.Territory.GetOwnerFaction(StarSystemId.FromProceduralIndex(i)), "Chain node " + i);
            Assert.AreSame(f.Player, f.Territory.GetOwner(f.Authored(0)));
            Assert.AreSame(f.Other, f.Territory.GetOwner(f.Authored(1))); CheckUnowned(f, f.Authored(2));
        }
        [Test] public void Procedural_TieBreak()
        {
            const int count = 5;
            ulong galaxySeed = 1; int playerSeed = -1, otherSeed = -1;
            for (; galaxySeed <= 64; galaxySeed++)
            {
                playerSeed = Seed(galaxySeed, 0, count); otherSeed = Seed(galaxySeed, 1, count);
                if (otherSeed == playerSeed) otherSeed = (otherSeed + 1) % count;
                if (playerSeed > otherSeed) break;
            }
            Assert.LessOrEqual(galaxySeed, 64UL, "A bounded fixture seed must place the lower faction at a higher system index.");
            using var f = new Fixture(playerSeeds: 1, otherSeeds: 1, radius: 1, seed: galaxySeed);
            int target = 0; while (target == playerSeed || target == otherSeed) target++;
            f.Graph(count, new List<(int, int)> { (playerSeed, target), (otherSeed, target) });
            Assert.AreEqual(0, f.Territory.GetOwnerFaction(StarSystemId.FromProceduralIndex(playerSeed)));
            Assert.AreEqual(1, f.Territory.GetOwnerFaction(StarSystemId.FromProceduralIndex(otherSeed)));
            Assert.AreEqual(0, f.Territory.GetOwnerFaction(StarSystemId.FromProceduralIndex(target)), "Equal-hop tie follows canonical faction order, independent of system order.");
            for (int i = 0; i < count; i++)
                if (i != playerSeed && i != otherSeed && i != target) CheckUnowned(f, StarSystemId.FromProceduralIndex(i));
        }
        [Test] public void TerritoryEntered_OnOwnerChange()
        {
            using var f = new Fixture(playerSeeds: 64, radius: 0);
            EventHub.DrainAll();
            var entries = new List<TerritoryEntered>(); EventBus<TerritoryEntered>.Subscribe(entries.Add);
            try
            {
                var a = f.Authored(0); var b = f.Authored(1); var c = f.Authored(2);
                Enter(a); Assert.AreEqual(1, entries.Count); CheckEntry(entries[0], f.Player, a);
                Enter(a); Assert.AreEqual(1, entries.Count);
                Enter(StarSystemId.FromProceduralIndex(0)); Assert.AreEqual(1, entries.Count, "Another system of the same faction is silent.");
                Enter(b); Assert.AreEqual(2, entries.Count); CheckEntry(entries[1], f.Other, b);
                Enter(c); Assert.AreEqual(2, entries.Count, "Leaving for unowned space is silent.");
                Enter(b); Assert.AreEqual(3, entries.Count); CheckEntry(entries[2], f.Other, b);
                Enter(b); Assert.AreEqual(3, entries.Count);
                EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Cleared)); EventHub.DrainAll();
                f.Galaxy.NewGame(42); Enter(b); Assert.AreEqual(4, entries.Count, "A new session has no previous territory.");
                int enteredSubscribers = EventBus<StarSystemEntered>.SubscriberCount, resetSubscribers = EventBus<SessionReset>.SubscriberCount;
                f.Territory.Dispose(); f.Territory.Dispose();
                Assert.AreEqual(enteredSubscribers - 1, EventBus<StarSystemEntered>.SubscriberCount);
                Assert.AreEqual(resetSubscribers - 1, EventBus<SessionReset>.SubscriberCount);
                Enter(a); Assert.AreEqual(4, entries.Count, "Disposed territory no longer publishes.");
            }
            finally { EventBus<TerritoryEntered>.Unsubscribe(entries.Add); }
        }
        [Test, Category("Performance")] public void Rebuild_100k()
        {
            using var f = new Fixture(playerSeeds: 64, otherSeeds: 64, radius: 8);
            const int procedural = 100000 - 3;
            double milliseconds = f.Graph(procedural, Chain(procedural));
            Assert.AreEqual(100000, f.Galaxy.Galaxy.StarSystems.Length);
            Assert.AreSame(f.Player, f.Territory.GetOwner(f.Authored(0)));
            Assert.AreSame(f.Other, f.Territory.GetOwner(f.Authored(1))); CheckUnowned(f, f.Authored(2));
            Assert.AreEqual(0, f.Territory.GetOwnerFaction(StarSystemId.FromProceduralIndex(Seed(f.Galaxy.Galaxy.Seed, 0, procedural))));
            PerfRun.Gate("Territory.Rebuild_100k", new[] { milliseconds }, 30);
        }
        static int Seed(ulong galaxySeed, byte faction, int count)
        { var rng = RngStreams.For(galaxySeed, RngPurpose.Territory, faction); return rng.NextInt(0, count); }
        static List<(int A, int B)> Chain(int count)
        {
            var edges = new List<(int, int)>(count - 1);
            for (int i = 1; i < count; i++) edges.Add((i - 1, i));
            return edges;
        }
        static void Enter(StarSystemId id)
        { EventBus<StarSystemEntered>.Publish(new StarSystemEntered(id.Value, StarSystemEntryReason.Jump, 1)); EventHub.DrainAll(); }
        static void CheckEntry(TerritoryEntered entry, FactionDefinition faction, StarSystemId system)
        { Assert.AreEqual(faction.Id.Hash, entry.Faction); Assert.AreEqual(system.Value, entry.StarSystemValue); }
        static void CheckUnowned(Fixture f, StarSystemId id)
        {
            Assert.AreEqual(255, f.Territory.GetOwnerFaction(id)); Assert.IsNull(f.Territory.GetOwner(id));
            Assert.AreEqual("", f.Territory.GetOwnerColorToken(id));
        }
        [Test] public void Rebuild_OnGeneration()
        {
            using var f = new Fixture(); var a = f.Authored(0); var b = f.Authored(1);
            int generation = f.Galaxy.GenerationCount;
            Assert.AreSame(f.Player, f.Territory.GetOwner(a)); Assert.AreSame(f.Other, f.Territory.GetOwner(b));
            Fixture.Owned(f.Player, f.Content.Generation.AuthoredStarSystems[1]);
            Fixture.Owned(f.Other, f.Content.Generation.AuthoredStarSystems[0]);
            f.Galaxy.NewGame(99); Assert.AreEqual(generation + 1, f.Galaxy.GenerationCount);
            // A query must rebuild before any territory frame tick runs.
            Assert.AreSame(f.Other, f.Territory.GetOwner(a)); Assert.AreSame(f.Player, f.Territory.GetOwner(b));
            Fixture.Owned(f.Other); f.Galaxy.NewGame(101);
            f.Ticker.Tick(new GalaxyFrameContext(f.World.EntityManager, 0, 0, 0, 1));
            // Change only the transient test definition after Tick: a no-op Tick would let the
            // first query wrongly rebuild from this newer value, instead of retaining its stamp.
            Fixture.Owned(f.Other, f.Content.Generation.AuthoredStarSystems[0]);
            CheckUnowned(f, a); Assert.AreSame(f.Player, f.Territory.GetOwner(b));
            f.Galaxy.NewGame(102); Assert.AreSame(f.Other, f.Territory.GetOwner(a));
        }
    }
}
