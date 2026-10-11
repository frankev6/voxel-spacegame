using Game.Data.Blocks;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.11: removes exact per-cell contributions in resolver order, excluding dissolution.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct GridMassRemoveJob : IJob
    {
        [ReadOnly] public NativeArray<CellRemoved> Cells;
        [ReadOnly] public NativeArray<GridRemovedRange> Ranges;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        public ComponentLookup<GridMass> Masses;
        public NativeList<Entity> Changed;
        public void Execute()
        {
            for (int i = 0; i < Ranges.Length; i++)
            {
                var range = Ranges[i]; if (!Masses.HasComponent(range.Grid) || !Grids.HasComponent(range.Grid)) continue;
                var mass = Masses[range.Grid]; float cellSize = Grids[range.Grid].CellSize; bool changed = false;
                for (int c = range.Start; c < range.Start + range.Count; c++)
                {
                    var cell = Cells[c];
                    if (cell.Reason != CellRemovedReason.Destroyed && cell.Reason != CellRemovedReason.DetachedParticle && cell.Reason != CellRemovedReason.DetachedBody) continue;
                    float kg = cell.Block < Blocks.Value.Blocks.Length ? Blocks.Value.Blocks[cell.Block].CellMass : 0;
                    MassAccumulator.RemoveCell(ref mass.Data, cell.Cell, cellSize, kg); changed = true;
                }
                if (!changed) continue;
                mass.Version++; Masses[range.Grid] = mass; Changed.Add(range.Grid);
            }
        }
    }
}
