using System;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.6. Owns the arrays a VoxsSource built from a VoxelStructure needs (uniform bricks expanded). Dispose after encoding.
    /// The `Source` field (SPEC-05 VoxsSource) is added by VOX-009 with the SPEC-05 types (SPEC-10 open question 14).</summary>
    public sealed class VoxsSourceBuffers : IDisposable
    {
        public void Dispose() { throw new System.NotImplementedException("VOX-009"); }                        // frees BrickCoords and Cells; idempotent
    }
}
