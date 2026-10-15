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
    /// still waiting for ADR-0117 phase 2 (palettes, chunk bodies, the hulk blast). Cells and instance health are no longer written here:
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

        /// <summary>Guest: the core-breach blast and sound a ship makes when the host turns it into a hulk (ShipDeathSystem's presentation).</summary>
        public static void EmitHulkBlast(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<VoxelGrid>(root)) return;
            using var fxQuery = em.CreateEntityQuery(ComponentType.ReadWrite<FxEventQueue>());
            if (fxQuery.IsEmptyIgnoreFilter) return;
            em.CompleteAllTrackedJobs();
            var grid = em.GetComponentData<VoxelGrid>(root); var pose = em.GetComponentData<LocalTransform>(root);
            float3 centre = pose.TransformPoint(((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
            float3 velocity = em.HasComponent<PhysicsVelocity>(root) ? em.GetComponentData<PhysicsVelocity>(root).Linear : float3.zero;
            fxQuery.GetSingleton<FxEventQueue>().Queue.Enqueue(FxEvents.Explosion(centre, 6, velocity));
            using var configs = em.CreateEntityQuery(ComponentType.ReadOnly<DamageConfig>());
            using var sfxQuery = em.CreateEntityQuery(ComponentType.ReadWrite<SfxEventQueue>());
            if (configs.IsEmptyIgnoreFilter || sfxQuery.IsEmptyIgnoreFilter) return;
            var config = configs.GetSingleton<DamageConfig>();
            if (config.CueCoreLost.Value != 0) sfxQuery.GetSingleton<SfxEventQueue>().Queue.Enqueue(new SfxEvent { Cue = config.CueCoreLost, Pos = centre, Volume = 1 });
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

        /// <summary>Creates a kinematic chunk body on a guest from the host's cells and pose. Its motion comes from snapshots.</summary>
        public static Entity CreateChunk(EntityManager em, float cellSize, float3 localOrigin, NativeArray<ChunkCell> cells, LocalTransform pose, uint endTick, uint dissolveStartTick, uint tick)
        {
            using var configs = em.CreateEntityQuery(ComponentType.ReadOnly<DamageConfig>());
            int maxBoxes = configs.IsEmptyIgnoreFilter ? 16 : configs.GetSingleton<DamageConfig>().ChunkMaxBoxes;
            var blob = ReplicaJobs.ChunkCollider(cells, cellSize, localOrigin, maxBoxes);   // Burst; managed it cost a guest ~20 ms
            var entity = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsVelocity),
                typeof(PhysicsMass), typeof(PhysicsCollider), typeof(PhysicsWorldIndex), typeof(PhysicsGraphicalSmoothing),
                typeof(PhysicsGraphicalInterpolationBuffer), typeof(ChunkBody), typeof(ChunkContactState), typeof(ChunkCell), typeof(ChunkColliderBlob),
                typeof(ChunkDissolve));
            em.SetSharedComponent(entity, GridBodyFactory.WorldIndex());
            em.SetComponentData(entity, pose);
            em.SetComponentData(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            em.SetComponentData(entity, new PhysicsCollider { Value = blob }); em.SetComponentData(entity, new ChunkColliderBlob { Value = blob });
            em.SetComponentData(entity, new PhysicsGraphicalSmoothing { ApplySmoothing = 1 });
            em.SetComponentData(entity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(pose.Rotation, pose.Position) });
            // Slot 255 marks a replica: the host-side pool logic never runs on guests, and presentation treats it as any chunk.
            em.SetComponentData(entity, new ChunkBody { SpawnTick = tick, EndTick = endTick, DissolveStartTick = dissolveStartTick, CellSize = cellSize,
                LocalOrigin = localOrigin, CellCount = (ushort)cells.Length, Slot = 255, Live = 1 });
            em.GetBuffer<ChunkCell>(entity).CopyFrom(cells);
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
