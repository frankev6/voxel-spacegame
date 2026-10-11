using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Cells of a chunk body for rendering (SPEC-63). Offsets are 0..15 per axis.</summary>
    [InternalBufferCapacity(0)]
    public struct ChunkCell : IBufferElementData { public byte X, Y, Z, Orient; public ushort Block; public byte Color, Pad; }
}
