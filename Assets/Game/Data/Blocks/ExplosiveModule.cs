namespace Game.Data.Blocks
{
    /// <summary>Explodes when the block is destroyed. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class ExplosiveModule : BlockModule<ExplosiveParams>
    { public ExplosiveParams Params; public override BlockModuleId Id => BlockModuleId.Explosive; protected override ExplosiveParams Bake(in BlockModuleBakeContext ctx) => Params; }
}
