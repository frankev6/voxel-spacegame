using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §5.6. BodyGridEvent contract. BodyGridWatchSystem output drained by the managed loader.</summary>
    public struct BodyGridEvent
    {
        public BodyId Body;
        public Entity Grid;
        public byte Kind;
    }
}
