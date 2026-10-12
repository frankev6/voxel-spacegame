using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponDefsRef.</summary>
    public struct WeaponDefsRef : IComponentData { public BlobAssetReference<WeaponDefsBlob> Value; }
}
