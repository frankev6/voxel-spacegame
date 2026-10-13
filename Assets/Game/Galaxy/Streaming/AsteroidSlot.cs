using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Streaming
{
    /// <summary>Implements SPEC-31 §4.5. AsteroidSlot contract; behaviour belongs to the owning streaming task.</summary>
    internal struct AsteroidSlot
    {
        public Entity Grid;
        public BodyId Id;
        public int StructureHandle;
    }
}
