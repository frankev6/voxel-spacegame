using System;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Data
{
    /// <summary>Implements SPEC-05 §4.7. Decoder output; owns its containers.</summary>
    public sealed class VoxsDecoded : IDisposable
    {
        public float CellSize; public int AnchorInstance; public bool Partial;
        public NativeList<PaletteEntry> Palette;
        public NativeList<BlockInstance> Instances;          // Functional = Entity.Null
        public NativeList<int3> BrickCoords;                 // sorted (z, y, x)
        public NativeList<Cell> Cells;                       // BrickCoords.Length × 4096
        public NativeList<int3> RemovedBricks;
        public int DriftWarningCount;                        // content drift fixes applied (§5.7)
        public void Dispose()
        {
            if (Palette.IsCreated) Palette.Dispose();
            if (Instances.IsCreated) Instances.Dispose();
            if (BrickCoords.IsCreated) BrickCoords.Dispose();
            if (Cells.IsCreated) Cells.Dispose();
            if (RemovedBricks.IsCreated) RemovedBricks.Dispose();
        }
    }
}
