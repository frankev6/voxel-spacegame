using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §4.2. BodyGridOwner contract. Only writer: StarSystemLoader (creation and damage capture).</summary>
    public struct BodyGridOwner : IComponentData
    {
        public BodyId Body;
        public Entity Anchor;
        public uint CapturedRevision;
        public byte PersistDamage;
        public byte Reserved0;
        public ushort Reserved1;
    }
}
