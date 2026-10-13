using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Streaming
{
    /// <summary>Implements SPEC-31 §4.5. LoadedBody contract; behaviour belongs to the owning streaming task.</summary>
    internal struct LoadedBody
    {
        public Entity Anchor;
        public Entity Grid;
        public ulong GridPersistentId;
        public int StructureHandle;
        public byte WantsGrid;
        public byte GridRequested;
    }
}
