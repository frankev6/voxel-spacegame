using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>ADR-0117: applies queued exact writes grid by grid through <see cref="CellWriter"/> and publishes them like the resolver's
    /// flatten does (removed cells and ranges, touched bricks with their dirty bits, the destroy list, grid results), so every later system
    /// treats them as this tick's changes.</summary>
    [BurstCompile] internal unsafe struct ExactCellApplyJob : IJob
    {
        public DamageAccess Access;
        public DamageConfig Config;
        public uint Tick;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        public NativeList<ExactCellWrite> Writes;
        public NativeList<CellRemoved> Cells;
        public NativeList<GridRemovedRange> Ranges;
        public NativeList<TouchedBrick> Bricks;
        public NativeList<Entity> Destroy;
        public NativeList<DamageGridResult> Grids;
        public NativeList<CellRestored> Restored;
        public NativeList<GridRemovedRange> RestoredRanges;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;

        public void Execute()
        {
            for (int start = 0; start < Writes.Length;)
            {
                Entity root = Writes[start].Grid; int end = start + 1;
                while (end < Writes.Length && Writes[end].Grid == root) end++;
                if (Access.Grids.HasComponent(root) && Access.Slots.HasBuffer(root) && Access.Transforms.HasComponent(root)) Apply(root, start, end);
                else Data->ExactMissing += end - start;
                start = end;
            }
            Writes.Clear();
        }

        void Apply(Entity root, int start, int end)
        {
            var writer = new CellWriter
            {
                RandomIdentity = Access.Ids.HasComponent(root) && Access.Ids[root].Value != 0 ? Access.Ids[root].Value : (ulong)(uint)root.Index,
                Access = Access.Cells(root, Blocks), Config = Config, Tick = Tick, Transform = Access.Transforms[root],
                Velocity = Access.Velocities.HasComponent(root) ? Access.Velocities[root] : default,
                Mass = Access.Masses.HasComponent(root) ? Access.Masses[root] : new PhysicsMass { Transform = RigidTransform.identity },
                Stats = Access.Stats.HasComponent(root) ? Access.Stats[root] : default, Modules = Modules, Orientations = Orientations
            };
            writer.Allocate(Allocator.Temp);
            // An exact write has no hit behind it: debris kicks outward from the centre of the cells removed here, at blast speeds.
            float3 sum = 0; int removed = 0;
            for (int i = start; i < end; i++) if (Writes[i].Kind == ExactWriteKind.Cell && Writes[i].Value.IsAir) { sum += Writes[i].Cell; removed++; }
            float cellSize = writer.Access.Definition.CellSize;
            writer.Event = new DamageEvent { Grid = root, LocalPoint = removed > 0 ? (sum / removed + .5f) * cellSize : 0, Radius = 16 * cellSize };
            writer.Row = Config.Table.IsCreated && Config.Table.Value.Types.Length > 0 ? Config.Table.Value.Types[0] : default;
            int restoredStart = Restored.Length;
            for (int i = start; i < end; i++)
            {
                var write = Writes[i];
                if (write.Kind == ExactWriteKind.Instance) { writer.ApplyExactInstance(write.Instance, write.FunctionHp, write.CellsAlive, write.Flags); continue; }
                if (writer.ApplyExactCell(write.Cell, write.Value, write.Reason, out var placed, out bool missing) && placed.Grid != Entity.Null) Restored.Add(placed);
                if (missing) Data->ExactMissing++;
            }
            if (writer.Changed) { var grid = writer.Access.Definition; grid.Revision++; Access.Grids[root] = grid; }
            if (Access.Stats.HasComponent(root)) Access.Stats[root] = writer.Stats;
            if (writer.Removed.Length > 0) Ranges.Add(new GridRemovedRange { Grid = root, Start = Cells.Length, Count = writer.Removed.Length });
            int destroyedCells = 0, detachedCells = 0;
            foreach (var cell in writer.Removed)
            {
                Cells.Add(cell);
                if (cell.Reason == CellRemovedReason.Destroyed) destroyedCells++; else detachedCells++;
            }
            if (Restored.Length > restoredStart) RestoredRanges.Add(new GridRemovedRange { Grid = root, Start = restoredStart, Count = Restored.Length - restoredStart });
            var result = new DamageGridResult { Grid = root, TouchedBrickStart = Bricks.Length, TouchedBrickCount = writer.Touched.Length };
            foreach (var brick in writer.Touched) { Bricks.Add(brick); Access.Dirty.SetComponentEnabled(brick.Brick, true); }
            foreach (var entity in writer.DestroyList) Destroy.Add(entity);
            Grids.Add(result);
            Data->Current.GridsResolved++; Data->Current.CellsDestroyed += destroyedCells; Data->Current.CellsDetached += detachedCells;
            Data->Current.CellsPlaced += Restored.Length - restoredStart;
            writer.Dispose();
        }
    }
}
