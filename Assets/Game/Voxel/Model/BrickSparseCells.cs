using System.Runtime.InteropServices;
using Unity.Entities;

namespace Game.Voxel.Model
{
    /// <summary>ADR-0023: six-byte sorted solid entry. Dense and sparse buffers coexist; exactly one holds cell storage.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    [InternalBufferCapacity(0)]
    public struct BrickSparseCells : IBufferElementData
    {
        public ushort Local;
        public Cell Value;
    }
}
