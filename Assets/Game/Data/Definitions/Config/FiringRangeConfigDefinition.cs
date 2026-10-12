using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>FND-020 authored setup for the repeatable test-station firing checkpoint (ADR-0053).</summary>
    [ContentIdPrefix("config_"), CreateAssetMenu(menuName = "Game/Content/Firing Range")]
    public sealed class FiringRangeConfigDefinition : ContentDefinition
    {
        [SerializeField] BlockDefinition hull, slope, longSlope, core, reactor, cargo, thruster;
        [SerializeField] byte hullColor = 15, bridgeColor = 13, moduleColor = 10;
        [SerializeField] float targetCellSize = 1;
        [SerializeField] Vector3 targetOffset = new Vector3(0, 0, 45);
        [SerializeField] float targetYaw;
        [SerializeField] Material tracerMaterial;
        [SerializeField] float tracerLength = 3, tracerWidth = .12f;
        public BlockDefinition Hull => hull;
        public BlockDefinition Slope => slope;
        public BlockDefinition LongSlope => longSlope;
        public BlockDefinition Core => core;
        public BlockDefinition Reactor => reactor;
        public BlockDefinition Cargo => cargo;
        public BlockDefinition Thruster => thruster;
        public byte HullColor => hullColor;
        public byte BridgeColor => bridgeColor;
        public byte ModuleColor => moduleColor;
        public float TargetCellSize => targetCellSize;
        public Vector3 TargetOffset => targetOffset;
        public float TargetYaw => targetYaw;
        public Material TracerMaterial => tracerMaterial;
        public float TracerLength => tracerLength;
        public float TracerWidth => tracerWidth;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            if (hull == null || slope == null || longSlope == null || core == null || reactor == null || cargo == null || thruster == null || !(targetCellSize > 0) || !float.IsFinite(targetCellSize) || tracerMaterial == null || !(tracerLength > 0) || !(tracerWidth > 0))
                scope.Error("FND-E020", "range", "Station block definitions, positive sizes and tracer material are required.");
        }
    }
}
