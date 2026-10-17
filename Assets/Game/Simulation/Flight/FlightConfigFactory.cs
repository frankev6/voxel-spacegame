using Game.Data.Definitions;

namespace Game.Simulation.Flight
{
    /// <summary>Definition-to-ECS flight configuration (SPEC-20 §4.4).</summary>
    public static class FlightConfigFactory
    {
        public static FlightConfig Create(FlightConfigDefinition def) => new FlightConfig
        {
            MaxForwardSpeed = def.MaxForwardSpeed,
            ReverseSpeedFraction = def.ReverseSpeedFraction,
            StrafeSpeedFraction = def.StrafeSpeedFraction,
            BoostSpeedMultiplier = def.BoostSpeedMultiplier,
            BoostThrottle = def.BoostThrottle,
            MaxYawRateDeg = def.MaxYawRateDeg,
            LinearAssistGain = def.LinearAssistGain,
            CoastDeceleration = def.CoastDeceleration,
            AngularAssistGain = def.AngularAssistGain,
            TorqueWeight = def.TorqueWeight,
            AllocationSweeps = def.AllocationSweeps,
            CoreYawAccelDeg = def.CoreYawAccelDeg,
            CoreMaxYawTorque = def.CoreMaxYawTorque,
            CoreLinearAccel = def.CoreLinearAccel,
            CoreMaxLinearForce = def.CoreMaxLinearForce,
            ThrottleEpsilon = def.ThrottleEpsilon,
            PlaneCorrection = def.PlaneCorrection,
            IntensityRise = def.IntensityRise,
            IntensityFall = def.IntensityFall,
            // Carry (SPEC-91): off until HAZ-002 adds the config_flight values.
            CarryDecel = FlightConfig.Default.CarryDecel, CarryBrakeDecel = FlightConfig.Default.CarryBrakeDecel,
            CarryYawDecel = FlightConfig.Default.CarryYawDecel, MaxCarry = FlightConfig.Default.MaxCarry, MaxCarryYaw = FlightConfig.Default.MaxCarryYaw,
        };
    }
}
