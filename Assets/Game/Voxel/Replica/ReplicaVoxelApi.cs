using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Voxel.Replica
{
    /// <summary>Co-op spike (ADR-0116 §7–8): voxel reads for replication (structures, brick reads, hashes) and the guest-side writes
    /// still waiting for ADR-0117 phase 2 (palettes, chunk bodies). Cells and instance health are no longer written here:
    /// guests queue exact writes that ExactCellApplySystem applies through the host's own CellWriter. Main thread.</summary>
    public static class ReplicaVoxelApi
    {
        /// <summary>A mutable structure carrying the host grid's exact palette and instance order, so later raw cell values mean the same.</summary>
        public static VoxelStructure CreateStructure(float cellSize, NativeArray<PaletteEntry> palette, NativeArray<BlockInstance> instances)
        {
            var structure = new VoxelStructure(cellSize);
            structure.CopyRuntimeMetadata(cellSize, palette, instances);
            return structure;
        }

        /// <summary>Appends or overwrites palette entries from <paramref name="start"/> (host palettes only grow, through Place restores).</summary>
        public static void SetPalette(EntityManager em, Entity root, int start, NativeArray<PaletteEntry> entries)
        {
            var palette = em.GetBuffer<PaletteEntry>(root);
            if (palette.Length < start + entries.Length) palette.Resize(start + entries.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < entries.Length; i++) palette[start + i] = entries[i];
        }

        /// <summary>Host: bricks whose cells left only as detached bodies (accumulate = true adds this tick to earlier ticks) (hulks and chunks), which guests receive as their own
        /// grids or chunk bodies and so must not also spray as debris. Call after the tick's structural sync, before the next tick.</summary>
        public static unsafe void CollectQuietBricks(EntityManager em, Dictionary<(Entity Grid, int3 Brick), bool> quiet, bool accumulate = false)
        {
            if (!accumulate) quiet.Clear();
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
            if (query.IsEmptyIgnoreFilter) return;
            var data = query.GetSingleton<DamageStreams>().Data;
            if (data == null) return;
            data->RemovedWriteHandle.Complete();   // only the resolver's write, not every job in flight
            var removed = data->Removed;
            for (int i = 0; i < removed.Length; i++)
            {
                var cell = removed[i];
                var key = (cell.Grid, BrickMath.BrickOf(cell.Cell));
                bool body = cell.Reason == CellRemovedReason.DetachedBody;
                quiet[key] = quiet.TryGetValue(key, out bool all) ? all && body : body;
            }
        }

        /// <summary>Reads one brick's 4096 cells (air for a missing brick). Host capture and hash checks use it.</summary>
        public static bool ReadBrick(EntityManager em, Entity brick, NativeArray<Cell> cells)
        {
            if (brick == Entity.Null || !em.Exists(brick) || !em.HasBuffer<BrickCells>(brick))
            { for (int i = 0; i < cells.Length; i++) cells[i] = default; return false; }
            var dense = em.GetBuffer<BrickCells>(brick, true);
            if (dense.Length != 0) { cells.CopyFrom(dense.Reinterpret<Cell>().AsNativeArray()); return true; }
            for (int i = 0; i < cells.Length; i++) cells[i] = default;
            foreach (var entry in em.GetBuffer<BrickSparseCells>(brick, true)) cells[entry.Local] = entry.Value;
            return true;
        }

        /// <summary>A co-op guest's replica of a host chunk body: the same components through <see cref="ChunkBodySetup"/>, kinematic
        /// (its motion comes from snapshots), with the collider built by the host's chunk box builder.</summary>
        public static Entity CreateChunk(EntityManager em, float cellSize, float3 localOrigin, NativeArray<ChunkCell> cells, LocalTransform pose, uint endTick, uint dissolveStartTick, uint tick)
        {
            using var configs = em.CreateEntityQuery(ComponentType.ReadOnly<DamageConfig>());
            int maxBoxes = configs.IsEmptyIgnoreFilter ? 16 : configs.GetSingleton<DamageConfig>().ChunkMaxBoxes;
            var blob = ReplicaJobs.ChunkCollider(cells, cellSize, localOrigin, maxBoxes);   // Burst; managed it cost a guest ~20 ms
            var entity = em.CreateEntity(ChunkBodySetup.Types());
            em.SetSharedComponent(entity, GridBodyFactory.WorldIndex());
            // Slot 255 marks a replica: the host-side pool logic never runs on guests, and presentation treats it as any chunk.
            var body = new ChunkBody { SpawnTick = tick, EndTick = endTick, DissolveStartTick = dissolveStartTick, CellSize = cellSize,
                LocalOrigin = localOrigin, CellCount = (ushort)cells.Length, Slot = 255, Live = 1 };
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ChunkBodySetup.Write(ref ecb, entity, pose, PhysicsMass.CreateKinematic(MassProperties.UnitSphere), default, blob, body, cells, Entity.Null, 0, 1);
            ecb.Playback(em); ecb.Dispose();
            return entity;
        }

        /// <summary>Removes a replica chunk. ChunkColliderBlob is a cleanup component: ChunkColliderCleanupSystem retires the collider
        /// once the entity is gone, exactly as for host chunks, so this must not free it too (a double free corrupts the heap).</summary>
        public static void DestroyChunk(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity) || !em.HasComponent<ChunkBody>(entity)) return;
            em.DestroyEntity(entity);
        }

        /// <summary>Order-independent hash of a grid's solid cells by brick coordinate, for host/guest comparison (SPEC-90 NET-007).</summary>
        public static ulong HashCells(EntityManager em, Entity root)
        {
            // Per brick: hash of its coordinate and solid cells in index order. Summed, so slot order (which differs when guest bounds
            // are tighter) does not matter. Burst, in parallel.
            var views = new NativeList<BrickView>(64, Allocator.TempJob);
            var sparse = new NativeList<Cell>(0, Allocator.TempJob);
            ReplicaJobs.ViewBricks(em, root, views, sparse);
            ulong total = ReplicaJobs.Hash(views.AsArray());
            views.Dispose(); sparse.Dispose();
            return total;
        }
    }
}
