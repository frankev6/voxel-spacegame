using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.6.</summary>
    public struct StorySpawnRecord                 // managed (ContentId); never in ECS data
    {
        public Game.Core.ContentId Template;       // body template ID
        public ulong AnchorBodyKey;                // 0 = position relative to the star
        public Unity.Mathematics.double3 OffsetM;  // from the anchor's position (XZ used; Y ignored)
        public float YawRad;
        public bool Removed;                       // removal keeps the index (slot stability)
    }
}
