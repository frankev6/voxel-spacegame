using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.11: independent exact brick sums; output index is unique to this worker.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct BrickMassJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<RecomputeBrick> Work;
        [ReadOnly] public NativeArray<RecomputeRoot> Roots;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BufferLookup<BrickCells> Cells;
        [ReadOnly] public BufferLookup<BrickSparseCells> Sparse;
        [ReadOnly] public BufferLookup<BrickSolidMask> Masks;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        public NativeArray<StructureMassData> Results;
        public void Execute(int index)
        {
            var work = Work[index]; var root = Roots[work.Root]; var masks = Masks[work.Brick];
            var cells = Cells[work.Brick]; var sparse = Sparse[work.Brick]; var palette = Palettes[root.Entity]; var instances = Instances[root.Entity];
            var sum = new StructureMassData();
            for (int w = 0; w < masks.Length; w++)
            {
                ulong bits = masks[w].Bits;
                while (bits != 0)
                {
                    int local = (w << 6) + math.tzcnt(bits); bits &= bits - 1;
                    var cell = BrickWrite.Get(cells, sparse, local); if (cell.IsAir) continue;
                    ushort block = cell.IsInstance ? instances[cell.InstanceIndex].Block : palette[cell.PaletteIndex].Block;
                    float kg = block < Blocks.Value.Blocks.Length ? Blocks.Value.Blocks[block].CellMass : 0;
                    MassAccumulator.AddCell(ref sum, BrickMath.CellOf(work.Coord, local), root.CellSize, kg);
                }
            }
            Results[index] = sum;
        }
    }
}
