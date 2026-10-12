using Unity.Entities;

namespace Game.Data.Components
{
    /// <summary>SPEC-21 §4.3: immutable attribution on a root launched or spawned by another root.</summary>
    public struct OwnedBy : IComponentData
    {
        public Entity Owner;
        public ulong OwnerPersistentId; // 0 when the owner is transient.
    }
}
