using System;
using Unity.Collections;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. Blocks removed and added between two structures (§5.4).</summary>
    public struct StructureDiff : IDisposable
    { public NativeList<BlockPlacement> Removed; public NativeList<BlockPlacement> Added; public void Dispose() { throw new System.NotImplementedException("VOX-007"); } }
}
