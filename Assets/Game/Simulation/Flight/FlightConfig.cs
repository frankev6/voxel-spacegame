using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Singleton, created at boot by SPEC-01 BootSingletons from config_flight. Implements SPEC-20 §4.3.</summary>
    public struct FlightConfig : IComponentData
    {
        public float MaxForwardSpeed;      // 120 m/s
        public float ReverseSpeedFraction; // 0.5 of MaxForwardSpeed
        public float StrafeSpeedFraction;  // 0.6 of MaxForwardSpeed
        public float BoostSpeedMultiplier; // 1.5 (forward cap only)
        public float BoostThrottle;        // 1.25 (max thruster throttle while boosting)
        public float MaxYawRateDeg;        // 120 deg/s
        public float LinearAssistGain;     // 0.5: fraction of the velocity error corrected per tick before saturation
        public float CoastDeceleration;    // 0.08/s: neutral forward braking, limited by real thrusters
        public float AngularAssistGain;    // 0.5
        public float TorqueWeight;         // 2.0: allocation weight of yaw relative to linear force
        public int AllocationSweeps;       // 6
        public float CoreYawAccelDeg;      // 60 deg/s²: gyro torque = Iyy × this, capped
        public float CoreMaxYawTorque;     // 2.0e6 N·m
        public float ThrottleEpsilon;      // 0.001: smaller throttles are 0
        public float PlaneCorrection;      // 0.2: fraction of the Y offset removed per tick
        public float IntensityRise;        // 0.35: ThrusterState.Intensity EMA factor when rising
        public float IntensityFall;        // 0.15: EMA factor when falling
        public static FlightConfig Default => new FlightConfig {
            MaxForwardSpeed = 120f, ReverseSpeedFraction = 0.5f, StrafeSpeedFraction = 0.6f, BoostSpeedMultiplier = 1.5f,
            BoostThrottle = 1.25f, MaxYawRateDeg = 120f, LinearAssistGain = 0.5f, CoastDeceleration = 0.08f, AngularAssistGain = 0.5f, TorqueWeight = 2f,
            AllocationSweeps = 6, CoreYawAccelDeg = 60f, CoreMaxYawTorque = 2.0e6f, ThrottleEpsilon = 0.001f,
            PlaneCorrection = 0.2f, IntensityRise = 0.35f, IntensityFall = 0.15f };
    }
}
