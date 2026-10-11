namespace Game.Data.Blocks
{
    /// <summary>Adds cargo capacity. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class CargoModule : BlockModule<CargoParams>                      // stats: CargoUnits
    {
        public CargoParams Params;
        public override BlockModuleId Id => BlockModuleId.Cargo;
        protected override CargoParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.Capacity", Params.Capacity); }
    }
}
