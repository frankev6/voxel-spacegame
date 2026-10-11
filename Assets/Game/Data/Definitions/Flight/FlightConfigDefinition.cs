using UnityEngine;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Single config_flight definition and validation (SPEC-20 §4.4, §7).</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/Flight Config", fileName = "config_flight")]
    public sealed class FlightConfigDefinition : ContentDefinition
    {
        [SerializeField, Min(1)] private float maxForwardSpeed = 120f;
        [SerializeField, Range(0, 1)] private float reverseSpeedFraction = 0.5f;
        [SerializeField, Range(0, 1)] private float strafeSpeedFraction = 0.6f;
        [SerializeField, Range(1, 3)] private float boostSpeedMultiplier = 1.5f;
        [SerializeField, Range(1, 2)] private float boostThrottle = 1.25f;
        [SerializeField, Range(5, 720)] private float maxYawRateDeg = 120f;
        [SerializeField, Range(0.05f, 1)] private float linearAssistGain = 0.5f;
        [SerializeField, Range(0.05f, 1)] private float angularAssistGain = 0.5f;
        [SerializeField, Range(0.1f, 10)] private float torqueWeight = 2f;
        [SerializeField, Range(1, 16)] private int allocationSweeps = 6;
        [SerializeField, Range(0, 720)] private float coreYawAccelDeg = 60f;
        [SerializeField, Min(0)] private float coreMaxYawTorque = 2.0e6f;
        [SerializeField, Range(0, 0.05f)] private float throttleEpsilon = 0.001f;
        [SerializeField, Range(0.01f, 1)] private float planeCorrection = 0.2f;
        [SerializeField, Range(0.01f, 1)] private float intensityRise = 0.35f;
        [SerializeField, Range(0.01f, 1)] private float intensityFall = 0.15f;

        public float MaxForwardSpeed => maxForwardSpeed;
        public float ReverseSpeedFraction => reverseSpeedFraction;
        public float StrafeSpeedFraction => strafeSpeedFraction;
        public float BoostSpeedMultiplier => boostSpeedMultiplier;
        public float BoostThrottle => boostThrottle;
        public float MaxYawRateDeg => maxYawRateDeg;
        public float LinearAssistGain => linearAssistGain;
        public float AngularAssistGain => angularAssistGain;
        public float TorqueWeight => torqueWeight;
        public int AllocationSweeps => allocationSweeps;
        public float CoreYawAccelDeg => coreYawAccelDeg;
        public float CoreMaxYawTorque => coreMaxYawTorque;
        public float ThrottleEpsilon => throttleEpsilon;
        public float PlaneCorrection => planeCorrection;
        public float IntensityRise => intensityRise;
        public float IntensityFall => intensityFall;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (maxForwardSpeed <= 0)
                scope.Error("FLT-E001", nameof(maxForwardSpeed), "Maximum forward speed must be positive.");
            if (allocationSweeps < 1)
                scope.Error("FLT-E001", nameof(allocationSweeps), "Allocation requires at least one sweep.");
        }
    }
}
