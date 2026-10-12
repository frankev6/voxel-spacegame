using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiConfig.</summary>
    public struct AiConfig : IComponentData
    {
        public BlobAssetReference<AiProfilesBlob> Profiles;
        public float HashCellSize, CombatBubbleRadius, ArrivalRadius, AvoidanceMargin;
        public int DormantThinkMultiplier;
        public ushort DefaultProfileIndex;
    }
}
