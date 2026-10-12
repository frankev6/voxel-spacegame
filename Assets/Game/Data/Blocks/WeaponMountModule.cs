namespace Game.Data.Blocks
{
    /// <summary>Mounts a weapon and its muzzle. v1 module, implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable] public sealed class WeaponMountModule : BlockModule<WeaponMountParams>
    {
        public Unity.Mathematics.float3 MuzzleOffsetCells;
        [UnityEngine.SerializeField] private Game.Data.Definitions.WeaponDefinition weapon;
        public Game.Data.Definitions.WeaponDefinition Weapon { get => weapon; set => weapon = value; }
        public override BlockModuleId Id => BlockModuleId.WeaponMount;
        protected override WeaponMountParams Bake(in BlockModuleBakeContext ctx) => new WeaponMountParams { WeaponIndex = weapon == null ? (ushort)0 : ctx.Catalog.IndexOf<Game.Data.Definitions.WeaponDefinition>(weapon), MuzzleOffsetCells = MuzzleOffsetCells };
    }
}
