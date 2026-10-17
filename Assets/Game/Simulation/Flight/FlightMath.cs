using Unity.Mathematics;
using Game.Simulation.Commands;

namespace Game.Simulation.Flight
{
    /// <summary>Flight formulas shared with SPEC-22 (stats), SPEC-23 (AI) and SPEC-61 (HUD). Implements SPEC-20 §5.3.</summary>
    public static class FlightMath
    {
        /// N·m of core reaction torque: min(Iyy × radians(CoreYawAccelDeg), CoreMaxYawTorque); 0 if inverseInertiaY ≤ 0.
        public static float GyroYawTorque(in FlightConfig c, float inverseInertiaY)
            => inverseInertiaY > 0 ? math.min(math.radians(c.CoreYawAccelDeg) / inverseInertiaY, c.CoreMaxYawTorque) : 0;
        /// N of core manoeuvring assist per local planar axis: min(mass × CoreLinearAccel, CoreMaxLinearForce); 0 if inverseMass ≤ 0 (ADR-0114).
        public static float CoreAssistForce(in FlightConfig c, float inverseMass)
            => inverseMass > 0 ? math.min(c.CoreLinearAccel / inverseMass, c.CoreMaxLinearForce) : 0;
        /// Caps after multipliers. Boost raises the forward cap only.
        public static void SpeedCaps(in FlightConfig c, float maxSpeedMultiplier, bool boost, out float forward, out float reverse, out float strafe)
        {
            float baseSpeed = c.MaxForwardSpeed * maxSpeedMultiplier;
            forward = baseSpeed * (boost ? c.BoostSpeedMultiplier : 1f);
            reverse = baseSpeed * c.ReverseSpeedFraction;
            strafe = baseSpeed * c.StrafeSpeedFraction;
        }
        public static float YawRateCap(in FlightConfig c, float turnRateMultiplier) => math.radians(c.MaxYawRateDeg) * turnRateMultiplier;
        /// SPEC-91 §5.1 step 4.2: each carry axis keeps the sign of the measured velocity and never exceeds it, then the max clamp.
        public static float2 ClampCarry(float2 carry, float2 measured, float max)
            => new float2(ClampCarry(carry.x, measured.x, max), ClampCarry(carry.y, measured.y, max));
        public static float ClampCarry(float carry, float measured, float max)
        {
            if (!math.isfinite(carry) || carry == 0 || math.sign(carry) != math.sign(measured)) return 0;
            return math.sign(carry) * math.min(math.min(math.abs(carry), math.abs(measured)), max);
        }
        /// SPEC-91 §5.1 step 4.4: linear carry shrinks as a vector, yaw carry toward 0.
        public static void DecayCarry(in FlightConfig c, bool brake, ref float2 carry, ref float carryYaw)
        {
            float d = (brake ? c.CarryBrakeDecel : c.CarryDecel) * Game.Core.SimTime.FixedTimestep, length = math.length(carry);
            carry = length > d ? carry * ((length - d) / length) : float2.zero;
            float y = c.CarryYawDecel * Game.Core.SimTime.FixedTimestep;
            carryYaw = math.abs(carryYaw) > y ? carryYaw - math.sign(carryYaw) * y : 0;
        }
        /// World velocity → (strafe, forward) in the ship's frame.
        public static float2 ToLocalPlanar(float3 worldVelocity, quaternion rotation)
        {
            float3 local = math.rotate(math.inverse(rotation), worldVelocity);
            return new float2(local.x, local.z);
        }
        /// Assist targets of §5.3 step 4 for one ship.
        public static void Targets(in FlightConfig c, float2 thrust, float turn, Commands.ShipCommandFlags flags, float2 localVelocity,
                                   float forwardCap, float reverseCap, float strafeCap, float yawRateCap,
                                   out float2 targetVelocity, out float targetYawRate)
        {
            if ((flags & ShipCommandFlags.Brake) != 0) { targetVelocity = default; targetYawRate = 0; return; }
            // Each command axis is independent: a bad sample must not erase the other valid axis.
            thrust = math.clamp(math.select(float2.zero, thrust, math.isfinite(thrust)), -1f, 1f);
            turn = math.isfinite(turn) ? math.clamp(turn, -1f, 1f) : 0;
            float forward = thrust.y > 0 ? thrust.y * forwardCap : thrust.y < 0 ? thrust.y * reverseCap :
                math.clamp(localVelocity.y, -reverseCap, forwardCap) *
                math.max(0, 1 - c.CoastDeceleration * Game.Core.SimTime.FixedTimestep / math.max(c.LinearAssistGain, .0001f));
            targetVelocity = new float2(thrust.x * strafeCap, forward);
            targetYawRate = turn * yawRateCap;
        }
    }
}
