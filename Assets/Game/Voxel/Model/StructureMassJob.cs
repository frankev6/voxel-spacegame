using Game.Data.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.4: independent per-brick sums, reduced in canonical order by the owner.</summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal struct StructureMassJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BrickRecord> Bricks;
        [ReadOnly] public NativeArray<Cell> Cells;
        [ReadOnly] public NativeArray<PaletteEntry> Palette;
        [ReadOnly] public NativeArray<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        public float CellSize;
        public NativeArray<StructureMassData> Results;
        public void Execute(int index)
        {
            var brick = Bricks[index]; var result = new StructureMassData();
            for (int local = 0; local < BrickMath.CellsPerBrick; local++)
            {
                Cell cell = brick.Page < 0 ? brick.Uniform : Cells[brick.Page * BrickMath.CellsPerBrick + local];
                if (cell.IsAir) continue;
                ushort block = cell.IsInstance ? Instances[cell.InstanceIndex].Block : Palette[cell.PaletteIndex].Block;
                MassAccumulator.AddCell(ref result, BrickMath.CellOf(brick.Coord, local), CellSize, Blocks.Value.Blocks[block].CellMass);
            }
            Results[index] = result;
        }
    }
}
