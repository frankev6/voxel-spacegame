using System;
using System.Collections.Generic;
using System.Threading;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed class GridBodyInitTests
    {
        internal sealed class Fixture : IDisposable
        {
            public readonly VoxelTestContent Content = new VoxelTestContent();
            readonly BlueprintLibrary library;
            readonly VoxelStructureRegistry registry;
            readonly List<VoxelStructure> sources = new List<VoxelStructure>();
            public readonly EcsTestWorld Test;
            public EntityManager Em => Test.EntityManager;
            public ColliderBuildCache Cache => Singleton<ColliderBuildCache>();
            public ColliderDirtyQueue Queue => Singleton<ColliderDirtyQueue>();
            public ColliderBatchQueue Batch => Singleton<ColliderBatchQueue>();
            public Fixture(bool physics = false, params Type[] extra)
            {
                var systems = new List<Type> { typeof(GridSpawnSystem), typeof(GridBodyInitSystem), typeof(ColliderSwapSystem), typeof(GameplayEndCommandBufferSystem) };
                systems.AddRange(extra);
                if (physics)
                    foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                        if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
                Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddSimClock(20);
                Content.Service.CreateSingletons(Em); library = new BlueprintLibrary(Content.Service.Catalog, Content.Content);
                registry = VoxelStructureRegistry.CreateRuntime(library); Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f }); Em.CreateSingleton(PhysicsVoxelConfig.Default);
                if (physics) { var step = PhysicsStep.Default; step.Gravity = 0; step.SynchronizeCollisionWorld = 1; Em.CreateSingleton(step); }
            }
            public T Singleton<T>() where T : unmanaged, IComponentData
            { using var query = Em.CreateEntityQuery(typeof(T)); return query.GetSingleton<T>(); }
            public Entity Spawn(GridMotion motion = GridMotion.Dynamic, GridSpawnFlags flags = GridSpawnFlags.PlanarInertia,
                bool station = false, float3 linear = default, float3 angular = default, quaternion rotation = default)
            {
                var source = station ? BenchmarkStructures.HollowStation1M(Content.Index("block_hull")) : BenchmarkStructures.Ship20k(Content.Index("block_hull"));
                sources.Add(source);
                if (math.lengthsq(rotation.value) == 0) rotation = quaternion.identity;
                Entity root = GridSpawnApi.RequestSpawn(Em, registry, registry.Register(source), new GridSpawnParams
                { Pose = new RigidTransform(rotation, float3.zero), Motion = motion, Flags = flags | GridSpawnFlags.NoRender | GridSpawnFlags.Transient,
                    LinearVelocity = linear, AngularVelocity = angular });
                Test.UpdateSystem<GridSpawnSystem>(); Test.CompleteAll(); Assert.IsFalse(Em.IsComponentEnabled<GridSpawning>(root)); return root;
            }
            public void Tick() { Test.StepTicks(1); Cache.CompleteAccess(); Queue.CompleteAccess(); }
            public void Dispose()
            {
                Test.Dispose(); Object.DestroyImmediate(registry); foreach (var source in sources) source.Dispose(); library.Dispose(); Content.Dispose();
            }
        }
        static void PhysicsTypes(EntityManager em, Entity root, ComponentTypeSet types)
        {
            var expected = new HashSet<Type> { typeof(PhysicsWorldIndex) };
            for (int i = 0; i < types.Length; i++) expected.Add(TypeManager.GetType(types.GetTypeIndex(i)));
            var actual = new HashSet<Type>(); using var components = em.GetComponentTypes(root);
            foreach (var component in components)
            { var type = TypeManager.GetType(component.TypeIndex); if (type.Namespace != null && type.Namespace.StartsWith("Unity.Physics")) actual.Add(type); }
            CollectionAssert.AreEquivalent(expected, actual); Assert.AreEqual(0, em.GetSharedComponent<PhysicsWorldIndex>(root).Value);
        }
        static unsafe void Provisional(Fixture f, Entity root, bool preview = false)
        {
            var grid = f.Em.GetComponentData<VoxelGrid>(root); var state = f.Em.GetComponentData<GridColliderState>(root);
            Assert.AreEqual(GridColliderPhase.Provisional, state.Phase); Assert.AreEqual(1, state.BoxCount); Assert.AreEqual(0, state.ChildCount);
            var blob = f.Em.GetComponentData<PhysicsCollider>(root).Value; var box = (BoxCollider*)blob.GetUnsafePtr();
            Assert.AreEqual(((float3)grid.CellMin + (float3)grid.CellMax + 1) * (.5f * grid.CellSize), box->Center);
            Assert.AreEqual(((float3)grid.CellMax - (float3)grid.CellMin + 1) * grid.CellSize, box->Size);
            Assert.AreEqual(0, box->BevelRadius); Assert.AreEqual(CollisionResponsePolicy.None, box->Material.CollisionResponse);
            Assert.AreEqual(preview ? PhysicsLayers.EditorPreview : PhysicsLayers.Grid, box->GetCollisionFilter());
            Assert.IsFalse(f.Em.IsComponentEnabled<GridBodyInit>(root));
            var cache = f.Cache; Assert.AreEqual(blob, cache.C.Grids[cache.C.GridIndex[grid.Serial]].Installed);
        }
        [Test] public void Dynamic_GetsBodyComponents()
        {
            using var f = new Fixture(); var rotation = quaternion.RotateY(.7f);
            Entity root = f.Spawn(linear: new float3(1, 2, 3), angular: new float3(2, 3, 4), rotation: rotation);
            Entity custom = f.Spawn(); var damping = new PhysicsDamping { Linear = .2f, Angular = .7f }; f.Em.AddComponentData(custom, damping);
            f.Tick(); PhysicsTypes(f.Em, root, GridBodyFactory.DynamicBodyTypes()); Provisional(f, root);
            var grid = f.Em.GetComponentData<VoxelGrid>(root); var mass = f.Em.GetComponentData<GridMass>(root).Data;
            Assert.AreEqual(GridMassMath.CreateDynamicMass(mass, grid.CellMin, grid.CellMax, grid.CellSize, true), f.Em.GetComponentData<PhysicsMass>(root));
            var velocity = f.Em.GetComponentData<PhysicsVelocity>(root); Assert.AreEqual(new float3(1, 2, 3), velocity.Linear); Assert.AreEqual(new float3(0, 3, 0), velocity.Angular);
            Assert.AreEqual(damping, f.Em.GetComponentData<PhysicsDamping>(custom));
            Assert.AreEqual(GridBodyFactory.DefaultDamping(PhysicsVoxelConfig.Default), f.Em.GetComponentData<PhysicsDamping>(root));
            Assert.AreEqual(velocity, f.Em.GetComponentData<PhysicsGraphicalInterpolationBuffer>(root).PreviousVelocity);
            Assert.AreEqual(new RigidTransform(rotation, float3.zero), f.Em.GetComponentData<PhysicsGraphicalInterpolationBuffer>(root).PreviousTransform);
            Assert.AreEqual(1, f.Em.GetComponentData<PhysicsGraphicalSmoothing>(root).ApplySmoothing);
            uint previous = 0; foreach (var request in f.Queue.Requests) { Assert.GreaterOrEqual(request.Serial, previous); previous = request.Serial; Assert.AreEqual(ColliderPriority.Spawn, request.Priority); }
        }
        [Test] public void Static_OnlyCollider()
        { using var f = new Fixture(); var root = f.Spawn(GridMotion.Static, station: true); f.Tick(); PhysicsTypes(f.Em, root, GridBodyFactory.StaticBodyTypes()); Provisional(f, root); }
        [Test] public void Kinematic_InfiniteMass()
        {
            using var f = new Fixture(); var rotation = quaternion.EulerXYZ(.2f, .5f, -.1f); var angular = new float3(1, 2, 3);
            var root = f.Spawn(GridMotion.Kinematic, GridSpawnFlags.None, linear: new float3(3, 2, 1), angular: angular, rotation: rotation);
            f.Tick(); PhysicsTypes(f.Em, root, GridBodyFactory.KinematicBodyTypes()); Provisional(f, root);
            var mass = f.Em.GetComponentData<PhysicsMass>(root); Assert.AreEqual(0, mass.InverseMass); Assert.AreEqual(float3.zero, mass.InverseInertia);
            var velocity = f.Em.GetComponentData<PhysicsVelocity>(root); Assert.AreEqual(new float3(3, 2, 1), velocity.Linear);
            Assert.Less(math.distance(angular, math.rotate(math.mul(rotation, mass.InertiaOrientation), velocity.Angular)), 1e-5f);
        }
        [Test] public void EditorPreview_QueryOnlyFilter()
        {
            using var f = new Fixture(); var root = f.Spawn(GridMotion.Static, GridSpawnFlags.EditorPreview); f.Tick(); Provisional(f, root, true);
            Assert.AreEqual(1, f.Em.GetComponentData<GridColliderState>(root).IsEditorPreview);
            foreach (var request in f.Queue.Requests) Assert.AreEqual(ColliderPriority.Edit, request.Priority);
        }
        [Test] public void EmptyMass_WaitsOneTick()
        {
            using var f = new Fixture(extra: new[] { typeof(GridMassUpdateSystem) }); var root = f.Spawn(); var exact = f.Em.GetComponentData<GridMass>(root);
            f.Em.SetComponentData(root, new GridMass()); f.Tick(); Assert.IsFalse(f.Em.HasComponent<PhysicsCollider>(root));
            Assert.IsTrue(f.Em.IsComponentEnabled<GridBodyInit>(root)); Assert.IsFalse(f.Em.IsComponentEnabled<GridMassRecompute>(root)); Assert.AreEqual(0, f.Queue.Requests.Length);
            Assert.AreEqual(exact.Data, f.Em.GetComponentData<GridMass>(root).Data);
            f.Tick(); Provisional(f, root);
            var grid = f.Em.GetComponentData<VoxelGrid>(root);
            Assert.AreEqual(GridMassMath.CreateDynamicMass(exact.Data, grid.CellMin, grid.CellMax, grid.CellSize, true), f.Em.GetComponentData<PhysicsMass>(root));
        }
        [Test] public void ExistingBody_AdoptsProvisional_KeepsVelocity()
        {
            using var f = new Fixture(); var root = f.Spawn(); var grid = f.Em.GetComponentData<VoxelGrid>(root);
            var blob = GridBodyFactory.CreateProvisionalCollider(grid.CellMin, grid.CellMax, grid.CellSize, false);
            var velocity = new PhysicsVelocity { Linear = new float3(7, 8, 9), Angular = new float3(1, 2, 3) };
            f.Em.AddComponent(root, GridBodyFactory.DynamicBodyTypes()); f.Em.AddSharedComponent(root, GridBodyFactory.WorldIndex());
            f.Em.SetComponentData(root, new PhysicsCollider { Value = blob }); f.Em.SetComponentData(root, velocity);
            f.Tick(); Assert.AreEqual(velocity, f.Em.GetComponentData<PhysicsVelocity>(root)); Assert.AreEqual(blob, f.Cache.C.Grids[0].Installed);
            foreach (var request in f.Queue.Requests) Assert.AreEqual(ColliderPriority.Split, request.Priority);
            f.Tick(); Assert.AreEqual(0, f.Cache.C.Retired.Length); Assert.AreEqual(1, f.Cache.C.Grids.Length);
            ColliderPipelineInvariants.Check(f.Cache, f.Batch);
        }
        [Test] public void Rebuild_KeepsCompound_RequeuesBricks()
        {
            using var f = new Fixture(); var root = f.Spawn(); f.Tick(); f.Queue.Requests.Clear();
            var cache = f.Cache; var batch = f.Batch; var record = cache.C.Grids[0];
            var child = GridBodyFactory.CreateProvisionalCollider(0, 0, .5f, false); record.Children.Add(new BrickChild { Blob = child, BoxCount = 1 }); cache.C.Grids[0] = record;
            var stats = cache.C.Stats.Value; stats.CachedBricks++; stats.CachedBoxes++; stats.CachedBrickBytes += ColliderPipeline.Bytes(child); cache.C.Stats.Value = stats;
            var children = new NativeArray<BrickChild>(1, Allocator.Temp); children[0] = record.Children[0];
            var installed = GridColliderAssembler.Assemble(children, .5f); children.Dispose();
            int ready = ColliderPipeline.GetOrCreateUpdate(ref cache.C, record.Serial, 0, 20, ColliderPriority.Spawn);
            ColliderPipeline.Seal(ref cache.C, ready); var built = cache.C.Updates[ready]; built.State = UpdateState.Built;
            built.Compound = installed; built.ChildCount = built.BoxCount = 1; cache.C.Updates[ready] = built;
            stats = cache.C.Stats.Value; stats.PendingCompoundBytes += ColliderPipeline.Bytes(installed); cache.C.Stats.Value = stats;
            f.Tick(); Assert.AreEqual(GridColliderPhase.Ready, f.Em.GetComponentData<GridColliderState>(root).Phase);
            Assert.AreEqual(ColliderType.Compound, f.Em.GetComponentData<PhysicsCollider>(root).Value.Value.Type);
            int retiredBefore = cache.C.Retired.Length;
            int update = ColliderPipeline.GetOrCreateUpdate(ref cache.C, record.Serial, 1000, 20, ColliderPriority.Damage);
            var mask = new BrickMaskBits(); mask.Set(0, true); ColliderPipeline.SetSnapshot(ref cache.C, update, 0, mask, false); ColliderPipeline.Seal(ref cache.C, update);
            batch.B.Masks.Add(ColliderPipeline.TakeNextMask(ref cache.C, update, out _)); batch.B.Bricks.Add(new BrickBuildItem { Update = update, Serial = record.Serial, CellSize = .5f });
            batch.B.Results.Add(default);
            var handle = new BrickColliderJob { Items = batch.B.Bricks.AsArray(), Masks = batch.B.Masks.AsArray(), Results = batch.B.Results.AsArray(), MaxBoxes = 64, Bevel = .05f }
                .Schedule(1, 1, new DelayJob().Schedule());
            batch.State.Value = new ColliderBatchState { InFlight = 1, Handle = handle }; JobHandle.ScheduleBatchedJobs();
            // Construction reads immutable descriptors while the init job detaches their old update.
            f.Em.SetComponentEnabled<GridRebuilt>(root, true); f.Tick();
            Assert.IsFalse(handle.IsCompleted, "Rebuild must not wait for detached construction");
            Assert.AreEqual(installed, f.Em.GetComponentData<PhysicsCollider>(root).Value); Assert.AreEqual(installed, cache.C.Grids[0].Installed);
            Assert.AreEqual(0, cache.C.Grids[0].Children.Length); Assert.AreEqual(0, cache.C.Grids[0].Updates.Length);
            Assert.AreEqual(UpdateState.Discarded, cache.C.Updates[update].State); Assert.AreEqual(0, cache.C.FreeUpdates.Length);
            Assert.IsTrue(f.Em.IsComponentEnabled<GridMassRecompute>(root)); Assert.IsFalse(f.Em.IsComponentEnabled<GridRebuilt>(root));
            Assert.AreEqual(GridColliderPhase.Provisional, f.Em.GetComponentData<GridColliderState>(root).Phase);
            int bricks = 0; foreach (var slot in f.Em.GetBuffer<BrickSlot>(root)) if (slot.Brick != Entity.Null) bricks++;
            Assert.AreEqual(bricks, f.Queue.Requests.Length); foreach (var request in f.Queue.Requests) Assert.AreEqual(ColliderPriority.Spawn, request.Priority);
            handle.Complete(); ColliderPipeline.Finalize(ref cache, ref batch); batch.State.Value = default;
            Assert.AreEqual(retiredBefore + 2, cache.C.Retired.Length); Assert.AreEqual(1, cache.C.FreeUpdates.Length);
            ColliderPipelineInvariants.Check(cache, batch);
        }
        struct DelayJob : IJob { public void Execute() => Thread.Sleep(500); }
        [Test] public void Provisional_DoesNotPush()
        {
            using var f = new Fixture(physics: true); var station = f.Spawn(GridMotion.Static, station: true); var ship = f.Spawn();
            f.Tick(); Provisional(f, station); Provisional(f, ship);
            for (int i = 0; i < 10; i++) f.Tick();
            Assert.GreaterOrEqual(f.Singleton<PhysicsWorldSingleton>().NumBodies, 2);
            Assert.AreEqual(float3.zero, f.Em.GetComponentData<LocalTransform>(ship).Position);
            Assert.AreEqual(float3.zero, f.Em.GetComponentData<PhysicsVelocity>(ship).Linear);
        }
    }
}
