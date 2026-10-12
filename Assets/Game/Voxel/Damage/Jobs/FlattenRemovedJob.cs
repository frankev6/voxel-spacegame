using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.3: canonical range-order publication; enable bits and totals have one serial writer.</summary>
    [BurstCompile] internal unsafe struct FlattenRemovedJob : IJob
    {
        [ReadOnly] public NativeList<ResolvedGrid> Results;
        [ReadOnly] public NativeStream.Reader Removed, Touched, DestroyList, Explosions;
        public NativeStream.Writer PublishedExplosions;
        public NativeList<CellRemoved> Cells;
        public NativeList<GridRemovedRange> Ranges;
        public NativeList<TouchedBrick> Bricks;
        public NativeList<Entity> Destroy;
        public NativeList<DamageGridResult> Grids;
        public NativeList<CellRestored> Restored;
        public NativeList<GridRemovedRange> RestoredRanges;
        public ComponentLookup<BrickDirty> Dirty;
        public NativeReference<byte> Warnings;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
        public void Execute()
        {
            Cells.Clear(); Ranges.Clear(); Bricks.Clear(); Destroy.Clear(); Grids.Clear(); Restored.Clear(); RestoredRanges.Clear();
            PublishedExplosions.BeginForEachIndex(0);
            for (int i = 0; i < Results.Length; i++)
            {
                var result = Results[i]; var grid = result.Result;
                int count = Removed.BeginForEachIndex(i);
                if (count > 0) Ranges.Add(new GridRemovedRange { Grid = grid.Grid, Start = Cells.Length, Count = count });
                for (int n = 0; n < count; n++) Cells.Add(Removed.Read<CellRemoved>()); Removed.EndForEachIndex();
                grid.TouchedBrickStart = Bricks.Length; count = Touched.BeginForEachIndex(i); grid.TouchedBrickCount = count;
                for (int n = 0; n < count; n++) { var brick = Touched.Read<TouchedBrick>(); Bricks.Add(brick); Dirty.SetComponentEnabled(brick.Brick, true); } Touched.EndForEachIndex();
                count = DestroyList.BeginForEachIndex(i); for (int n = 0; n < count; n++) Destroy.Add(DestroyList.Read<Entity>()); DestroyList.EndForEachIndex();
                count = Explosions.BeginForEachIndex(i); for (int n = 0; n < count; n++) PublishedExplosions.Write(Explosions.Read<ExplosionRequest>()); Explosions.EndForEachIndex();
                Grids.Add(grid); Data->Current.GridsResolved++;
                Data->Current.CellsDestroyed += result.Destroyed; Data->Current.CellsDetached += result.Detached; Data->Current.InstancesDestroyed += result.Instances;
                if (result.RadiusClamped != 0 && (Warnings.Value & 1) == 0) { Warnings.Value |= 1; DamageWarnings.RadiusClamped(); }
            }
            PublishedExplosions.EndForEachIndex();
        }
    }
}
