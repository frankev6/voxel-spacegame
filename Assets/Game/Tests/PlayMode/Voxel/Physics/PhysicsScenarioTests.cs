using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Game.Tests.PlayMode.Voxel.Physics
{
    /// <summary>PHY-017: real fixed-step voxel bodies in an isolated PlayMode scenario scene.</summary>
    [Category("Voxel"), Category("Scenario")]
    public sealed class PhysicsScenarioTests
    {
        Scene scene;
        [SetUp] public void SetUp() => scene = SceneManager.CreateScene("PHY-017 scenarios");
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var unload = SceneManager.UnloadSceneAsync(scene); float until = Time.realtimeSinceStartup + 10;
            while (!unload.isDone && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(unload.isDone, "Scenario scene unload timed out");
        }

        internal sealed class Fixture : IDisposable
        {
            readonly ContentService content;
            readonly BlueprintLibrary library;
            readonly VoxelStructureRegistry registry;
            readonly List<VoxelStructure> sources = new List<VoxelStructure>();
            public readonly EcsTestWorld Test;
            public EntityManager Em => Test.EntityManager;
            public readonly ushort Hull;
            public Fixture(params Type[] extra)
            {
                content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
                Hull = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                library = new BlueprintLibrary(content.Catalog, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
                registry = VoxelStructureRegistry.CreateRuntime(library);
                var systems = new List<Type> { typeof(GridSpawnSystem), typeof(GridBodyInitSystem), typeof(GridMassUpdateSystem),
                    typeof(BrickDirtyScheduleSystem), typeof(ColliderSnapshotSystem), typeof(BrickColliderBuildSystem), typeof(ColliderSwapSystem),
                    typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem), typeof(GameplayEndCommandBufferSystem) };
                systems.AddRange(extra);
                foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                    if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
                Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddSimClock(); content.CreateSingletons(Em);
                Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
                var config = content.Catalog.Get<PhysicsVoxelConfigDefinition>(new ContentId("config_physics"));
                Em.CreateSingleton(PhysicsVoxelConfigFactory.Create(config)); Em.CreateSingleton(PhysicsVoxelConfigFactory.CreatePhysicsStep(config));
            }
            public T Singleton<T>() where T : unmanaged, IComponentData
            { using var q = Em.CreateEntityQuery(typeof(T)); return q.GetSingleton<T>(); }
            public Entity Spawn(bool station = false, float3 position = default, float3 velocity = default, float3 angular = default)
            {
                var source = station ? BenchmarkStructures.HollowStation1M(Hull) : BenchmarkStructures.Ship20k(Hull); sources.Add(source);
                return GridSpawnApi.RequestSpawn(Em, registry, registry.Register(source), new GridSpawnParams
                {
                    Pose = new RigidTransform(quaternion.identity, position), Motion = station ? GridMotion.Static : GridMotion.Dynamic,
                    Flags = GridSpawnFlags.PlanarInertia | GridSpawnFlags.NoRender | GridSpawnFlags.Transient,
                    LinearVelocity = velocity, AngularVelocity = angular,
                });
            }
            public Entity SpawnRegistered(int handle, GridMotion motion = GridMotion.Static) => GridSpawnApi.RequestSpawn(Em, registry, handle,
                new GridSpawnParams { Pose = RigidTransform.identity, Motion = motion, Flags = GridSpawnFlags.NoRender | GridSpawnFlags.Transient });
            public void Tick(int count = 1) => Test.StepTicks(count);
            public void Ready(params Entity[] roots)
            {
                for (int frame = 0; frame < 30; frame++)
                {
                    Tick(); bool ready = true;
                    foreach (var root in roots) ready &= Em.HasComponent<GridColliderState>(root) && Em.GetComponentData<GridColliderState>(root).Phase == GridColliderPhase.Ready;
                    if (ready) return;
                }
                Assert.Fail("Full colliders were not ready in 30 scenario frames");
            }
            public float3 Position(Entity root) => Em.GetComponentData<LocalTransform>(root).Position;
            public PhysicsVelocity Velocity(Entity root) => Em.GetComponentData<PhysicsVelocity>(root);
            public void Dispose()
            { Test.Dispose(); Object.DestroyImmediate(registry); foreach (var source in sources) source.Dispose(); library.Dispose(); content.Dispose(); }
        }

        [Test] public void Ram_PushesBoth_MomentumConserved()
        {
            using var f = new Fixture(); Entity moving = f.Spawn(position: new float3(-50, 0, 0), velocity: new float3(20, 0, 0)); Entity resting = f.Spawn();
            f.Ready(moving, resting);
            double m1 = f.Em.GetComponentData<GridMass>(moving).Data.Mass, m2 = f.Em.GetComponentData<GridMass>(resting).Data.Mass;
            double3 before = m1 * (double3)f.Velocity(moving).Linear + m2 * (double3)f.Velocity(resting).Linear;
            Assert.AreEqual(20, f.Velocity(moving).Linear.x, 1e-5); Assert.AreEqual(float3.zero, f.Velocity(resting).Linear);
            f.Tick(120);
            var a = f.Velocity(moving).Linear; var b = f.Velocity(resting).Linear;
            Assert.Greater(a.x, .1f); Assert.Less(a.x, 19.9f); Assert.Greater(b.x, .1f);
            double error = math.length(m1 * (double3)a + m2 * (double3)b - before) / math.length(before);
            Assert.LessOrEqual(error, .02); TestContext.WriteLine($"Ram: velocities {a}, {b}; momentum relative error {error:G8}");
        }

        [Test] public void OffCentreImpulse_YawOnly()
        {
            using var f = new Fixture(); Entity ship = f.Spawn(); f.Ready(ship);
            var mass = f.Em.GetComponentData<PhysicsMass>(ship); var transform = f.Em.GetComponentData<LocalTransform>(ship); var velocity = f.Velocity(ship);
            // The vertical offset would also produce pitch/roll if planar inverse inertia were missing.
            float3 point = transform.TransformPoint(mass.CenterOfMass) + new float3(0, 3, 6);
            velocity.ApplyImpulse(mass, transform.Position, transform.Rotation, new float3(100000, 0, 0), point);
            f.Em.SetComponentData(ship, velocity); f.Tick(30); var after = f.Velocity(ship).Angular;
            Assert.Greater(math.abs(after.y), .001f); Assert.AreEqual(0, after.x, 1e-7); Assert.AreEqual(0, after.z, 1e-7);
            TestContext.WriteLine($"Off-centre impulse: inertia-frame angular velocity {after}");
        }

        [Test] public void Hole_ColliderUpdatesAtInstallTick()
        {
            using var f = new Fixture(); Entity station = f.Spawn(station: true); f.Ready(station);
            var ray = new RaycastInput { Start = new float3(-1, 16.75f, 16.75f), End = new float3(3, 16.75f, 16.75f), Filter = PhysicsLayers.ProjectileQuery };
            Assert.IsTrue(f.Singleton<PhysicsWorldSingleton>().CastRay(ray, out RaycastHit initial)); Assert.AreEqual(station, initial.Entity);
            var grid = f.Em.GetComponentData<VoxelGrid>(station); grid.Revision++;
            for (int z = 32; z < 36; z++) for (int y = 32; y < 36; y++) for (int x = 0; x < 4; x++)
            {
                int3 cell = new int3(x, y, z); Entity entity = f.Em.GetBuffer<BrickSlot>(station)[BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims)].Brick;
                var brick = f.Em.GetComponentData<Brick>(entity); var dirty = f.Em.GetComponentData<BrickDirty>(entity);
                Assert.IsTrue(BrickWrite.Set(ref brick, f.Em.GetBuffer<BrickCells>(entity), f.Em.GetBuffer<BrickSparseCells>(entity),
                    f.Em.GetBuffer<BrickSolidMask>(entity), ref dirty, BrickMath.LocalIndex(BrickMath.LocalOf(cell)), default, grid.Revision));
                f.Em.SetComponentData(entity, brick); f.Em.SetComponentData(entity, dirty); f.Em.SetComponentEnabled<BrickDirty>(entity, true);
            }
            f.Em.SetComponentData(station, grid); f.Em.SetComponentEnabled<GridMassRecompute>(station, true);
            uint requestTick = f.Singleton<SimClock>().Tick; var config = f.Singleton<PhysicsVoxelConfig>();
            uint bucket = (uint)config.InstallBucketTicks; uint installTick = (requestTick + (uint)config.InstallDelayTicks + bucket - 1) / bucket * bucket;
            for (uint tick = requestTick; tick <= installTick + 1; tick++)
            {
                f.Tick(); bool hit = f.Singleton<PhysicsWorldSingleton>().CastRay(ray, out RaycastHit _);
                Assert.AreEqual(tick < installTick, hit, "Hole ray at tick " + tick);
                if (tick >= installTick) Assert.AreEqual(installTick, f.Em.GetComponentData<GridColliderState>(station).InstalledTick);
            }
            Assert.AreEqual(0, GridColliderStats.Gather(f.Em).LateInstalls);
            TestContext.WriteLine($"64 cells removed at {requestTick}; ray blocked before {installTick}, clear from that install tick");
        }

        [Test] public void ProvisionalStation_DoesNotPush()
        {
            using var f = new Fixture(); Entity ship = f.Spawn(); f.Ready(ship); float3 original = f.Position(ship);
            Entity station = f.Spawn(station: true); int provisionalTicks = 0;
            for (int frame = 0; frame < 30; frame++)
            {
                f.Tick(); var state = f.Em.GetComponentData<GridColliderState>(station);
                if (state.Phase == GridColliderPhase.Ready) break;
                if (state.Phase == GridColliderPhase.Provisional) provisionalTicks++;
                Assert.Less(math.distance(original, f.Position(ship)), .001f);
            }
            Assert.Greater(provisionalTicks, 0); Assert.AreEqual(GridColliderPhase.Ready, f.Em.GetComponentData<GridColliderState>(station).Phase);
            TestContext.WriteLine($"Overlapping station: ship stays within 1 mm for {provisionalTicks} provisional ticks");
        }

        [Test] public void Planar_NoPitchRoll()
        {
            using var f = new Fixture(); Entity ship = f.Spawn(angular: new float3(10, 1, 4)); f.Ready(ship);
            for (int i = 0; i < 120; i++)
            {
                f.Tick(); var velocity = f.Velocity(ship); var rotation = f.Em.GetComponentData<LocalTransform>(ship).Rotation;
                Assert.AreEqual(0, velocity.Angular.x, 1e-7); Assert.AreEqual(0, velocity.Angular.z, 1e-7);
                Assert.Less(math.distance(new float3(0, 1, 0), math.rotate(rotation, new float3(0, 1, 0))), 1e-6f);
            }
            Assert.Greater(math.abs(f.Velocity(ship).Angular.y), .1f);
        }
    }
}
