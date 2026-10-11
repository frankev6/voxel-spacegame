using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>One usable thruster of the ship, grid-local. Rebuilt when VoxelGrid.Revision changes. Only writer: PropulsionSystem. Implements SPEC-20 §4.3.</summary>
    [InternalBufferCapacity(0)]
    public struct FlightThruster : IBufferElementData   // 44 bytes (Q9)
    {
        public Entity Thruster;          // functional entity
        public int InstanceIndex;        // BlockInstance index
        public float3 PositionLocal;     // grid-local metres: footprint centre (GridSpaceMath.InstanceCenter)
        public float3 ForceDirLocal;     // grid-local unit vector, Y = 0 (decision 5)
        public float MaxForce;           // N = ThrusterParams.ThrustNewtons
        public float Throttle;           // last solved throttle 0..BoostThrottle (warm start)
    }
}
