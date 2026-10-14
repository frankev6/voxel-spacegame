using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Factions;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    /// <summary>ENC-005: tiny generated galaxy and the real boot singleton/faction facade; direct queries and ticks.</summary>
    public sealed class TerritoryTests
    {
        sealed class Fixture : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Content = new(3, 1, 42, false);
            internal readonly World World = new("ENC-005 territory");
            internal readonly GalaxyState State = new();
            internal readonly GalaxyFrameTicker Ticker = new();
            internal readonly FactionDefinition Player, Other;
            internal readonly FactionService Factions;
            internal readonly GalaxyService Galaxy;
            internal readonly TerritoryService Territory;
            readonly List<Entity> created = new();
            internal Fixture(bool generate = true)
            {
                try
                {
                    // Player sorts after the other faction in content; canonical index zero must still win.
                    Player = Content.Create<FactionDefinition>("faction_z_player", so =>
                    {
                        so.FindProperty("displayName").stringValue = "Player"; so.FindProperty("isPlayerFaction").boolValue = true;
                        so.FindProperty("relationLocked").boolValue = true; so.FindProperty("basePlayerRelation").intValue = 100;
                        so.FindProperty("mapColorToken").stringValue = "--color-faction-ark";
                        SetOwned(so, Content.Generation.AuthoredStarSystems[0]);
                    });
                    Other = Content.Create<FactionDefinition>("faction_a_other", so =>
                    {
                        so.FindProperty("displayName").stringValue = "Other";
                        so.FindProperty("mapColorToken").stringValue = "--color-faction-vestari";
                        SetOwned(so, Content.Generation.AuthoredStarSystems[1]);
                    });
                    var config = Content.Create<EncounterConfigDefinition>("config_encounters");
                    var report = new ValidationReport("ENC-005 territory"); var catalog = ContentCatalog.Build(Content.Definitions, report);
                    Assert.IsFalse(report.HasErrors, report.ToText());
                    FactionContent.CreateSingletons(World.EntityManager, catalog, config, created);
                    Factions = new FactionService(World, catalog);
                    Galaxy = new GalaxyService(catalog, State, World, Ticker);
                    if (generate) Galaxy.NewGame(42);
                    Territory = new TerritoryService(Galaxy, Factions, catalog); Ticker.Register(Territory);
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
            public void Dispose()
            {
                if (Territory != null) Ticker.Unregister(Territory);
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
            Assert.IsTrue(procedural, "The fixture includes one procedural system which remains unowned in M3.");
            var formerlyOwned = f.Authored(0); Assert.AreSame(f.Player, f.Territory.GetOwner(formerlyOwned));
            f.State.Clear(); EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Cleared)); EventHub.DrainAll();
            Assert.IsNull(f.Galaxy.Galaxy); CheckUnowned(f, formerlyOwned);
        }
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
