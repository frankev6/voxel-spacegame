using Unity.Collections;

namespace Game.Voxel.Model
{
    /// <summary>ADR-0082: borrowed read-only source arrays for jobs; pin the owner until readers complete.</summary>
    public struct VoxelStructureReadView
    {
        public NativeArray<BrickRecord>.ReadOnly Bricks;
        public NativeArray<Cell>.ReadOnly Dense;
        public NativeArray<BrickSparseCells>.ReadOnly Sparse;
        public NativeArray<PaletteEntry>.ReadOnly Palette;
        public NativeArray<BlockInstance>.ReadOnly Instances;
        public static VoxelStructureReadView From(VoxelStructure structure)
        {
            var data = structure.Data;
            return new VoxelStructureReadView { Bricks = data.Bricks.AsArray().AsReadOnly(), Dense = data.CellPages.AsArray().AsReadOnly(),
                Sparse = data.SparsePages.AsArray().AsReadOnly(), Palette = data.Palette.AsArray().AsReadOnly(), Instances = data.Instances.AsArray().AsReadOnly() };
        }
        public Cell Read(int brickIndex, int local)
        {
            var brick = Bricks[brickIndex];
            if (brick.Page == -1) return brick.Uniform;
            if (brick.Page >= 0) return Dense[brick.Page * BrickMath.CellsPerBrick + local];
            int start = SparseCellStorage.PageIndex(brick.Page) * SparseCellStorage.Limit;
            int low = 0, high = brick.SolidCount;
            while (low < high)
            { int mid = (low + high) >> 1; if (Sparse[start + mid].Local < local) low = mid + 1; else high = mid; }
            return low < brick.SolidCount && Sparse[start + low].Local == local ? Sparse[start + low].Value : default;
        }
    }
}
