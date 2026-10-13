using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-32 §4.3 authored travel defaults; validation and the config asset belong to TRV-002.</summary>
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

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("TRV-002");
        }
    }
}
