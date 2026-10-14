using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.App.Services;
using Game.App.Session;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Galaxy.Systems;
using Game.Narrative;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using Game.Tests.EditMode.Galaxy;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.App
{
    /// <summary>ENC-009's five cases: real faction/territory/loader/director and compiled logic; direct owner ticks, no scenes or combat waits.</summary>
    public sealed class LogicWorldAdapterEncounterTests
    {
        // Only the grid creation boundary is recorded. The production director owns every instance,
        // anchor resolution, pose, pending request, encounter tag, combat initialization and defeat.
        sealed class Spawner : IShipSpawner
        {
            readonly EntityManager em;
            internal readonly List<GalaxyShipSpawnArgs> Calls = new();
            internal readonly List<Entity> Roots = new();
            internal Spawner(EntityManager em) { this.em = em; }
            public PersistentIdValue Spawn(in GalaxyShipSpawnArgs args)
            {
                Calls.Add(args);
                ulong id = (ulong)Calls.Count;
                var root = em.CreateEntity(typeof(LocalTransform), typeof(PersistentId), typeof(Allegiance), typeof(AiBrain));
                em.SetComponentData(root, LocalTransform.FromPositionRotation(args.Position, args.Rotation));
                em.SetComponentData(root, new PersistentId(id)); em.SetComponentData(root, args.Allegiance);
                em.AddSharedComponent(root, new StarSystemMember { Id = args.StarSystem });
                Roots.Add(root); return new PersistentIdValue(id);
            }
            public bool TryGetRoot(ulong id, out Entity root)
            {
                root = id > 0 && id <= (ulong)Roots.Count ? Roots[(int)id - 1] : Entity.Null;
                return root != Entity.Null && em.Exists(root);
            }
            public bool Despawn(ulong id)
            { if (!TryGetRoot(id, out var root)) return false; em.DestroyEntity(root); return true; }
        }
        sealed class Audio : IAudioService
        {
            public void PlayCue(ContentId cue) => throw new InvalidOperationException("No travel in encounter routing cases.");
            public void PlayCueAt(ContentId cue, float3 position, float volume) { }
            public void PlayUi(UiSound sound) { }
            public void PlayStinger(ContentId cue) { }
            public void SetMusicOverride(ContentId cue, float seconds) { }
            public void ClearMusicOverride(float seconds) { }
        }
        // An INarrativeSystem host forwards the actual runtime dependency selection to LogicWatcher.
        // It records evaluations/flips; it does not supply relation values or condition results.
        sealed class WatchedCondition : INarrativeSystem, IDisposable
        {
            readonly DependencyIndex index;
            readonly HashSet<int> dirty = new();
            internal readonly LogicWatcher Watcher;
            internal int Evaluations, Flips;
            public int Order => 0;
            internal WatchedCondition(ICondition condition, ILogicContext context)
            {
                var dependencies = new List<LogicDependency>(); condition.CollectDependencies(dependencies);
                index = new DependencyIndex(new IReadOnlyList<LogicDependency>[] { dependencies });
                Watcher = new LogicWatcher(condition, context, _ => Flips++);
            }
            public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
            {
                dirty.Clear(); index.Collect(changed, dirty);
                if (!fullReevaluate && dirty.Count == 0) return;
                Evaluations++; Watcher.Refresh();
            }
            public void OnNewGame() { }
            public void OnRestored() { }
            public void AfterEvaluation(bool playerFree) { }
            public void Dispose() => Watcher.Dispose();
        }
        sealed class Fixture : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Content = new(2, 0, 42, false);
            internal readonly EcsTestWorld Ecs = EcsTestWorld.Create(typeof(FactionStandingSystem), typeof(OriginRebaseSystem));
            internal EntityManager Em => Ecs.EntityManager;
            internal readonly GalaxyState State = new();
            internal readonly ContentCatalog Catalog;
            internal readonly FactionDefinition Ark, Vestari, Other;
            internal readonly EncounterDefinition Encounter;
            internal readonly FactionService Factions;
            internal readonly GalaxyService Galaxy;
            internal readonly TerritoryService Territory;
            internal readonly StarSystemLoader Loader;
            internal readonly TravelService Travel;
            internal readonly EncounterDirectorService Director;
            internal readonly LogicWorldAdapter Adapter;
            internal readonly Spawner Ships;
            internal readonly LogicContext Context;
            internal readonly NarrativeService Narrative;
            internal readonly NarrativeWorldEventBridge Bridge;
            internal readonly FactStore Facts;
            internal readonly Entity Standing, Player, Clock;
            readonly NativeQueue<ManagedEventEnvelope<FactionRelationChanged>> relationEvents;
            readonly NativeQueue<ManagedEventEnvelope<OriginShifted>> originEvents;
            readonly PauseService pause = new();
            readonly GameSession session;
            readonly BlueprintLibrary blueprints;
            readonly VoxelStructureRegistry registry;
            readonly RefResolverRegistry refs;
            readonly LogicCompiler compiler;
            internal Fixture(bool ready = true)
            {
                try
                {
                    Ark = Content.Create<FactionDefinition>("faction_ark", so =>
                    { TestContentFactory.Set(so, "isPlayerFaction", true); TestContentFactory.Set(so, "relationLocked", true); });
                    Vestari = Content.Create<FactionDefinition>("faction_vestari", so =>
                    {
                        var owned = so.FindProperty("ownedStarSystems"); owned.arraySize = 1;
                        owned.GetArrayElementAtIndex(0).objectReferenceValue = Content.Start;
                    });
                    Other = Content.Create<FactionDefinition>("faction_wanderers");
                    var config = Content.Create<EncounterConfigDefinition>("config_encounters", so =>
                    { TestContentFactory.Set(so, "minPlayerDistanceM", 1f); TestContentFactory.Set(so, "bodyClearanceM", 0f); });
                    var ship = Content.Create<ShipBlueprintDefinition>("ship_encounter_fixture", so =>
                    { so.FindProperty("boundsMin").vector3IntValue = Vector3Int.zero; so.FindProperty("boundsMax").vector3IntValue = Vector3Int.one; });
                    Encounter = Content.Create<EncounterDefinition>("encounter_raiders", so =>
                    {
                        TestContentFactory.Set(so, "faction", Vestari); TestContentFactory.Set(so, "kind", (int)EncounterKind.Story);
                        TestContentFactory.Set(so, "spawnDistanceMinM", 1000f); TestContentFactory.Set(so, "spawnDistanceMaxM", 1000f);
                        var ships = so.FindProperty("ships"); ships.arraySize = 1; var entry = ships.GetArrayElementAtIndex(0);
                        entry.FindPropertyRelative("blueprint").objectReferenceValue = ship;
                        entry.FindPropertyRelative("countMin").intValue = entry.FindPropertyRelative("countMax").intValue = 1;
                    });
                    using (var so = new SerializedObject(Content.Planet))
                    { TestContentFactory.Set(so, "storyTag", "test_anchor"); so.ApplyModifiedPropertiesWithoutUndo(); }
                    using (var so = new SerializedObject(Content.Start))
                    {
                        // Both the parent slot and the selected moon's subslot must survive LogicBodyHandle mapping.
                        so.FindProperty("bodies").GetArrayElementAtIndex(0).FindPropertyRelative("slot").intValue = 3;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    // No voxel streaming or collider resources are needed at this routing boundary.
                    foreach (var body in new[] { Content.Star, Content.Planet, Content.Moon })
                    { using var so = new SerializedObject(body); TestContentFactory.Set(so, "hasCollider", false); so.ApplyModifiedPropertiesWithoutUndo(); }
                    Content.Create<TravelConfigDefinition>("config_travel", so =>
                        TestContentFactory.Set(so, "fuelItem", Content.Definitions.Find(def => def is ItemDefinition)));
                    var gameConfig = Content.Create<GameConfigDefinition>("config_encounter_game");
                    var narrativeConfig = Content.Create<NarrativeConfigDefinition>("narrative_encounter_fixture");
                    var report = new ValidationReport("ENC-009 fixture"); Catalog = ContentCatalog.Build(Content.Definitions, report);
                    refs = RefResolverRegistry.CreateDefault(Catalog);
                    var facts = FactCatalog.Build(Catalog, refs, report); Facts = new FactStore(facts);
                    compiler = new LogicCompiler(facts, refs, new LogicFunctionRegistry());
                    Assert.IsFalse(report.HasErrors, report.ToText());
                    var created = new List<Entity>(); FactionContent.CreateSingletons(Em, Catalog, config, created); Standing = created[0];
                    Factions = new FactionService(Ecs.World, Catalog);
                    Ecs.AddGameplayActive(); Ecs.AddWorldSceneActive(); Clock = Ecs.AddSimClock();
                    Ecs.AddSingleton(default(PlayerControlLock));
                    relationEvents = new NativeQueue<ManagedEventEnvelope<FactionRelationChanged>>(Allocator.Persistent);
                    originEvents = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent);
                    Ecs.AddSingleton(new ManagedEventQueue<FactionRelationChanged> { Queue = relationEvents });
                    Ecs.AddSingleton(new ManagedEventQueue<OriginShifted> { Queue = originEvents });
                    Player = Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform), typeof(UniversePosition));
                    Em.SetComponentData(Player, LocalTransform.Identity);
                    Galaxy = new GalaxyService(Catalog, State, Ecs.World, new GalaxyFrameTicker()); Galaxy.NewGame(42);
                    blueprints = new BlueprintLibrary(Catalog, default); registry = VoxelStructureRegistry.CreateRuntime(blueprints);
                    session = new GameSession(Ecs.World, gameConfig);
                    var docking = new DockingService(); Loader = new StarSystemLoader(Galaxy, Ecs.World, registry, blueprints, session, Catalog, docking);
                    Travel = new TravelService(Galaxy, Loader, docking, Ecs.World, Catalog, pause, new Audio());
                    Territory = new TerritoryService(Galaxy, Factions, Catalog); Ships = new Spawner(Em);
                    Director = new EncounterDirectorService(Galaxy, Loader, Travel, docking, Ships, Factions, Territory, Ecs.World, Catalog) { Enabled = false };
                    Services.Register(Factions); Services.Register(Territory); Services.Register(Loader); Services.Register(Director);
                    Adapter = new LogicWorldAdapter(Catalog, session, Galaxy, blueprints, null, null, Ecs.World, narrativeConfig);
                    var unused = new FakeLogicContext(facts); // Unused economy/quest seams only; World is the real adapter.
                    Context = new LogicContext(Facts, unused, unused, Adapter, () => 42);
                    Context.PushScope(new LogicScope(LogicUsage.EncounterCondition, 0, 0, 0, false, default));
                    Narrative = new NarrativeService(Facts, Context);
                    Bridge = NarrativeWorldEventBridge.CreateForWorld(Narrative, Catalog, Galaxy,
                        id => Director.TryGetInstanceByMember(id, out var info) ? info.Definition.Id.Hash : (ContentHash?)null,
                        () => Travel.Phase == Game.Galaxy.Travel.TravelPhase.Idle, (_, _) => false, (_, _, _) => { }, (_, _) => { });
                    if (ready) Load(Galaxy.Galaxy.StartStarSystem);
                }
                catch { Dispose(); throw; }
            }
            internal void Load(StarSystemId system)
            {
                State.CurrentStarSystem = system; State.HasPlayerLocation = true;
                State.PlayerLocation = new PlayerLocation { StarSystem = system };
                Loader.OnSceneReady(new SceneReadyContext(GameScene.World, SceneEntryReason.NewGame, default));
                Ecs.UpdateSystem<OriginRebaseSystem>(); Ecs.CompleteAll();
                Loader.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 0));
                Assert.IsTrue(Loader.IsReady); Assert.AreEqual(system, Loader.Loaded);
                // Keep the player well away from bodies, so their anchor identities are distinguishable.
                Em.SetComponentData(Player, LocalTransform.FromPosition(new float3(120000, 0, -70000)));
                EventHub.DrainAll();
            }
            internal BodySpec Body(BodyRole role, ushort subSlot = 0)
            {
                foreach (var body in Galaxy.GetContent(Loader.Loaded).Bodies)
                    if (body.Id.Role == role && body.Id.SubSlot == subSlot) return body;
                throw new InvalidOperationException("Fixture body is absent.");
            }
            internal LogicScope Scope(BodySpec? body = null)
            {
                var handle = body.HasValue ? new LogicBodyHandle(body.Value.Id.System.Value, (byte)body.Value.Id.Role,
                    body.Value.Id.Slot, body.Value.Id.SubSlot, Galaxy.GetBodyTemplate(body.Value.TemplateIndex).Id.Hash) : default;
                return new LogicScope(LogicUsage.DialogCommand, 0, 0, 0, body.HasValue, handle);
            }
            internal void Command(string source, in LogicScope scope)
            {
                var report = new ValidationReport("ENC-009 command");
                Assert.IsTrue(compiler.TryCompileCommand(source, new LogicCompileOptions(scope.Usage, "test:encounter", "command"), report, out var effect), report.ToText());
                effect.Validate(report); effect.Bind(new LogicBinder(Facts.Catalog, refs, report)); Assert.IsFalse(report.HasErrors, report.ToText());
                Context.PushScope(scope); try { effect.Apply(Context); } finally { Context.PopScope(); }
            }
            internal CompiledCondition Condition(string source)
            {
                var condition = LogicTestEnvironment.Source(new CompiledCondition(), source); var report = new ValidationReport("ENC-009 watcher");
                Assert.IsTrue(compiler.Compile(condition, new LogicCompileOptions(LogicUsage.EncounterCondition, "test:encounter", "condition"), report), report.ToText());
                condition.Bind(new LogicBinder(Facts.Catalog, refs, report)); Assert.IsFalse(report.HasErrors, report.ToText()); return condition;
            }
            internal void TickRelations(uint tick)
            {
                Em.SetComponentData(Clock, new SimClock { Tick = tick }); Ecs.UpdateSystem<FactionStandingSystem>(); Ecs.CompleteAll();
                // Transport the actual fixed-step owner's payload, preserving its catalogue hash.
                while (relationEvents.TryDequeue(out var envelope)) EventBus<FactionRelationChanged>.Publish(envelope.Payload);
                EventBus<FactionRelationChanged>.Drain();
            }
            internal void TickDirector() => Director.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 0));
            internal void CheckPose(int call, double3 anchor, double radius, double distance)
            {
                var args = Ships.Calls[call]; var origin = Ecs.GetSingleton<FrameOrigin>().OriginInStarSystem;
                Assert.AreEqual(radius + distance, math.length(((double3)args.Position + origin - anchor).xz), 1);
                Assert.AreEqual(Encounter.Id.Hash, args.Encounter); Assert.AreEqual(Loader.Loaded, args.StarSystem);
                Assert.AreEqual(Factions.Indices.IndexOf(Vestari), args.Allegiance.Faction);
                Assert.AreEqual(Encounter.Ships[0].blueprint.Id, args.Blueprint); Assert.IsTrue(args.WithAi);
                Assert.IsTrue(Director.TryGetInstanceByMember((ulong)call + 1, out var info)); Assert.AreSame(Encounter, info.Definition);
            }
            internal double3 PlayerPosition => Ecs.GetSingleton<FrameOrigin>().OriginInStarSystem + (double3)Em.GetComponentData<LocalTransform>(Player).Position;
            public void Dispose()
            {
                Ecs.CompleteAll(); Bridge?.Dispose(); Adapter?.Dispose(); Director?.Dispose(); Travel?.Dispose(); Loader?.Dispose();
                Services.Unregister(Director); Services.Unregister(Loader); Services.Unregister(Territory); Services.Unregister(Factions);
                session?.Dispose(); registry?.Dispose(); if (registry != null) Object.DestroyImmediate(registry); blueprints?.Dispose();
                Factions?.Dispose(); Galaxy?.Dispose(); State.Dispose(); pause.Dispose();
                if (Standing != Entity.Null && Em.Exists(Standing)) Em.GetComponentData<FactionRelations>(Standing).Dispose();
                if (relationEvents.IsCreated) relationEvents.Dispose(); if (originEvents.IsCreated) originEvents.Dispose();
                Ecs.Dispose(); Content.Dispose();
            }
        }

        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup()
        { Services.Reset(); EventHub.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); LogAssert.NoUnexpectedReceived(); }

        [Test] public void Relation_Routes()
        {
            using var f = new Fixture(); int index = f.Catalog.IndexOf(f.Vestari);
            Assert.AreNotEqual(index, f.Factions.Indices.IndexOf(f.Vestari), "Narrative indices are one-based content indices, not ECS faction indices.");
            Assert.AreEqual(0, f.Adapter.GetRelation(index));
            f.Command("adjust_relation faction_vestari -17", f.Scope());
            var requests = f.Em.GetBuffer<FactionRelationRequest>(f.Standing);
            Assert.AreEqual(1, requests.Length); Assert.AreEqual(f.Factions.Indices.IndexOf(f.Vestari), requests[0].Faction);
            Assert.AreEqual(-17, requests[0].Value); Assert.AreEqual(0, requests[0].Mode);
            Assert.AreEqual(-17, f.Adapter.GetRelation(index)); Assert.AreEqual(-17, f.Adapter.GetRelation(-1));
            Assert.AreEqual(0, f.Em.GetBuffer<FactionStanding>(f.Standing)[requests[0].Faction].Relation, "Only the fixed-step owner applies the request.");
            f.Command("adjust_relation owner 7", f.Scope());
            Assert.AreEqual(2, requests.Length); Assert.AreEqual(-10, f.Adapter.GetRelation(index));
            f.TickRelations(1); Assert.AreEqual(0, f.Em.GetBuffer<FactionRelationRequest>(f.Standing).Length);
            Assert.AreEqual(-10, f.Factions.GetRelation(f.Vestari));
            Assert.AreEqual(0, f.Adapter.GetRelation(0)); Assert.AreEqual(0, f.Adapter.GetRelation(int.MaxValue));
            foreach (string member in new[] { nameof(LogicWorldAdapter.GetRelation), nameof(LogicWorldAdapter.AdjustRelation),
                nameof(LogicWorldAdapter.CurrentStarSystemOwnerFactionIndex), nameof(LogicWorldAdapter.SpawnEncounter), nameof(LogicWorldAdapter.StartCombat) })
                CollectionAssert.DoesNotContain(LogicWorldAdapter.UnwiredMembers, member);
        }
        [Test] public void SpawnEncounter_AnchorMapping()
        {
            using var f = new Fixture(); var moon = f.Body(BodyRole.Moon, 1); var planet = f.Body(BodyRole.Planet);
            Assert.AreEqual(3, moon.Id.Slot); Assert.AreEqual(1, moon.Id.SubSlot);
            var bodyScope = f.Scope(moon); var none = f.Scope();
            f.Command("spawn_encounter encounter_raiders here 2", bodyScope);
            Assert.AreEqual(2, f.Director.ActiveInstanceCount); Assert.AreEqual(2, f.Ships.Calls.Count);
            for (int i = 0; i < 2; i++) f.CheckPose(i, OrbitMath.Position(moon, 0), moon.RadiusM, 1000);
            f.Command("spawn_encounter encounter_raiders player 1", bodyScope); f.CheckPose(2, f.PlayerPosition, 0, 1000);
            f.Command("spawn_encounter encounter_raiders here 1", none); f.CheckPose(3, f.PlayerPosition, 0, 1000);
            f.Command("spawn_encounter encounter_raiders test_anchor 1", bodyScope); f.CheckPose(4, OrbitMath.Position(planet, 0), planet.RadiusM, 1000);
            var instances = new List<EncounterInstanceInfo>(); f.Director.GetActiveInstances(instances); Assert.AreEqual(5, instances.Count);
            foreach (var instance in instances) { Assert.AreEqual(EncounterSource.Narrative, instance.Source); Assert.IsTrue(instance.Persistent); Assert.AreEqual(1, instance.Members); }
            // The real director's defeat notification reaches the canonical generated counter exactly once.
            int slot = f.Facts.Catalog.GeneratedSlot(FactFamily.Defeated, f.Encounter.Id.Hash.Value); Assert.GreaterOrEqual(slot, 0);
            f.Em.AddComponent<Hulk>(f.Ships.Roots[0]); f.TickDirector(); EventBus<EncounterDefeated>.Drain();
            Assert.AreEqual(1, f.Facts.GetBySlot(slot)); f.TickDirector(); EventBus<EncounterDefeated>.Drain(); Assert.AreEqual(1, f.Facts.GetBySlot(slot));
            f.Bridge.Dispose(); f.Em.AddComponent<Hulk>(f.Ships.Roots[1]); f.TickDirector(); EventBus<EncounterDefeated>.Drain(); Assert.AreEqual(1, f.Facts.GetBySlot(slot));
        }
        [Test] public void StartCombat_Routes()
        {
            using var f = new Fixture(false);
            f.Command("start_combat encounter_raiders", f.Scope());
            Assert.AreEqual(0, f.Ships.Calls.Count, "The real director retains requests before streaming is ready.");
            f.Load(f.Galaxy.Galaxy.StartStarSystem); f.TickDirector();
            Assert.AreEqual(1, f.Director.ActiveInstanceCount); f.CheckPose(0, f.PlayerPosition, 0, 500);
            var body = f.Body(BodyRole.Moon, 1); f.Command("start_combat encounter_raiders", f.Scope(body));
            Assert.AreEqual(2, f.Director.ActiveInstanceCount); Assert.AreEqual(2, f.Ships.Calls.Count);
            f.CheckPose(1, OrbitMath.Position(body, 0), body.RadiusM, 500);
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(f.Director.TryGetInstanceByMember((ulong)i + 1, out var instance));
                Assert.AreEqual(EncounterSource.Combat, instance.Source); Assert.IsTrue(instance.Persistent);
                var brain = f.Em.GetComponentData<AiBrain>(f.Ships.Roots[i]);
                Assert.AreEqual(1, brain.Initialized); Assert.AreEqual(f.Em.GetComponentData<LocalTransform>(f.Player).Position, brain.GoalPosition);
            }
            f.TickDirector(); Assert.AreEqual(2, f.Ships.Calls.Count, "Pending combat is consumed once.");
        }
        [Test] public void OwnerIndex()
        {
            using var f = new Fixture(); int owner = f.Catalog.IndexOf(f.Vestari);
            Assert.AreEqual(owner, f.Adapter.CurrentStarSystemOwnerFactionIndex);
            StarSystemId unowned = StarSystemId.FromAuthored(f.Content.Generation.AuthoredStarSystems[1].Id.Hash);
            f.State.CurrentStarSystem = unowned;
            Assert.AreEqual(owner, f.Adapter.CurrentStarSystemOwnerFactionIndex, "Ownership follows the actually loaded system, not a pending state target.");
            f.Load(unowned); Assert.AreEqual(0, f.Adapter.CurrentStarSystemOwnerFactionIndex); Assert.AreEqual(0, f.Adapter.GetRelation(-1));
            f.Load(f.Galaxy.Galaxy.StartStarSystem); Assert.AreEqual(owner, f.Adapter.CurrentStarSystemOwnerFactionIndex);
            f.Loader.OnSceneExiting(new SceneExitContext(GameScene.World, GameScene.MainMenu, SceneEntryReason.QuitToMenu));
            Assert.AreEqual(0, f.Adapter.CurrentStarSystemOwnerFactionIndex); Assert.AreEqual(0, f.Adapter.GetRelation(-1));
        }
        [Test] public void RelationChange_NotifiesDependency()
        {
            using var f = new Fixture();
            using var named = new WatchedCondition(f.Condition("relation(\"faction_vestari\") < 0"), f.Context);
            using var owner = new WatchedCondition(f.Condition("relation(\"owner\") < 0"), f.Context);
            f.Narrative.Register(named); f.Narrative.Register(owner); f.Narrative.BeginNewGame(); f.Narrative.SetWorldReady(true); f.Narrative.Tick();
            named.Evaluations = owner.Evaluations = 0; Assert.IsFalse(named.Watcher.Value); Assert.IsFalse(owner.Watcher.Value);
            f.Adapter.AdjustRelation(f.Catalog.IndexOf(f.Vestari), -5);
            f.TickRelations(1); Assert.IsFalse(named.Watcher.Value, "World events queue dependencies for the next narrative pass.");
            f.Narrative.Tick(); Assert.IsTrue(named.Watcher.Value); Assert.IsTrue(owner.Watcher.Value);
            Assert.AreEqual(1, named.Evaluations); Assert.AreEqual(1, named.Flips); Assert.AreEqual(1, owner.Flips);
            f.Adapter.AdjustRelation(f.Catalog.IndexOf(f.Other), 7); f.TickRelations(2); f.Narrative.Tick();
            Assert.AreEqual(1, named.Evaluations, "An unrelated faction dependency does not re-evaluate this watcher.");
            f.Adapter.AdjustRelation(f.Catalog.IndexOf(f.Vestari), 10); f.TickRelations(3); f.Narrative.Tick();
            Assert.IsFalse(named.Watcher.Value); Assert.AreEqual(2, named.Flips); Assert.AreEqual(2, named.Evaluations);
            f.Bridge.Dispose(); f.Adapter.AdjustRelation(f.Catalog.IndexOf(f.Vestari), -10); f.TickRelations(4); f.Narrative.Tick();
            Assert.AreEqual(2, named.Evaluations); Assert.IsFalse(named.Watcher.Value); Assert.AreEqual(0, EventBus<FactionRelationChanged>.SubscriberCount);
        }
    }
}
