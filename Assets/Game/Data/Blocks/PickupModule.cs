namespace Game.Data.Blocks
{
    /// <summary>A pickup: a ship flying within reach collects its energy (ADR-0119, SPEC-91 §4.6). No stat contribution.</summary>
    [System.Serializable] public sealed class PickupModule : BlockModule<PickupParams>
    {
        public PickupParams Params = new PickupParams { Energy = 50, Reach = 4 };
        public override BlockModuleId Id => BlockModuleId.Pickup;
        protected override PickupParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void ContributeStats(ref BlockStatContribution stats, byte orient) { }
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.Energy", Params.Energy); ModuleChecks.Positive(report, "Params.Reach", Params.Reach); }
    }
}
