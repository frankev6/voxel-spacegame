using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Interaction
{
    /// <summary>Implements SPEC-31 §4.3. InteractionTarget contract; behaviour belongs to the owning streaming task.</summary>
    public struct InteractionTarget : IComponentData
    {
        public Entity Target;
        public BodyId Body;
        public InteractionKind Kind;
        public byte InRange;
        public ushort TemplateIndex;
        public float DistanceM;
        public ulong GridPersistentId;
    }
}
