using System;
using System.Collections.Generic;
using Game.Core;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{
    /// <summary>Incremental managed save boundary; main thread outside system updates or inside GridSpawnSystem (SPEC-10 §4.6).</summary>
    public static class GridStructureSync
    {
        public static int WriteBack(EntityManager em, Entity root, VoxelStructureRegistry registry)
            => WriteBackCore(em, root, registry, false);

        internal static int WriteBackCore(EntityManager em, Entity root, VoxelStructureRegistry registry, bool finalizingSpawn)
        {
            if (!em.Exists(root) || !em.HasComponent<GridSource>(root)) return 0;
            var origin = em.GetComponentData<GridSource>(root);
            if ((origin.Flags & GridSpawnFlags.WriteBack) == 0) return 0;
            var source = registry.Get(origin.StructureHandle);
            if (source == null || !source.Data.IsCreated)
            { GameLog.Warn("Voxel", "write-back skipped: structure handle was released"); return 0; }
            bool partial = em.IsComponentEnabled<GridSpawning>(root);
            if (partial && !finalizingSpawn) return 0; // Snapshot is already complete in the source; do not mutate an active reader.
            var grid = em.GetComponentData<VoxelGrid>(root);
            if (grid.Revision == origin.SyncedRevision) return 0;
            if (source.IsPinned) throw new InvalidOperationException("Write-back source is still pinned by another reader");
            em.CompleteAllTrackedJobs();
            var slots = em.GetBuffer<BrickSlot>(root);
            int copied = CopyChanged(em, root, source, origin.SyncedRevision);
            if (partial)
            {
                var baseline = em.GetBuffer<GridSpawnBrick>(root);
                int created = math.min(em.GetComponentData<GridSpawning>(root).NextBrick, baseline.Length);
                for (int i = 0; i < created; i++)
                    if (!Live(em, slots, grid, baseline[i].Coord)) source.RemoveBrick(baseline[i].Coord);
            }
            else
            {
                // RemoveAt swaps the final record, so scan backwards while changing the source table.
                for (int i = source.Data.Bricks.Length - 1; i >= 0; i--)
                {
                    int3 coord = source.Data.Bricks[i].Coord;
                    if (!Live(em, slots, grid, coord)) source.RemoveBrick(coord);
                }
            }
            source.CopyRuntimeMetadata(grid.CellSize, em.GetBuffer<PaletteEntry>(root).AsNativeArray(), em.GetBuffer<BlockInstance>(root).AsNativeArray());
            origin.SyncedRevision = grid.Revision; em.SetComponentData(root, origin);
            return copied;
        }

        public static int CaptureChanges(EntityManager em, Entity root, VoxelStructure changedBricks, NativeList<int3> removedBricks)
        {
            if (changedBricks == null) throw new ArgumentNullException(nameof(changedBricks));
            em.CompleteAllTrackedJobs();
            var grid = em.GetComponentData<VoxelGrid>(root);
            changedBricks.Clear(); removedBricks.Clear();
            changedBricks.CopyRuntimeMetadata(grid.CellSize, em.GetBuffer<PaletteEntry>(root).AsNativeArray(), em.GetBuffer<BlockInstance>(root).AsNativeArray());
            int count = CopyChanged(em, root, changedBricks, 0);
            var slots = em.GetBuffer<BrickSlot>(root); var baseline = em.GetBuffer<GridSpawnBrick>(root);
            int created = em.IsComponentEnabled<GridSpawning>(root)
                ? math.min(em.GetComponentData<GridSpawning>(root).NextBrick, baseline.Length) : baseline.Length;
            for (int i = 0; i < created; i++) if (!Live(em, slots, grid, baseline[i].Coord)) removedBricks.Add(baseline[i].Coord);
            removedBricks.AsArray().Sort(new CoordOrder());
            return count;
        }

        static int CopyChanged(EntityManager em, Entity root, VoxelStructure target, uint since)
        {
            int copied = 0;
            foreach (var slot in em.GetBuffer<BrickSlot>(root))
            {
                Entity entity = slot.Brick;
                if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<Brick>(entity)) continue;
                var brick = em.GetComponentData<Brick>(entity);
                if (brick.ChangedRevision <= since) continue;
                target.SetBrickDense(brick.Coord, em.GetBuffer<BrickCells>(entity).Reinterpret<Cell>().AsNativeArray());
                target.OptimizeBrick(brick.Coord); copied++;
            }
            return copied;
        }
        static bool Live(EntityManager em, DynamicBuffer<BrickSlot> slots, VoxelGrid grid, int3 coord)
        {
            int index = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims);
            if (index < 0 || index >= slots.Length) return false;
            Entity entity = slots[index].Brick;
            return entity != Entity.Null && em.Exists(entity) && em.HasComponent<Brick>(entity);
        }
        public static void WriteBackAll(EntityManager em, EntityQuery roots, VoxelStructureRegistry registry)
        {
            em.CompleteAllTrackedJobs();
            using var entities = roots.ToEntityArray(Allocator.Temp);
            // Query callers include disabled entities so stowed fleet members persist too.
            var ordered = new List<(uint Serial, Entity Entity)>(entities.Length);
            foreach (var entity in entities) ordered.Add((em.GetComponentData<VoxelGrid>(entity).Serial, entity));
            ordered.Sort((a, b) => a.Serial.CompareTo(b.Serial));
            foreach (var entry in ordered) WriteBack(em, entry.Entity, registry);
        }
        struct CoordOrder : IComparer<int3>
        {
            public int Compare(int3 a, int3 b)
            { int z = a.z.CompareTo(b.z); if (z != 0) return z; int y = a.y.CompareTo(b.y); return y != 0 ? y : a.x.CompareTo(b.x); }
        }
    }
}
