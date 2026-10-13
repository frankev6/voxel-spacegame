using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Interaction
{
    /// <summary>Implements SPEC-31 §4.3. InteractionInputState contract; behaviour belongs to the owning streaming task.</summary>
    public struct InteractionInputState : IComponentData
    {
        public byte LastInteractCounter;
        public byte Initialized;
    }
}
