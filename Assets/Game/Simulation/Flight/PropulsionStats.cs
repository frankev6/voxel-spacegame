using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Feature stats of propulsion (ADR-0014). On every ship root (FlightSetup). Only writer: PropulsionStatsSystem (M2). Implements SPEC-20 §4.5.</summary>
    public struct PropulsionStats : Unity.Entities.IComponentData
    {
        // Functional part: recomputed when ShipStats.FunctionalRevision != SourceRevision
        public float ForwardThrust, ReverseThrust, StrafeThrust;  // N (strafe = min of left and right)
        public float YawTorque;         // N·m = min(Σ positive yaw torques, Σ |negative yaw torques|) of the thrusters
        public float ThrusterDraw;      // E/s at full throttle
        public int AliveThrusterCount;
        public uint SourceRevision;
        // Mass part: recomputed when the functional part, PhysicsMass or ShipStatMultipliers changed
        public float GyroYawTorque;     // N·m = FlightMath.GyroYawTorque(FlightConfig, InverseInertia.y)
        public float ForwardAccel;      // m/s² = ForwardThrust × Thrust multiplier / ShipStats.Mass
        public float ReverseAccel;      // m/s²
        public float StrafeAccel;       // m/s²
        public float YawAccelDeg;       // deg/s² = degrees((YawTorque × Thrust mult + GyroYawTorque) × TurnRate mult × InverseInertia.y)
        public float TurnRateDeg;       // deg/s: 90 / t90 with t90 = 2·sqrt((π/2) / yawAccelRad); 0 if yawAccel = 0
    }
}
