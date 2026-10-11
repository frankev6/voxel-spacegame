using System;
using Game.Voxel.Data;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.6. Owns expanded brick coordinates/cells; palette and instance arrays borrow structure storage.</summary>
    public sealed class VoxsSourceBuffers : IDisposable
    {
        public VoxsSource Source;
        public void Dispose()
        {
            if (Source.BrickCoords.IsCreated) Source.BrickCoords.Dispose();
            if (Source.Cells.IsCreated) Source.Cells.Dispose();
            Source = default;
        }
    }
}
