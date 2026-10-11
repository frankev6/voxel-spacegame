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
        [ReadOnly] public NativeArray<BrickSparseCells> Sparse;
        [ReadOnly] public NativeArray<PaletteEntry> Palette;
        [ReadOnly] public NativeArray<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        public float CellSize;
        public NativeArray<StructureMassData> Results;
        public void Execute(int index)
        {
            var brick = Bricks[index]; var result = new StructureMassData();
            int length = brick.Page <= -2 ? brick.SolidCount : BrickMath.CellsPerBrick;
            for (int i = 0; i < length; i++)
            {
                int local = i; Cell cell;
                if (brick.Page <= -2) { var entry = Sparse[SparseCellStorage.PageIndex(brick.Page) * SparseCellStorage.Limit + i]; local = entry.Local; cell = entry.Value; }
                else cell = brick.Page == -1 ? brick.Uniform : Cells[brick.Page * BrickMath.CellsPerBrick + local];
                if (cell.IsAir) continue;
                ushort block = cell.IsInstance ? Instances[cell.InstanceIndex].Block : Palette[cell.PaletteIndex].Block;
                MassAccumulator.AddCell(ref result, BrickMath.CellOf(brick.Coord, local), CellSize, Blocks.Value.Blocks[block].CellMass);
            }
            Results[index] = result;
        }
    }
}
