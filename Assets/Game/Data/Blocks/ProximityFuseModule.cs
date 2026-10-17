namespace Game.Data.Blocks
{
    /// <summary>Makes its device a mine: arm, sense, detonate (SPEC-91 §4.4, ADR-0118 decision 7). No stat contribution.</summary>
    [System.Serializable] public sealed class ProximityFuseModule : BlockModule<ProximityFuseParams>
    {
        public ProximityFuseParams Params = new ProximityFuseParams { BlastType = Game.Data.Combat.DamageType.Explosive };
        public override BlockModuleId Id => BlockModuleId.ProximityFuse;
        protected override ProximityFuseParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void ContributeStats(ref BlockStatContribution stats, byte orient) { }
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        {
            ModuleChecks.Positive(report, "Params.TriggerRadius", Params.TriggerRadius);
            ModuleChecks.Positive(report, "Params.BlastRadius", Params.BlastRadius);
            ModuleChecks.Positive(report, "Params.BlastDamage", Params.BlastDamage);
        }
    }
}
