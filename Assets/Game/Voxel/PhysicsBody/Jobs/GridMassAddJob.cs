using Game.Data.Blocks;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §5.11 step 1b: adds exact restored-cell contributions in resolver range order.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct GridMassAddJob : IJob
    {
        [ReadOnly] public NativeArray<CellRestored> Cells;
        [ReadOnly] public NativeArray<GridRemovedRange> Ranges;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        public ComponentLookup<GridMass> Masses;
        public NativeList<Entity> Changed;

        public void Execute()
        {
            for (int i = 0; i < Ranges.Length; i++)
            {
                var range = Ranges[i];
                if (range.Count == 0 || !Masses.HasComponent(range.Grid) || !Grids.HasComponent(range.Grid)) continue;
                var mass = Masses[range.Grid];
                float cellSize = Grids[range.Grid].CellSize;
                for (int c = range.Start; c < range.Start + range.Count; c++)
                {
                    var cell = Cells[c];
                    float kg = cell.Block < Blocks.Value.Blocks.Length ? Blocks.Value.Blocks[cell.Block].CellMass : 0;
                    MassAccumulator.AddCell(ref mass.Data, cell.Cell, cellSize, kg);
                }
                mass.Version++;
                Masses[range.Grid] = mass;
                Changed.Add(range.Grid);
            }
        }
    }
}
