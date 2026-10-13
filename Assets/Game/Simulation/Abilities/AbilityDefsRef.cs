using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.2. Boot singleton referencing the baked ability definitions.</summary>
    public struct AbilityDefsRef : IComponentData { public BlobAssetReference<AbilityDefsBlob> Value; }
}
