using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Hazards
{
    /// <summary>Polyline points of a Path zone, local-frame XZ, at least two (SPEC-91 §4.3).</summary>
    [InternalBufferCapacity(0)]
    public struct ZonePathPoint : IBufferElementData { public float2 Point; }
}
