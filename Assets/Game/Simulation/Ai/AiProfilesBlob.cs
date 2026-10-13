using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiProfilesBlob.</summary>
    public struct AiProfilesBlob
    {
        public BlobArray<AiProfile> Profiles;
        public BlobArray<FormationDef> Formations; // one-based catalog indices; zero has no follower slots (AI-013, SPEC-23 Q14)
    }
}
