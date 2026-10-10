namespace Game.Data.Blocks
{
    /// <summary>Pushes along block-local −Z. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class ThrusterModule : BlockModule<ThrusterParams>                // stats: Thrust += OrientationTables.Rotate((0,0,−1), orient) × ThrustNewtons; PowerDraw
    { public ThrusterParams Params; public override BlockModuleId Id => BlockModuleId.Thruster; protected override ThrusterParams Bake(in BlockModuleBakeContext ctx) => Params; }
}
