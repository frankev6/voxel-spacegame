using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Flight formulas shared with SPEC-22 (stats), SPEC-23 (AI) and SPEC-61 (HUD). Implements SPEC-20 §5.3.</summary>
    public static class FlightMath
    {
        /// N·m of core reaction torque: min(Iyy × radians(CoreYawAccelDeg), CoreMaxYawTorque); 0 if inverseInertiaY ≤ 0.
        public static float GyroYawTorque(in FlightConfig c, float inverseInertiaY) => throw new System.NotImplementedException("FLT-002");
        /// Caps after multipliers. Boost raises the forward cap only.
        public static void SpeedCaps(in FlightConfig c, float maxSpeedMultiplier, bool boost, out float forward, out float reverse, out float strafe) => throw new System.NotImplementedException("FLT-002");
        public static float YawRateCap(in FlightConfig c, float turnRateMultiplier) => math.radians(c.MaxYawRateDeg) * turnRateMultiplier;
        /// World velocity → (strafe, forward) in the ship's frame.
        public static float2 ToLocalPlanar(float3 worldVelocity, quaternion rotation) => throw new System.NotImplementedException("FLT-002");
        /// Assist targets of §5.3 step 4 for one ship.
        public static void Targets(in FlightConfig c, float2 thrust, float turn, Commands.ShipCommandFlags flags, float2 localVelocity,
                                   float forwardCap, float reverseCap, float strafeCap, float yawRateCap,
                                   out float2 targetVelocity, out float targetYawRate) => throw new System.NotImplementedException("FLT-002");
    }
}
