using System;
using Game.Voxel.Data;
using Unity.Mathematics;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §5.4: owns the native source arrays for one imported ship; dispose after encoding.</summary>
    internal sealed class LegacyShipBuildResult : IDisposable
    {
        internal VoxsSource Source;
        internal MigrationShipRow Statistics;
        internal int3 BoundsMin, BoundsMax;
        public void Dispose()
        {
            if (Source.Palette.IsCreated) Source.Palette.Dispose();
            if (Source.Instances.IsCreated) Source.Instances.Dispose();
            if (Source.BrickCoords.IsCreated) Source.BrickCoords.Dispose();
            if (Source.Cells.IsCreated) Source.Cells.Dispose();
            Source = default;
        }
    }
}
