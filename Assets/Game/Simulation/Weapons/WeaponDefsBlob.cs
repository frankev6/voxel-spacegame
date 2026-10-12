using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponDefsBlob.</summary>
    public struct WeaponDefsBlob { public BlobArray<WeaponDef> Weapons; }
}
