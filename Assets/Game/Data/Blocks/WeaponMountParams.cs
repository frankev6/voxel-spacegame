namespace Game.Data.Blocks
{
    /// <summary>Weapon mount row, baked from WeaponMountModule (DATA-010). Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    public struct WeaponMountParams
    { public ushort WeaponIndex; /* IndexOf<WeaponDefinition>, 0 = none */ public Unity.Mathematics.float3 MuzzleOffsetCells; /* block-local, from the root cell centre; fire dir = +Z */ }
}
