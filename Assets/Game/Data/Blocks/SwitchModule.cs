namespace Game.Data.Blocks
{
    /// <summary>A switch: shooting it fires a mechanism trigger (ADR-0119, SPEC-91 §4.6). No stat contribution.</summary>
    [System.Serializable] public sealed class SwitchModule : BlockModule<SwitchParams>
    {
        public SwitchParams Params = new SwitchParams { TriggerDamage = 1 };
        public override BlockModuleId Id => BlockModuleId.Switch;
        protected override SwitchParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void ContributeStats(ref BlockStatContribution stats, byte orient) { }
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.TriggerDamage", Params.TriggerDamage); }
    }
}
