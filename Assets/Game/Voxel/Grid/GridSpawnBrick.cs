using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{
    /// <summary>Original occupied coordinates in source record order; distinguishes removed from never-occupied slots (ADR-0026).</summary>
    [InternalBufferCapacity(0)]
    internal struct GridSpawnBrick : IBufferElementData { public int3 Coord; }
}
