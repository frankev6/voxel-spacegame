using System;
using Game.Core;
using Game.Data.Services;
using Game.Voxel.Model;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.4. Decoded blueprint cache, owned by SPEC-01's composition root.</summary>
    public sealed class BlueprintLibrary : IDisposable
    {
        public BlueprintLibrary(ContentCatalog catalog, VoxelContent content) { throw new System.NotImplementedException("VOX-011"); }   // decodes with VoxelStructureCodec.TryDecode(payload, catalog, …)
        public bool TryGet(ContentHash blueprint, out VoxelStructure structure) { throw new System.NotImplementedException("VOX-011"); }   // decodes on first use; read-only; cached
        public void ReleaseUnpinnedLarge() { throw new System.NotImplementedException("VOX-011"); }                       // disposes cached structures with > 65,536 cells and PinCount == 0
        public int CachedCount => throw new NotImplementedException("VOX-011");
        public void Dispose() { throw new System.NotImplementedException("VOX-011"); }                                    // disposes every cached structure
    }
}
