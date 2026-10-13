using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Interaction
{
    /// <summary>Implements SPEC-31 §4.3. Interactable contract; behaviour belongs to the owning streaming task.</summary>
    public struct Interactable : IComponentData
    {
        public InteractionKind Kind;
        public byte Reserved0;
        public ushort TemplateIndex;
        public float DistanceM;
        public BodyId Body;
    }
}
