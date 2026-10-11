using System;
using Game.Data.Services;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.6. Converts between VoxelStructure and SPEC-05's VoxsSource/VoxsDecoded; no byte layout of its own.
    /// ToSource, Encode and FromDecoded take SPEC-05 types and are added by VOX-009 (SPEC-10 open question 14).</summary>
    public static class VoxelStructureCodec
    {
        public static bool TryDecode(ReadOnlySpan<byte> data, ContentCatalog catalog, out VoxelStructure result, out string error) { throw new System.NotImplementedException("VOX-009"); }
    }
}
