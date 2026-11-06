using Unity.Burst;

namespace Game.Voxel.Model
{
    /// <summary>VOX-022: synchronous bounded ingestion loops; pointers are borrowed only for each direct call.</summary>
    [BurstCompile]
    internal static unsafe class DenseBrickIngestion
    {
        [BurstCompile]
        internal static int Analyze(Cell* source, out BrickMaskBits mask)
        {
            mask = default;
            int count = 0;
            for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                if (!source[i].IsAir) { mask.Set(i, true); count++; }
            return count;
        }
        [BurstCompile]
        internal static void CopyDense(Cell* source, Cell* destination)
        {
            for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                destination[i] = source[i].IsAir ? default : source[i];
        }
        [BurstCompile]
        internal static void CopySparse(Cell* source, BrickSparseCells* destination)
        {
            int at = 0;
            for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                if (!source[i].IsAir) destination[at++] = new BrickSparseCells { Local = (ushort)i, Value = source[i] };
            for (; at < SparseCellStorage.Limit; at++) destination[at] = default;
        }
    }
}
