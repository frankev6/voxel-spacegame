namespace Game.Data.Blocks
{
    /// <summary>Stores power. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class PowerStorageModule : BlockModule<PowerStorageParams>        // stats: PowerStorage
    { public PowerStorageParams Params; public override BlockModuleId Id => BlockModuleId.PowerStorage; protected override PowerStorageParams Bake(in BlockModuleBakeContext ctx) => Params; }
}
