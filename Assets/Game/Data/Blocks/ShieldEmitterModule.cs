namespace Game.Data.Blocks
{
    /// <summary>Projects the ship's shield. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class ShieldEmitterModule : BlockModule<ShieldEmitterParams>      // stats: ShieldCapacity, ShieldRegen, PowerDraw
    { public ShieldEmitterParams Params; public override BlockModuleId Id => BlockModuleId.ShieldEmitter; protected override ShieldEmitterParams Bake(in BlockModuleBakeContext ctx) => Params; }
}
