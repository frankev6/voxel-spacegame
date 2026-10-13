using System;
using System.Linq;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Streaming;
using Game.Galaxy.Systems;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class StarSystemLoadTests
    {
        sealed class Ids : IPersistentIdAllocator { ulong next = 1; public ulong Allocate() => next++; }
        sealed class Fixture : IDisposable
        {
            internal readonly World World = new("SYS-004 direct loader");
            internal EntityManager Em => World.EntityManager;
            internal readonly GalaxyState State = new();
            internal readonly GalaxyService Galaxy;
            internal readonly StarSystemLoader Loader;
            internal readonly VoxelStructureRegistry Registry;
            readonly BlueprintLibrary blueprints;
            readonly NativeQueue<ManagedEventEnvelope<OriginShifted>> shifts;
            readonly Entity clock, retirement;
            readonly SystemHandle origin, deferred;
            internal static SceneReadyContext Entry => new(GameScene.World, SceneEntryReason.NewGame, default);
            internal Fixture()
            {
                var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");
                var report = new ValidationReport("Loader fixture");
                var catalog = ContentCatalog.Build(database.Definitions, report); Assert.IsFalse(report.HasErrors, report.ToText());
                blueprints = new BlueprintLibrary(catalog, default); Registry = VoxelStructureRegistry.CreateRuntime(blueprints);
                Em.CreateEntity(typeof(GameplayActive), typeof(WorldSceneActive)); clock = Em.CreateEntity(typeof(SimClock));
                shifts = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent);
                Em.CreateSingleton(new ManagedEventQueue<OriginShifted> { Queue = shifts });
                retirement = Em.CreateEntity(typeof(DeferredDisposalQueue));
                Em.SetComponentData(retirement, new DeferredDisposalQueue { Blobs = new NativeList<RetiredBlob>(Allocator.Persistent), Meshes = new NativeList<RetiredMesh>(Allocator.Persistent) });
                origin = World.CreateSystem<OriginRebaseSystem>(); deferred = World.CreateSystem<DeferredDisposalSystem>();
                Galaxy = new GalaxyService(catalog, State, World, new GalaxyFrameTicker()); Galaxy.NewGame(42);
                State.PlayerLocation = Galaxy.GetStartLocation(); State.HasPlayerLocation = true;
                Loader = new StarSystemLoader(Galaxy, World, Registry, blueprints, new Ids(), catalog);
            }
            internal void Load()
            {
                Loader.OnSceneReady(Entry); Assert.AreEqual(LoaderState.Loading, Loader.State);
                Loader.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 0));
                Assert.AreEqual(LoaderState.Loading, Loader.State, "Must wait for the actual origin owner.");
                origin.Update(World.Unmanaged); Em.CompleteAllTrackedJobs();
                Loader.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 1)); Assert.IsTrue(Loader.IsReady);
            }
            internal void Tick() => Loader.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 2));
            internal void Unload()
            { Loader.OnSceneExiting(new SceneExitContext(GameScene.World, GameScene.MainMenu, SceneEntryReason.QuitToMenu)); }
            internal int Members()
            { using var q = Em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<StarSystemMember>() }, Options = EntityQueryOptions.IncludeDisabledEntities }); return q.CalculateEntityCount(); }
            internal void DrainRetired()
            {
                Em.SetComponentData(clock, new SimClock { Tick = 10 });
                for (int i = 0; i < 3; i++) deferred.Update(World.Unmanaged);
                Assert.AreEqual(0, Em.GetComponentData<DeferredDisposalQueue>(retirement).Blobs.Length);
            }
            public void Dispose()
            {
                Em.CompleteAllTrackedJobs(); Loader.Dispose(); Galaxy.Dispose(); State.Dispose();
                Registry.Dispose(); Object.DestroyImmediate(Registry); blueprints.Dispose();
                var queue = Em.GetComponentData<DeferredDisposalQueue>(retirement);
                foreach (var record in queue.Blobs) { var blob = record.Blob; if (blob.IsCreated) blob.Dispose(); }
                queue.Blobs.Dispose(); queue.Meshes.Dispose(); World.Dispose(); shifts.Dispose();
            }
        }
        [Test] public void Load_CreatesAnchors()
        {
            using var f = new Fixture(); var content = f.Galaxy.GetContent(f.State.CurrentStarSystem);
            var omitted = content.Bodies[0].Id; f.Galaxy.MarkBodyDestroyed(omitted); f.Load();
            using var anchors = f.Em.CreateEntityQuery(typeof(StarSystemBody));
            using var entities = anchors.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(content.Bodies.Length - 1, entities.Length);
            foreach (var entity in entities)
            {
                var body = f.Em.GetComponentData<StarSystemBody>(entity); Assert.AreNotEqual(omitted, body.Id);
                Assert.AreEqual(f.Loader.Loaded, f.Em.GetSharedComponent<StarSystemMember>(entity).Id);
                Assert.IsTrue(f.Em.HasComponent<LocalTransform>(entity)); Assert.IsTrue(f.Em.HasComponent<LocalToWorld>(entity));
                Assert.IsTrue(content.TryFind(body.Id.LocalKey, out int index));
                Assert.AreEqual(OrbitMath.Position(content.Bodies[index], 0), f.Em.GetComponentData<UniversePosition>(entity).Value);
            }
            Assert.IsFalse(f.Loader.IsAreaReady(f.State.PlayerLocation.PositionM), "Unbuilt nearby station grids must not be reported ready.");
        }
        [Test] public void Unload_DestroysAll()
        {
            using var f = new Fixture(); f.Load(); Assert.Greater(f.Members(), 0);
            var stowed = f.Em.CreateEntity(typeof(Disabled)); f.Em.AddSharedComponent(stowed, new StarSystemMember { Id = f.Loader.Loaded });
            f.Em.CreateEntity(typeof(ChunkBody)); f.Unload(); Assert.AreEqual(0, f.Members());
            using var chunks = f.Em.CreateEntityQuery(typeof(ChunkBody)); Assert.IsTrue(chunks.IsEmptyIgnoreFilter);
            Assert.IsFalse(f.Em.Exists(stowed)); Assert.AreEqual(LoaderState.Idle, f.Loader.State); f.DrainRetired();
            // Content is released by unload, so a later lookup is a fresh generation.
            f.Galaxy.GetContent(f.State.CurrentStarSystem); Assert.AreEqual(2, f.Galaxy.ContentGenerationCount(f.State.CurrentStarSystem));
        }
        [Test] public void Adoption_SplitPieces()
        {
            using var f = new Fixture(); f.Load(); var other = new StarSystemId(123);
            var parent = f.Em.CreateEntity(); f.Em.AddSharedComponent(parent, new StarSystemMember { Id = other });
            var child = f.Em.CreateEntity(); var root = f.Em.CreateEntity(typeof(SplitOrigin));
            f.Em.SetComponentData(root, new SplitOrigin { Parent = parent }); var linked = f.Em.AddBuffer<LinkedEntityGroup>(root);
            linked.Add(new LinkedEntityGroup { Value = root }); linked.Add(new LinkedEntityGroup { Value = child });
            var player = f.Em.CreateEntity(typeof(PlayerShip)); var playerPiece = f.Em.CreateEntity(typeof(SplitOrigin));
            f.Em.SetComponentData(playerPiece, new SplitOrigin { Parent = player }); f.Tick();
            Assert.AreEqual(other, f.Em.GetSharedComponent<StarSystemMember>(root).Id);
            Assert.AreEqual(other, f.Em.GetSharedComponent<StarSystemMember>(child).Id);
            Assert.AreEqual(f.Loader.Loaded, f.Em.GetSharedComponent<StarSystemMember>(playerPiece).Id);
            Assert.IsFalse(f.Em.HasComponent<SplitOrigin>(root)); Assert.IsFalse(f.Em.HasComponent<SplitOrigin>(playerPiece));
            Assert.IsFalse(f.Em.HasComponent<StarSystemMember>(player));
        }
        [Test] public void Hook_TagsBricks()
        {
            using var f = new Fixture(); f.Load(); var root = f.Em.CreateEntity();
            f.Em.AddSharedComponent(root, new StarSystemMember { Id = f.Loader.Loaded });
            using var created = new NativeArray<Entity>(new[] { f.Em.CreateEntity(), f.Em.CreateEntity() }, Allocator.Temp);
            var hook = f.Registry.SpawnHooks.OfType<StarSystemSpawnHook>().Single(); hook.OnEntitiesCreated(f.Em, root, created);
            foreach (var entity in created) Assert.AreEqual(f.Loader.Loaded, f.Em.GetSharedComponent<StarSystemMember>(entity).Id);
            f.Loader.Dispose(); Assert.IsFalse(f.Registry.SpawnHooks.Contains(hook));
        }
        [Test] public void EnteredOnce()
        {
            EventBus<StarSystemEntered>.Drain(); int entries = 0; uint visits = 0;
            void OnEntry(StarSystemEntered e) { entries++; visits = e.VisitCount; }
            EventBus<StarSystemEntered>.Subscribe(OnEntry);
            try
            {
                using var f = new Fixture(); f.Load(); for (int i = 0; i < 3; i++) f.Tick();
                EventBus<StarSystemEntered>.Drain(); Assert.AreEqual(1, entries); Assert.AreEqual(1, visits);
                f.Unload(); f.Load(); EventBus<StarSystemEntered>.Drain(); Assert.AreEqual(2, entries); Assert.AreEqual(2, visits);
            }
            finally { EventBus<StarSystemEntered>.Unsubscribe(OnEntry); }
        }
    }
}
