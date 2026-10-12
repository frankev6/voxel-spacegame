using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Spatial
{
    /// <summary>Implements SPEC-23 §4.1: SpatialProxy.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 64)]
    public struct SpatialProxy
    {
        public Entity Entity;            // grid root or celestial body entity
        public float3 Center;            // local frame: grids = world centre of the cell bounds; bodies = LocalTransform.Position
        public float Radius;             // max(HullRadius, shield reach) — what queries test
        public float HullRadius;         // grids: half diagonal of the cell bounds; bodies: body radius
        public float3 Velocity;          // PhysicsVelocity.Linear or 0
        public ProxyKind Kind;
        public ProxyFlags Flags;
        public Allegiance Allegiance;    // Game.Simulation.Allegiance; None for bodies and grids without one
        public uint Reserved;
    }
}
