using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.2. Indexed ability definitions with entry zero reserved for none.</summary>
    public struct AbilityDefsBlob { public BlobArray<AbilityDef> Abilities; }
}
