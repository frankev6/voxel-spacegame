using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponDefsBlob. <see cref="ChargeBeams"/> (WPN-019, §4.6d) is parallel to
    /// <see cref="Weapons"/> (same index; all-zero rows for every other delivery) so delivery data shares the weapon blob's lifetime.
    /// Hand-built test blobs may leave it empty; readers bounds-check.</summary>
    public struct WeaponDefsBlob { public BlobArray<WeaponDef> Weapons; public BlobArray<ChargeBeamDef> ChargeBeams; }
}
