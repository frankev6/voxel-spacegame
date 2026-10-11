using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. Palette entry, 4 bytes; buffer element on the grid root.</summary>
    public struct PaletteEntry : IBufferElementData
    {
        public ushort Block;   // BlockIndex (SPEC-04); 0 only in entry 0 (reserved) or an unused entry
        public byte Orient;    // 0..23
        public byte Color;     // paint palette index (SPEC-11 VoxelPaletteDefinition)
    }
}
