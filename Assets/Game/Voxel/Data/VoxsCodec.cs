using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Data
{
    /// <summary>Implements SPEC-05 §4.7. The only VOXS byte layout (§4.4, §5.7).</summary>
    public static class VoxsCodec
    {
        public const ushort Version = 1;
        public static int CellIndex(int3 local) { throw new System.NotImplementedException("PERS-007"); }   // x + 16 * (y + 16 * z); must equal SPEC-10's BrickCells layout
        public static byte[] Encode(in VoxsSource source, VoxsBlockNames names, VoxsEncodeOptions options) { throw new System.NotImplementedException("PERS-007"); }   // thread-agnostic
        public static bool TryDecode(ReadOnlySpan<byte> data, Game.Data.Services.ContentCatalog catalog,
                                     Allocator allocator, out VoxsDecoded result, out string error) { throw new System.NotImplementedException("PERS-007"); } // main thread (records unresolved IDs)
        public static bool TryPeek(ReadOnlySpan<byte> data, out ushort version, out bool partial, out int brickCount) { throw new System.NotImplementedException("PERS-007"); }
    }
}
