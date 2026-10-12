using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiProfilesBlob.</summary>
    public struct AiProfilesBlob { public BlobArray<AiProfile> Profiles; }
}
