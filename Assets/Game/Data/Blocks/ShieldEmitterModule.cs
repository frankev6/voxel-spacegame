namespace Game.Data.Blocks
{
    /// <summary>Projects the ship's shield. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class ShieldEmitterModule : BlockModule<ShieldEmitterParams>      // stats: ShieldCapacity, ShieldRegen, PowerDraw
    {
        public ShieldEmitterParams Params;
        public override BlockModuleId Id => BlockModuleId.ShieldEmitter;
        protected override ShieldEmitterParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void ContributeStats(ref BlockStatContribution stats, byte orient)
        {
            stats.ShieldCapacity += Params.Capacity; stats.ShieldRegen += Params.RegenPerSecond; stats.PowerDraw += Params.PowerDraw;
        }
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.Capacity", Params.Capacity); ModuleChecks.NonNegative(report, "Params.RegenPerSecond", Params.RegenPerSecond);
          ModuleChecks.NonNegative(report, "Params.RechargeDelaySeconds", Params.RechargeDelaySeconds); ModuleChecks.NonNegative(report, "Params.PowerDraw", Params.PowerDraw); }
    }
}
