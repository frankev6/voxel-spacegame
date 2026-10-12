using Unity.Entities;

namespace Game.Simulation.Spatial
{
    /// <summary>Implements SPEC-23 §4.1: SpatialBody.</summary>
    public struct SpatialBody : IComponentData { public float Radius; }
}
