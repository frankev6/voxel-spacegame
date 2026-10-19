namespace Game.Data.Blocks
{
    /// <summary>Core: build budget; its power comes from the block's PowerSource/PowerStorage modules. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class CoreModule : BlockModule<CoreParams>
    {
        public CoreParams Params;
        public override BlockModuleId Id => BlockModuleId.Core;
        protected override CoreParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        {
            ModuleChecks.AtLeastOne(report, "Params.MaxCells", Params.MaxCells); ModuleChecks.AtLeastOne(report, "Params.MaxExtentCells", Params.MaxExtentCells);
            if (Params.MinCells < 0 || Params.MinCells > Params.MaxCells)
                report.Error("DATA-E041", "", "Params.MinCells", $"Params.MinCells is {Params.MinCells}; it must be 0 (20% of MaxCells) or 1..MaxCells.");
        }
    }
}
