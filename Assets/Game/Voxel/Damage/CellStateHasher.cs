using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Deterministic hash of all cell state (tests, harness). Not for hot paths.</summary>
    public static class CellStateHasher
    {
        public static ulong ComputeWorldHash(EntityManager em) { throw new System.NotImplementedException("DMG-014"); }   // grids ordered by PersistentId
        public static ulong ComputeGridHash(EntityManager em, Entity grid) { throw new System.NotImplementedException("DMG-014"); }
    }
}
