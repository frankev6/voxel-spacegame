namespace Game.Data.Blocks
{
    /// <summary>Pushes along block-local −Z. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class ThrusterModule : BlockModule<ThrusterParams>                // stats: Thrust += OrientationTables.Rotate((0,0,−1), orient) × ThrustNewtons; PowerDraw
    {
        public ThrusterParams Params;
        public override BlockModuleId Id => BlockModuleId.Thruster;
        protected override ThrusterParams Bake(in BlockModuleBakeContext ctx) => Params;
        public override void ContributeStats(ref BlockStatContribution stats, byte orient)
        {
            stats.Thrust += OrientationTables.Rotate(new Unity.Mathematics.float3(0, 0, -1), orient) * Params.ThrustNewtons;
            stats.PowerDraw += Params.PowerDraw;
        }
        public override void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report)
        { ModuleChecks.Positive(report, "Params.ThrustNewtons", Params.ThrustNewtons); ModuleChecks.NonNegative(report, "Params.PowerDraw", Params.PowerDraw); }
    }
}
