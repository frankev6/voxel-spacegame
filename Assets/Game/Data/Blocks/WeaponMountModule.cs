namespace Game.Data.Blocks
{
    /// <summary>Mounts a weapon and its muzzle. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class WeaponMountModule : BlockModule<WeaponMountParams>
    {
        public Unity.Mathematics.float3 MuzzleOffsetCells;
        // DATA-010 (M2) adds: [SerializeField] WeaponDefinition weapon (+ getter); Bake then sets WeaponIndex = ctx.Catalog.IndexOf<WeaponDefinition>(weapon)
        // (0 if null); ContributeStats adds weapon.SustainedDps and weapon.PowerDraw (SPEC-21 getters). Until then WeaponIndex = 0 (unarmed).
        public override BlockModuleId Id => BlockModuleId.WeaponMount;
        protected override WeaponMountParams Bake(in BlockModuleBakeContext ctx) => new WeaponMountParams { WeaponIndex = 0, MuzzleOffsetCells = MuzzleOffsetCells };
    }
}
