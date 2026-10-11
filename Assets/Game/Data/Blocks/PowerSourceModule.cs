namespace Game.Data.Blocks
{
    /// <summary>Generates power. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class PowerSourceModule : BlockModule<PowerSourceParams>          // stats: PowerGeneration
    {
        public PowerSourceParams Params;
        public override BlockModuleId Id => BlockModuleId.PowerSource;
        protected override PowerSourceParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.Generation", Params.Generation); }
    }
}
