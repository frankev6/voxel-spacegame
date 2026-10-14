using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Per-ship flight state on the root. Only writer: PropulsionSystem. Implements SPEC-20 §4.3.</summary>
    public struct FlightState : IComponentData
    {
        public uint SourceRevision;      // VoxelGrid.Revision the FlightThruster buffer was built from
        public float Gyro;               // last gyro throttle −1..1 (warm start)
        public float GyroTorque;         // N·m available this tick
        public float2 CoreAssist;        // last core manoeuvring throttles −1..1 (x strafe, y forward; warm start, ADR-0114)
        public float CoreAssistForce;    // N per axis available this tick
        public float2 LocalVelocity;     // m/s: x = strafe (+ right), y = forward
        public float YawRate;            // rad/s, + = turning right
        public float ForwardCap, ReverseCap, StrafeCap;   // m/s this tick, after multipliers and boost
        public float YawRateCap;         // rad/s this tick
        public float2 TargetLocalVelocity; // the assist target this tick (x strafe, y forward)
        public float TargetYawRate;
        public FlightFlags Flags;
    }
}
