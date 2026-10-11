using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. One brick of a VoxelStructure, 28 bytes (SPEC-10 open question 13).</summary>
    public struct BrickRecord
    {
        public int3 Coord;             // brick coordinate
        public int Page;               // >=0: dense page; -1: uniform; <=-2: sparse page index = -2-Page (ADR-0023)
        public Cell Uniform;           // valid when Page == -1
        public ushort SolidCount;      // 1..4096
        public ushort Reserved;
        public uint ChangedRevision;   // Header.Revision of the last change to this brick
    }
}
