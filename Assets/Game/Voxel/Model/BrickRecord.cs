using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. One brick of a VoxelStructure, 28 bytes (SPEC-10 open question 13).</summary>
    public struct BrickRecord
    {
        public int3 Coord;             // brick coordinate
        public int Page;               // page number in CellPages (page p = cells [p*4096, p*4096+4096)); -1 = uniform
        public Cell Uniform;           // valid when Page == -1
        public ushort SolidCount;      // 1..4096
        public ushort Reserved;
        public uint ChangedRevision;   // Header.Revision of the last change to this brick
    }
}
