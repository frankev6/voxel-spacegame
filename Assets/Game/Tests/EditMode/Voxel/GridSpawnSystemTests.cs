using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-013 required integration cases: real content, native sources, and the budgeted spawn system.</summary>
    public sealed class GridSpawnSystemTests
    {
        struct Requester : IComponentData { public int Value; }
        struct ProfileData : IComponentData { public int Value; }
        struct Universal : IComponentData { public int Value; }
        struct ThrusterData : IComponentData { public int Value; }
        struct SourceData : IComponentData { public int Value; }
        struct ExplosiveData : IComponentData { public int Value; }
        sealed class Hook : IGridSpawnHook
        {
            public readonly List<Entity> Created = new List<Entity>();
            public void OnEntitiesCreated(EntityManager em, Entity root, NativeArray<Entity> created)
            { foreach (var entity in created) { Assert.IsTrue(em.Exists(entity)); Created.Add(entity); } }
        }
        VoxelTestContent fixture;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        World world;
        SystemHandle system;
        readonly List<VoxelStructure> sources = new List<VoxelStructure>();
        EntityManager EM => world.EntityManager;
        [SetUp] public void SetUp()
        {
            fixture = new VoxelTestContent(includeExplosive: true);
            library = new BlueprintLibrary(fixture.Service.Catalog, fixture.Content);
            registry = VoxelStructureRegistry.CreateRuntime(library);
            world = new World("GridSpawnSystemTests");
            fixture.Service.CreateSingletons(EM);
            EM.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
            EM.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            EM.CreateEntity(typeof(GameplayActive)); system = world.CreateSystem<GridSpawnSystem>();
        }
        [TearDown] public void TearDown()
        {
            world?.Dispose(); Object.DestroyImmediate(registry);
            foreach (var source in sources) source.Dispose(); sources.Clear();
            library?.Dispose(); fixture?.Dispose();
        }
        T Singleton<T>() where T : unmanaged, IComponentData
        { using var q = EM.CreateEntityQuery(typeof(T)); return q.GetSingleton<T>(); }
        Entity SingletonEntity<T>() where T : unmanaged
        { using var q = EM.CreateEntityQuery(ComponentType.ReadOnly<T>()); return q.GetSingletonEntity(); }
        void Tick() => system.Update(world.Unmanaged);
        VoxelStructure Own(VoxelStructure source) { sources.Add(source); return source; }
        VoxelStructure Ship() => Own(BenchmarkStructures.Ship20k(fixture.Index("block_hull")));
        VoxelStructure Single()
        {
            var source = Own(new VoxelStructure());
            ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = fixture.Index("block_hull") });
            source.SetBrickUniform(new int3(0), Cell.Palette(palette)); return source;
        }
        Entity Spawn(VoxelStructure source, GridProfile profile = GridProfile.None)
            => GridSpawnApi.RequestSpawn(EM, registry, registry.Register(source), new GridSpawnParams
            { PersistentId = 42, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender, Profile = profile, Motion = GridMotion.Dynamic });
        void Place(VoxelStructure source, string block, int3 origin)
            => Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = fixture.Index(block), Origin = origin }, fixture.Content));
        VoxelStructure WithFunctional()
        {
            var source = Ship();
            Place(source, "block_core_cross", new int3(5, 11, 5));
            Place(source, "block_thruster_medium", new int3(15, 11, 5));
            Place(source, "block_thruster_medium", new int3(25, 11, 5));
            return source;
        }
        [Test] public void Spawn_PrefillsMass()
        {
            var source = Ship(); var expected = source.GetMass(fixture.Content);
            var root = Spawn(source); Tick();
            Assert.AreEqual(expected, EM.GetComponentData<GridMass>(root).Data);
            Assert.IsFalse(EM.IsComponentEnabled<GridMassRecompute>(root));
            Assert.AreEqual(0, source.Data.Header.Value.PinCount);
        }
        [Test] public void Spawn_Ship20k_CreatesExpectedEntities()
        {
            var source = WithFunctional(); Assert.AreEqual(10, source.BrickCount);
            var root = Spawn(source); Tick(); var grid = EM.GetComponentData<VoxelGrid>(root);
            Assert.AreEqual(42, EM.GetComponentData<PersistentId>(root).Value);
            Assert.AreEqual(14, EM.GetBuffer<LinkedEntityGroup>(root).Length);
            Assert.AreEqual(0, grid.AnchorInstance);
            var slots = EM.GetBuffer<BrickSlot>(root);
            for (int b = 0; b < source.BrickCount; b++)
            {
                var record = source.Data.Bricks[b]; var entity = slots[BrickMath.SlotIndex(record.Coord, grid.BrickMin, grid.BrickDims)].Brick;
                Assert.AreEqual(root, EM.GetComponentData<Brick>(entity).Grid);
                Assert.AreEqual(record.SolidCount, EM.GetComponentData<Brick>(entity).SolidCount);
                var transform = EM.GetComponentData<LocalTransform>(entity);
                Assert.AreEqual(source.CellSize, transform.Scale);
                Assert.AreEqual((float3)record.Coord * (16 * source.CellSize), transform.Position);
                var cells = EM.GetBuffer<BrickCells>(entity); var masks = EM.GetBuffer<BrickSolidMask>(entity);
                var sparse = EM.GetBuffer<BrickSparseCells>(entity);
                Assert.AreEqual(record.SolidCount <= 1024 ? 0 : 4096, cells.Length); Assert.AreEqual(64, masks.Length);
                for (int i = 0; i < 4096; i++)
                {
                    var expected = source.GetCell(BrickMath.CellOf(record.Coord, i));
                    Assert.AreEqual(expected, BrickWrite.Get(cells, sparse, i));
                    Assert.AreEqual(!expected.IsAir, (masks[i >> 6].Bits & (1UL << (i & 63))) != 0);
                }
            }
            var instances = EM.GetBuffer<BlockInstance>(root);
            for (int i = 0; i < instances.Length; i++)
            {
                var functional = EM.GetComponentData<FunctionalBlock>(instances[i].Functional);
                Assert.AreEqual(root, functional.Grid); Assert.AreEqual(i, functional.InstanceIndex);
            }
        }
        [Test] public void RequesterComponents_Preserved()
        {
            var root = Spawn(Single()); EM.AddComponentData(root, new Requester { Value = 83 }); Tick();
            Assert.AreEqual(83, EM.GetComponentData<Requester>(root).Value);
        }
        [Test] public void Hook_ReceivesAllCreated()
        {
            var hook = new Hook(); registry.AddSpawnHook(hook); var root = Spawn(WithFunctional()); Tick();
            var expected = new List<Entity>(); foreach (var entry in EM.GetBuffer<LinkedEntityGroup>(root)) if (entry.Value != root) expected.Add(entry.Value);
            CollectionAssert.AreEquivalent(expected, hook.Created); Assert.AreEqual(hook.Created.Count, new HashSet<Entity>(hook.Created).Count);
        }
        [Test] public void Budget_SpreadsAcrossFrames()
        {
            var source = Own(BenchmarkStructures.Lattice2500(fixture.Index("block_hull")));
            int handle = registry.Register(source);
            var root = GridSpawnApi.RequestSpawn(EM, registry, handle, new GridSpawnParams { PersistentId = 1, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender });
            Assert.AreEqual(1, source.Data.Header.Value.PinCount); registry.Release(handle);
            Tick(); Assert.AreEqual(1025, EM.GetBuffer<LinkedEntityGroup>(root).Length); Assert.IsTrue(source.IsPinned);
            Tick(); Assert.AreEqual(2049, EM.GetBuffer<LinkedEntityGroup>(root).Length); Assert.IsTrue(source.IsPinned);
            Tick(); Assert.AreEqual(2501, EM.GetBuffer<LinkedEntityGroup>(root).Length); Assert.IsFalse(source.IsPinned);
            Assert.IsFalse(EM.IsComponentEnabled<GridSpawning>(root));
            var cancelled = Spawn(source); Tick(); Assert.IsTrue(source.IsPinned);
            EM.DestroyEntity(cancelled); Tick(); Assert.IsFalse(source.IsPinned);
            using (var ecb = new EntityCommandBuffer(Allocator.TempJob))
            {
                GridSpawnApi.RequestSpawn(ecb, registry, registry.Register(source), new GridSpawnParams { PersistentId = 2 });
                Assert.IsTrue(source.IsPinned); // An unplayed ECB still has one registry-owned read.
                registry.CancelSpawnReads(); Assert.IsFalse(source.IsPinned);
            }
            Spawn(source); Tick(); Assert.IsTrue(source.IsPinned);
            world.Dispose(); world = null; Assert.IsFalse(source.IsPinned);
        }
        [Test] public void UniformBrick_Expanded()
        {
            var source = Single(); var root = Spawn(source); Tick();
            var brick = EM.GetBuffer<BrickSlot>(root)[0].Brick;
            foreach (var cell in EM.GetBuffer<BrickCells>(brick)) Assert.AreEqual(source.Data.Bricks[0].Uniform, cell.Value);
            foreach (var word in EM.GetBuffer<BrickSolidMask>(brick)) Assert.AreEqual(ulong.MaxValue, word.Bits);
        }
        [Test] public void InvalidRequests_Destroyed()
        {
            var empty = Spawn(Own(new VoxelStructure()));
            var both = Spawn(Single()); var request = EM.GetComponentData<GridSpawnRequest>(both); request.Blueprint = new ContentHash(123); EM.SetComponentData(both, request);
            var unknown = GridSpawnApi.RequestSpawnBlueprint(EM, new ContentHash(987), new GridSpawnParams { Pose = RigidTransform.identity });
            LogAssert.Expect(LogType.Error, "[Voxel] spawn rejected: empty structure");
            LogAssert.Expect(LogType.Error, "[Voxel] spawn rejected: exactly one source is required");
            LogAssert.Expect(LogType.Error, "[Voxel] spawn rejected: source not found");
            Tick(); Assert.IsFalse(EM.Exists(empty)); Assert.IsFalse(EM.Exists(both)); Assert.IsFalse(EM.Exists(unknown));
            foreach (var source in sources) Assert.IsFalse(source.IsPinned);
        }
        [Test] public void FunctionalRegistration_Applied()
        {
            var registrations = EM.GetBuffer<FunctionalComponentRegistration>(SingletonEntity<FunctionalComponentRegistration>());
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.None, Type = typeof(Universal) });
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.Thruster, Type = typeof(ThrusterData) });
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.PowerSource, Type = typeof(SourceData) });
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.Explosive, Type = typeof(ExplosiveData) });
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.Explosive, Type = typeof(SourceData) });
            var source = WithFunctional(); Place(source, "block_test_reactor", new int3(35, 11, 5));
            var root = Spawn(source); Tick(); var instances = EM.GetBuffer<BlockInstance>(root);
            foreach (var instance in instances) Assert.IsTrue(EM.HasComponent<Universal>(instance.Functional));
            Assert.IsTrue(EM.HasComponent<ThrusterData>(instances[1].Functional)); Assert.IsTrue(EM.HasComponent<ThrusterData>(instances[2].Functional));
            Assert.IsTrue(EM.HasComponent<SourceData>(instances[3].Functional)); Assert.IsTrue(EM.HasComponent<ExplosiveData>(instances[3].Functional));
            Assert.AreEqual(3, Singleton<GridPrototypes>().FunctionalByMaskNoRender.Count);
        }
        [Test] public void ProfileRegistration_Applied()
        {
            var registrations = EM.GetBuffer<RootComponentRegistration>(SingletonEntity<RootComponentRegistration>());
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.NpcShip, Type = typeof(ProfileData) });
            var source = Single(); var npc = Spawn(source, GridProfile.NpcShip); EM.AddComponentData(npc, new ProfileData { Value = 71 });
            var plain = Spawn(source); Tick();
            Assert.AreEqual(71, EM.GetComponentData<ProfileData>(npc).Value); Assert.IsFalse(EM.HasComponent<ProfileData>(plain));
            Assert.AreNotEqual(0, EM.GetComponentData<GridSource>(npc).Flags & GridSpawnFlags.PlanarInertia);
        }
        [Test] public void Stow_Unstow_TickExact()
        {
            var root = Spawn(Single()); Tick(); EM.SetComponentEnabled<GridBodyInit>(root, false); EM.AddComponentData(root, new PhysicsVelocity());
            Entity child = EM.GetBuffer<BrickSlot>(root)[0].Brick;
            using (var ecb = new EntityCommandBuffer(Allocator.TempJob))
            {
                GridStowApi.Stow(ecb.AsParallelWriter(), 0, root); Assert.IsFalse(EM.HasComponent<Disabled>(root));
                ecb.Playback(EM); Assert.IsTrue(EM.HasComponent<Disabled>(root)); Assert.IsTrue(EM.HasComponent<Disabled>(child));
            }
            var pose = new RigidTransform(quaternion.RotateY(1), new float3(10, 0, 7));
            using (var ecb = new EntityCommandBuffer(Allocator.TempJob))
            {
                GridStowApi.Unstow(ecb.AsParallelWriter(), 0, root, pose, new float3(1,2,3), new float3(0,1,0), child, 77);
                Assert.IsTrue(EM.HasComponent<Disabled>(root)); ecb.Playback(EM);
            }
            Assert.IsFalse(EM.HasComponent<Disabled>(root)); Assert.IsFalse(EM.HasComponent<Disabled>(child));
            Assert.AreEqual(pose.pos, EM.GetComponentData<LocalTransform>(root).Position);
            Assert.AreEqual(new float3(1,2,3), EM.GetComponentData<PhysicsVelocity>(root).Linear);
            Assert.AreEqual(pose, EM.GetComponentData<PhysicsGraphicalInterpolationBuffer>(root).PreviousTransform);
            Assert.AreEqual(77, EM.GetComponentData<SplitGrace>(root).UntilTick);
        }
        [Test] public void Headless_NoRenderComponents()
        {
            var root = Spawn(WithFunctional()); Tick();
            foreach (var entry in EM.GetBuffer<LinkedEntityGroup>(root))
            {
                Assert.IsFalse(EM.HasComponent<MaterialMeshInfo>(entry.Value)); Assert.IsFalse(EM.HasComponent<RenderBounds>(entry.Value));
                Assert.IsFalse(EM.HasComponent<BrickRenderState>(entry.Value));
            }
        }
    }
}
