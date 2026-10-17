using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>One usable thruster of the ship, grid-local. Rebuilt when VoxelGrid.Revision changes. Only writer: PropulsionSystem. Implements SPEC-20 §4.3.</summary>
    [InternalBufferCapacity(0)]
    public struct FlightThruster : IBufferElementData   // 56 bytes (Q9; spool and boost added by ADR-0120)
    {
        public Entity Thruster;          // functional entity
        public int InstanceIndex;        // BlockInstance index
        public float3 PositionLocal;     // grid-local metres: footprint centre (GridSpaceMath.InstanceCenter)
        public float3 ForceDirLocal;     // grid-local unit vector, Y = 0 (decision 5)
        public float MaxForce;           // N = ThrusterParams.ThrustNewtons
        public float Throttle;           // last solved throttle 0..BoostMax (warm start, and the spool's starting point)
        public float SpoolUp;            // throttle gained per second at most; 0 = instant (1 / ThrusterParams.SpoolUpSeconds)
        public float SpoolDown;          // throttle shed per second at most; 0 = instant
        public float BoostMax;           // throttle ceiling while boosting; 0 = FlightConfig.BoostThrottle
    }
}
