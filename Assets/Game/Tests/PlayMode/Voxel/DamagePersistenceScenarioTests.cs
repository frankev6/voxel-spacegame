using Game.App.Boot;
using Game.Core;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.PlayMode.Voxel
{
    public struct PersistenceDamageRequest : IComponentData { public Entity Root; public int Changed; }

    /// <summary>One fixed-step producer stands in for future combat; it uses the same sole cell-write helper.</summary>
    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    public partial struct PersistenceDamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PersistenceDamageRequest>();
        public void OnUpdate(ref SystemState state)
        {
            ref var request = ref SystemAPI.GetSingletonRW<PersistenceDamageRequest>().ValueRW;
            if (request.Changed != 0) return;
            var em = state.EntityManager; var grid = em.GetComponentData<VoxelGrid>(request.Root); grid.Revision++;
            var slots = em.GetBuffer<BrickSlot>(request.Root);
            for (int x = 0; x < 50; x++)
            {
                int3 cell = new int3(x, 0, 0), coord = BrickMath.BrickOf(cell);
                Entity entity = slots[BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims)].Brick;
                int local = BrickMath.LocalIndex(BrickMath.LocalOf(cell));
                var dense = em.GetBuffer<BrickCells>(entity); var sparse = em.GetBuffer<BrickSparseCells>(entity);
                Cell value = BrickWrite.Get(dense, sparse, local);
                if (x % 5 == 0) value = default;
                else { value.Hp = (byte)(x + 20); value.Flags = 3; }
                var brick = em.GetComponentData<Brick>(entity); var dirty = em.GetComponentData<BrickDirty>(entity);
                if (BrickWrite.Set(ref brick, dense, sparse, em.GetBuffer<BrickSolidMask>(entity), ref dirty, local, value, grid.Revision)) request.Changed++;
                em.SetComponentData(entity, brick); em.SetComponentData(entity, dirty); em.SetComponentEnabled<BrickDirty>(entity, true);
            }
            em.SetComponentData(request.Root, grid);
        }
    }

    /// <summary>VOX-019 / regression 04-B3: fixed-step damage persists across fleet despawn and an explicit save boundary.</summary>
    [Category("Voxel"), Category("Scenario")]
    public sealed class DamagePersistenceScenarioTests
    {
        // A test-local logical cell digest keeps this scenario independent of DMG-014's later world determinism hasher.
        static unsafe ulong Hash(EntityManager em, Entity root, VoxelStructure source = null)
        {
            var slots = em.GetBuffer<BrickSlot>(root);
            var cells = new NativeArray<Cell>(4096, Allocator.Temp); ulong hash = StableHash.EmptyHash;
            try
            {
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    Entity entity = slots[slot].Brick;
                    if (entity == Entity.Null) continue;
                    var brick = em.GetComponentData<Brick>(entity);
                    var dense = em.GetBuffer<BrickCells>(entity); var sparse = em.GetBuffer<BrickSparseCells>(entity);
                    for (int local = 0; local < cells.Length; local++)
                        cells[local] = source == null ? BrickWrite.Get(dense, sparse, local) : source.GetCell(BrickMath.CellOf(brick.Coord, local));
                    hash = StableHash.Combine(hash, StableHash.Combine((ulong)slot, StableHash.Hash64((byte*)cells.GetUnsafeReadOnlyPtr(), cells.Length * 4)));
                }
                return hash;
            }
            finally { cells.Dispose(); }
        }
        static void Exercise(bool save)
        {
            using var content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            var voxelContent = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            using var library = new BlueprintLibrary(content.Catalog, voxelContent);
            ushort block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            using var source = BenchmarkStructures.Ship20k(block); // Fleet owns this mutable structure across both spawns.
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                using var test = EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(PersistenceDamageSystem));
                var em = test.EntityManager; content.CreateSingletons(em);
                em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024 }); test.AddGameplayActive(); test.AddSimClock();
                int handle = registry.Register(source);
                var args = new GridSpawnParams { PersistentId = 787, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender | GridSpawnFlags.WriteBack };
                Entity root = GridSpawnApi.RequestSpawn(em, registry, handle, args); test.StepTicks(1);
                Assert.IsFalse(em.IsComponentEnabled<GridSpawning>(root)); ulong initial = Hash(em, root);
                Assert.AreEqual(initial, Hash(em, root, source));
                Entity request = em.CreateSingleton(new PersistenceDamageRequest { Root = root });
                uint before = em.GetComponentData<VoxelGrid>(root).Revision;
                test.StepTicks(1);
                Assert.AreEqual(50, em.GetComponentData<PersistenceDamageRequest>(request).Changed);
                Assert.AreEqual(before + 1, em.GetComponentData<VoxelGrid>(root).Revision, "one revision per damage tick");
                ulong damaged = Hash(em, root); Assert.AreNotEqual(initial, damaged);
                Assert.AreEqual(initial, Hash(em, root, source), "fleet source remains unchanged until its write-back boundary");
                if (save)
                {
                    Assert.AreEqual(4, GridStructureSync.WriteBack(em, root, registry));
                    Assert.AreEqual(damaged, Hash(em, root, source));
                    Assert.AreEqual(0, GridStructureSync.WriteBack(em, root, registry));
                    // Despawn now cannot mask a missing save-time copy by writing damage a second time.
                    var origin = em.GetComponentData<GridSource>(root); origin.Flags &= ~GridSpawnFlags.WriteBack; em.SetComponentData(root, origin);
                }
                GridSpawnApi.RequestDespawn(em, root); test.StepTicks(1); Assert.IsFalse(em.Exists(root));
                Entity respawn = GridSpawnApi.RequestSpawn(em, registry, handle, args); test.StepTicks(1);
                Assert.IsFalse(em.IsComponentEnabled<GridSpawning>(respawn)); Assert.AreEqual(damaged, Hash(em, respawn));
                Assert.AreEqual(damaged, Hash(em, respawn, source)); Assert.AreEqual(19990, source.SolidCellCount);
                Assert.IsFalse(source.IsPinned);
                registry.Release(handle);
            }
            finally { Object.DestroyImmediate(registry); }
        }
        static void Run(bool save)
        {
            var previous = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try { Exercise(save); UnsafeUtility.CheckForLeaks(); Exercise(save); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); }
            finally { NativeLeakDetection.Mode = previous; }
        }
        [Test] public void DespawnRespawn_KeepsDamage_04B3() => Run(false);
        [Test] public void SaveWriteBack_KeepsDamage() => Run(true);
    }
}
