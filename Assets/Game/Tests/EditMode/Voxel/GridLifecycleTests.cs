using System.Collections.Generic;
using System.Diagnostics;
using Game.Tests.Common.Perf;
using Unity.PerformanceTesting;
using Game.Core.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-014 lifecycle and save-state regressions through real source and ECS storage.</summary>
    public sealed class GridLifecycleTests
    {
        struct OwnerValue : IComponentData { public int Value; }
        VoxelTestContent content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        World world;
        SystemHandle system;
        readonly List<VoxelStructure> sources = new List<VoxelStructure>();
        EntityManager EM => world.EntityManager;
        [SetUp] public void SetUp()
        {
            content = new VoxelTestContent(); library = new BlueprintLibrary(content.Service.Catalog, content.Content);
            registry = VoxelStructureRegistry.CreateRuntime(library); world = new World("GridLifecycleTests");
            content.Service.CreateSingletons(EM); EM.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
            EM.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024 }); EM.CreateEntity(typeof(GameplayActive));
            system = world.CreateSystem<GridSpawnSystem>(); Tick();
        }
        [TearDown] public void TearDown()
        {
            world.Dispose(); Object.DestroyImmediate(registry);
            foreach (var source in sources) source.Dispose(); sources.Clear();
            library.Dispose(); content.Dispose();
        }
        void Tick() => system.Update(world.Unmanaged);
        VoxelStructure Own(VoxelStructure source) { sources.Add(source); return source; }
        VoxelStructure Ship(bool functional = false)
        {
            var source = Own(BenchmarkStructures.Ship20k(content.Index("block_hull")));
            if (functional) Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
            { Block = content.Index("block_core_cross"), Origin = new int3(5, 11, 5) }, content.Content));
            return source;
        }
        Entity Spawn(VoxelStructure source, out int handle)
        {
            handle = registry.Register(source);
            var root = GridSpawnApi.RequestSpawn(EM, registry, handle, new GridSpawnParams
            { PersistentId = 87, Pose = new RigidTransform(quaternion.RotateY(.7f), new float3(3,0,9)), Flags = GridSpawnFlags.NoRender | GridSpawnFlags.WriteBack });
            Tick(); return root;
        }
        Entity BrickAt(Entity root, int3 coord)
        {
            var grid = EM.GetComponentData<VoxelGrid>(root);
            return EM.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims)].Brick;
        }
        void Damage(Entity root, int3 coord, int index = 0)
        {
            Entity entity = BrickAt(root, coord); var brick = EM.GetComponentData<Brick>(entity); var dirty = EM.GetComponentData<BrickDirty>(entity);
            var grid = EM.GetComponentData<VoxelGrid>(root); grid.Revision++;
            var cells = EM.GetBuffer<BrickCells>(entity); var value = cells[index].Value; value.Hp = 51; value.Flags = 3;
            Assert.IsTrue(BrickWrite.Set(ref brick, cells, EM.GetBuffer<BrickSolidMask>(entity), ref dirty, index, value, grid.Revision));
            EM.SetComponentData(root, grid); EM.SetComponentData(entity, brick); EM.SetComponentData(entity, dirty); EM.SetComponentEnabled<BrickDirty>(entity, true);
        }
        void DestroyBrick(Entity root, int3 coord)
        {
            var grid = EM.GetComponentData<VoxelGrid>(root); var slots = EM.GetBuffer<BrickSlot>(root);
            int index = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims); Entity entity = slots[index].Brick;
            slots[index] = default; grid.Revision++; EM.SetComponentData(root, grid); EM.DestroyEntity(entity);
        }
        [Test, Performance, Category("Performance")] public void WriteBack_CopiesOnlyChanged()
        {
            var source = Ship(true); var root = Spawn(source, out _);
            Damage(root, new int3(0)); Damage(root, new int3(1,0,0)); Damage(root, new int3(2,0,0));
            var instances = EM.GetBuffer<BlockInstance>(root); var instance = instances[0];
            instance.FunctionHp = 321; instance.Flags = (BlockInstanceFlags)1; instances[0] = instance;
            source.GetMass(content.Content);
            Assert.AreEqual(3, GridStructureSync.WriteBack(EM, root, registry));
            Assert.AreEqual(0, GridStructureSync.WriteBack(EM, root, registry));
            Assert.AreEqual(321, source.Data.Instances[0].FunctionHp); Assert.AreEqual(Entity.Null, source.Data.Instances[0].Functional);
            Assert.AreEqual(0, source.Data.Header.Value.MassRevision);
            Assert.AreEqual(51, source.GetCell(new int3(0)).Hp); Assert.AreEqual(3, source.GetCell(new int3(32,0,0)).Flags);
            Assert.AreEqual(EM.GetComponentData<VoxelGrid>(root).Revision, EM.GetComponentData<GridSource>(root).SyncedRevision);

            var station = Own(BenchmarkStructures.HollowStation1M(content.Index("block_hull")));
            var stationRoot = Spawn(station, out _); Assert.IsFalse(station.IsPinned);
            for (int i = 0; i < 5; i++) Assert.AreEqual(0, GridStructureSync.WriteBack(EM, stationRoot, registry));
            var samples = new double[20];
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); int copied = GridStructureSync.WriteBack(EM, stationRoot, registry);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency; Assert.AreEqual(0, copied);
            }
            var stat = PerfRun.Gate("WriteBackUnchanged1M", samples, .2);
            TestContext.WriteLine($"Unchanged 1M write-back: median {stat.Median:F5} ms, p95 {stat.P95:F5} ms");
        }
        [Test] public void DespawnRespawn_KeepsDamage()
        {
            var source = Ship(); var root = Spawn(source, out int handle); Damage(root, new int3(1,0,0));
            GridSpawnApi.RequestDespawn(EM, root); Tick(); Assert.IsFalse(EM.Exists(root));
            var next = GridSpawnApi.RequestSpawn(EM, registry, handle, new GridSpawnParams
            { PersistentId = 87, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender | GridSpawnFlags.WriteBack });
            Tick(); Assert.AreEqual(51, EM.GetBuffer<BrickCells>(BrickAt(next, new int3(1,0,0)))[0].Value.Hp);

            var lattice = Own(BenchmarkStructures.Lattice2500(content.Index("block_hull")));
            var partial = Spawn(lattice, out _); Assert.IsTrue(lattice.IsPinned);
            Damage(partial, new int3(0)); DestroyBrick(partial, new int3(1,0,0));
            Assert.AreEqual(0, GridStructureSync.WriteBack(EM, partial, registry)); Assert.IsTrue(lattice.IsPinned);
            GridSpawnApi.RequestDespawn(EM, partial); Tick(); Assert.IsFalse(lattice.IsPinned);
            Assert.AreEqual(2499, lattice.BrickCount, "uncreated source bricks survive partial despawn");
            Assert.AreEqual(51, lattice.GetCell(new int3(0)).Hp); Assert.IsTrue(lattice.GetCell(new int3(16,0,0)).IsAir);
            Assert.IsFalse(lattice.GetCell(new int3(384,48,384)).IsAir);
        }
        [Test] public void WriteBack_RemovesDestroyedBricks()
        {
            var source = Ship(); var root = Spawn(source, out _); DestroyBrick(root, new int3(1,0,0));
            Assert.AreEqual(0, GridStructureSync.WriteBack(EM, root, registry));
            Assert.AreEqual(9, source.BrickCount); Assert.IsFalse(source.Data.BrickIndex.ContainsKey(new int3(1,0,0)));
            Assert.AreEqual(0, GridStructureSync.WriteBack(EM, root, registry));
        }
        [Test] public void Rebuild_KeepsRoot()
        {
            var first = Ship(true); var root = Spawn(first, out _); EM.AddComponentData(root, new OwnerValue { Value = 901 });
            var oldGrid = EM.GetComponentData<VoxelGrid>(root); var pose = EM.GetComponentData<LocalTransform>(root);
            var oldChildren = new List<Entity>(); foreach (var entry in EM.GetBuffer<LinkedEntityGroup>(root)) if (entry.Value != root) oldChildren.Add(entry.Value);
            var replacement = Ship(true); replacement.SetCell(new int3(40,0,0), default);
            GridSpawnApi.RequestRebuild(EM, registry, root, registry.Register(replacement), GridSpawnFlags.NoRender | GridSpawnFlags.WriteBack);
            Assert.IsTrue(replacement.IsPinned); Tick();
            Assert.IsTrue(EM.Exists(root)); Assert.AreEqual(901, EM.GetComponentData<OwnerValue>(root).Value);
            Assert.AreEqual(pose, EM.GetComponentData<LocalTransform>(root)); Assert.AreEqual(87, EM.GetComponentData<PersistentId>(root).Value);
            Assert.AreEqual(oldGrid.Serial, EM.GetComponentData<VoxelGrid>(root).Serial);
            Assert.AreEqual(oldGrid.Revision + 1, EM.GetComponentData<VoxelGrid>(root).Revision);
            Assert.IsTrue(EM.IsComponentEnabled<GridRebuilt>(root)); Assert.IsFalse(replacement.IsPinned);
            foreach (var entity in oldChildren) Assert.IsFalse(EM.Exists(entity));
            Assert.IsTrue(EM.GetBuffer<BrickCells>(BrickAt(root, new int3(2,0,0)))[8].Value.IsAir);
            Assert.AreEqual(0, GridStructureSync.WriteBack(EM, root, registry));
        }
        [Test] public void Despawn_DestroysAll()
        {
            int baseline = EM.UniversalQuery.CalculateEntityCount();
            var root = Spawn(Ship(true), out _);
            // Functional prototypes are reusable world resources, not members of a grid.
            using var prefabQuery = EM.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Prefab>() }, Options = EntityQueryOptions.IncludePrefab });
            int functionalPrototypes = prefabQuery.CalculateEntityCount() - 1;
            var group = EM.GetBuffer<LinkedEntityGroup>(root).ToNativeArray(Allocator.Temp);
            using (group)
            {
                EM.SetEnabled(root, false); GridSpawnApi.RequestDespawn(EM, root); Tick();
                foreach (var entry in group) Assert.IsFalse(EM.Exists(entry.Value));
            }
            Assert.AreEqual(baseline + functionalPrototypes, EM.UniversalQuery.CalculateEntityCount());
        }
        [Test] public void CaptureChanges_SinceSpawn()
        {
            var source = Own(new VoxelStructure()); ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = content.Index("block_hull") });
            source.SetBrickUniform(new int3(0), Cell.Palette(palette)); source.SetBrickUniform(new int3(2,0,0), Cell.Palette(palette));
            var root = Spawn(source, out _); Damage(root, new int3(0)); DestroyBrick(root, new int3(2,0,0));
            Assert.AreEqual(1, GridStructureSync.WriteBack(EM, root, registry));
            using var changed = new VoxelStructure(); using var removed = new NativeList<int3>(Allocator.Temp);
            Assert.AreEqual(1, GridStructureSync.CaptureChanges(EM, root, changed, removed));
            Assert.AreEqual(1, changed.BrickCount); Assert.AreEqual(51, changed.GetCell(new int3(0)).Hp);
            Assert.AreEqual(1, removed.Length); Assert.AreEqual(new int3(2,0,0), removed[0], "the never-occupied slot at x=1 is not a removal");
            Assert.AreEqual(source.Data.Palette.Length, changed.Data.Palette.Length);
            Assert.AreEqual(1, GridStructureSync.CaptureChanges(EM, root, changed, removed)); Assert.AreEqual(1, removed.Length, "capture replaces outputs");
        }
    }
}
