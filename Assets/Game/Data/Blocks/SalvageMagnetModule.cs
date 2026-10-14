namespace Game.Data.Blocks
{
    /// <summary>Extends the salvage reach of the ship carrying it (QA-001, ADR-0013 module). No stat contribution.</summary>
    [System.Serializable] public sealed class SalvageMagnetModule : BlockModule<SalvageMagnetParams>
    {
        public SalvageMagnetParams Params;
        public override BlockModuleId Id => BlockModuleId.SalvageMagnet;
        protected override SalvageMagnetParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void ContributeStats(ref BlockStatContribution stats, byte orient) { }
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.ReachBonusM", Params.ReachBonusM); }
    }
}
