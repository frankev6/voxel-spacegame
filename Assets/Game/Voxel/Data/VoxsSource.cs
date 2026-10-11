using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Data
{
    /// <summary>Implements SPEC-05 §4.7. Input views for encoding. The caller keeps ownership of every array.</summary>
    public struct VoxsSource
    {
        public float CellSize;
        public int AnchorInstance;
        public NativeArray<PaletteEntry> Palette;
        public NativeArray<BlockInstance> Instances;
        public NativeArray<int3> BrickCoords;  // K bricks
        public NativeArray<Cell> Cells;        // K × 4096; brick b at [b*4096, (b+1)*4096), index = VoxsCodec.CellIndex(local)
        public bool Partial;
        public NativeArray<int3> RemovedBricks; // only when Partial; may be default (empty)
    }
}
