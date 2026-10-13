using UnityEngine;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-32 §4.3 authored travel defaults and field-local validation.</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/World/Travel Config", fileName = "config_travel")]
    public sealed class TravelConfigDefinition : ContentDefinition
    {
        [SerializeField] private int spoolTicks = 30;
        [SerializeField] private int arrivalTicks = 30;
        [SerializeField] private float coverMinSeconds = 0.5f;
        [SerializeField] private int revealDelayFrames = 2;
        [SerializeField] private float transitTimeoutSeconds = 15f;
        [SerializeField] private float arrivalStandoffM = 150f;
        [SerializeField] private float arrivalStepM = 50f;
        [SerializeField] private int arrivalMaxSteps = 16;
        [SerializeField] private float playerClearanceM = 60f;
        [SerializeField] private ItemDefinition fuelItem;
        [SerializeField] private float fuelPerLy = 0.5f;
        [SerializeField] private int fuelMinPerJump = 1;
        [SerializeField] private int ancientGateFuel = 0;
        [SerializeField] private int laneRefreshTicks = 600;
        [SerializeField] private AudioCueDefinition departCue;
        [SerializeField] private AudioCueDefinition arriveCue;
        [SerializeField] private float warpFxScale = 10f;
        [SerializeField] private int cruiseSpoolTicks = 60;
        [SerializeField] private float cruiseMinDistanceM = 3000f;
        [SerializeField] private float cruiseStandoffM = 300f;
        [SerializeField] private int cruiseDamageCooldownTicks = 300;
        [SerializeField] private float escortRadiusM = 2000f;

        public int SpoolTicks => spoolTicks;
        public int ArrivalTicks => arrivalTicks;
        public float CoverMinSeconds => coverMinSeconds;
        public int RevealDelayFrames => revealDelayFrames;
        public float TransitTimeoutSeconds => transitTimeoutSeconds;
        public float ArrivalStandoffM => arrivalStandoffM;
        public float ArrivalStepM => arrivalStepM;
        public int ArrivalMaxSteps => arrivalMaxSteps;
        public float PlayerClearanceM => playerClearanceM;
        public ItemDefinition FuelItem => fuelItem;
        public float FuelPerLy => fuelPerLy;
        public int FuelMinPerJump => fuelMinPerJump;
        public int AncientGateFuel => ancientGateFuel;
        public int LaneRefreshTicks => laneRefreshTicks;
        public AudioCueDefinition DepartCue => departCue;
        public AudioCueDefinition ArriveCue => arriveCue;
        public float WarpFxScale => warpFxScale;
        public int CruiseSpoolTicks => cruiseSpoolTicks;
        public float CruiseMinDistanceM => cruiseMinDistanceM;
        public float CruiseStandoffM => cruiseStandoffM;
        public int CruiseDamageCooldownTicks => cruiseDamageCooldownTicks;
        public float EscortRadiusM => escortRadiusM;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            Check(scope, "TRV-E002", nameof(spoolTicks), spoolTicks >= 1 && spoolTicks <= 600, "Spool ticks must be in 1..600.");
            Check(scope, "TRV-E002", nameof(arrivalTicks), arrivalTicks >= 1 && arrivalTicks <= 600, "Arrival ticks must be in 1..600.");
            Check(scope, "TRV-E002", nameof(coverMinSeconds), FiniteRange(coverMinSeconds, 0, 5), "Cover duration must be finite and in 0..5 seconds.");
            Check(scope, "TRV-E002", nameof(transitTimeoutSeconds), FiniteAtLeast(transitTimeoutSeconds, 5), "Transit timeout must be finite and at least 5 seconds.");
            Check(scope, "TRV-E002", nameof(revealDelayFrames), revealDelayFrames >= 0 && revealDelayFrames <= 10, "Reveal delay must be in 0..10 frames.");
            Check(scope, "TRV-E002", nameof(laneRefreshTicks), laneRefreshTicks >= 60, "Lane refresh must be at least 60 ticks.");
            Check(scope, "TRV-E003", nameof(fuelItem), fuelItem != null, "Fuel item must be set.");
            Check(scope, "TRV-E003", nameof(fuelPerLy), FiniteAtLeast(fuelPerLy, 0), "Fuel per light-year must be finite and nonnegative.");
            Check(scope, "TRV-E003", nameof(fuelMinPerJump), fuelMinPerJump >= 0, "Minimum fuel must be nonnegative.");
            Check(scope, "TRV-E003", nameof(ancientGateFuel), ancientGateFuel >= 0, "Ancient gate fuel must be nonnegative.");
            Check(scope, "TRV-E004", nameof(arrivalStandoffM), FiniteRange(arrivalStandoffM, 50, 2000), "Arrival standoff must be finite and in 50..2000 metres.");
            Check(scope, "TRV-E004", nameof(arrivalStepM), FiniteRange(arrivalStepM, 10, 500), "Arrival step must be finite and in 10..500 metres.");
            Check(scope, "TRV-E004", nameof(arrivalMaxSteps), arrivalMaxSteps >= 0 && arrivalMaxSteps <= 64, "Arrival steps must be in 0..64.");
            Check(scope, "TRV-E004", nameof(playerClearanceM), float.IsFinite(playerClearanceM) && playerClearanceM >= 0 && playerClearanceM <= arrivalStandoffM, "Player clearance must be finite and in 0..arrival standoff.");
            Check(scope, "TRV-E008", nameof(departCue), departCue != null, "Departure cue must be set.");
            Check(scope, "TRV-E008", nameof(arriveCue), arriveCue != null, "Arrival cue must be set.");
            Check(scope, "TRV-E008", nameof(warpFxScale), float.IsFinite(warpFxScale) && warpFxScale > 0, "Warp scale must be finite and positive.");
            Check(scope, "TRV-E008", nameof(cruiseSpoolTicks), cruiseSpoolTicks >= 1, "Cruise spool must be at least one tick.");
            Check(scope, "TRV-E008", nameof(cruiseMinDistanceM), FiniteAtLeast(cruiseMinDistanceM, 500), "Cruise minimum distance must be finite and at least 500 metres.");
            Check(scope, "TRV-E008", nameof(cruiseStandoffM), FiniteAtLeast(cruiseStandoffM, 50), "Cruise standoff must be finite and at least 50 metres.");
            Check(scope, "TRV-E008", nameof(escortRadiusM), FiniteAtLeast(escortRadiusM, 0), "Escort radius must be finite and nonnegative.");
        }
        static bool FiniteRange(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
        static bool FiniteAtLeast(float value, float min) => float.IsFinite(value) && value >= min;
        static void Check(ValidationScope scope, string code, string field, bool valid, string message)
        { if (!valid) scope.Error(code, field, message); }
    }
}
