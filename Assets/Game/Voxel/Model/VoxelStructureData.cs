using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. Copy = same storage. Only VoxelStructure disposes.</summary>
    public struct VoxelStructureData
    {
        public NativeReference<StructureHeader> Header;
        public NativeList<PaletteEntry> Palette;                 // [0] reserved (Block 0)
        public NativeList<BlockInstance> Instances;
        public NativeList<int> FreeInstances;                    // tombstoned instance indices, LIFO reuse
        public NativeList<BrickRecord> Bricks;                   // unordered; SortedBrickOrder gives canonical order
        public NativeList<BrickMaskBits> Masks;                  // parallel to Bricks
        public NativeHashMap<int3, int> BrickIndex;              // coord → index into Bricks (lookup only, never iterated)
        public NativeList<Cell> CellPages;                       // dense pages of 4096 cells, local linear order
        public NativeList<int> FreePages;                        // free page numbers, LIFO reuse
        public bool IsCreated => Header.IsCreated;
    }
}
