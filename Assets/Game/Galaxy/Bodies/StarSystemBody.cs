using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §4.2. StarSystemBody contract. Only writer: StarSystemLoader.</summary>
    public struct StarSystemBody : IComponentData
    {
        public BodyId Id;
        public ushort TemplateIndex;
        public BodyFlags Flags;
        public byte Reserved;
        public float RadiusM;
        public Entity Grid;
        public Entity Visual;
    }
}
