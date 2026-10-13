using System;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Galaxy.Streaming
{
    /// <summary>Main-thread transfer of an owned body snapshot into its persistent system delta.</summary>
    internal static class DamageCapture
    {
        // True means the delta owns the registry source; callers must relinquish local disposal ownership.
        internal static bool Capture(EntityManager em, Entity root, VoxelStructureRegistry registry, StarSystemDelta delta)
        {
            if (!em.Exists(root) || !em.HasComponent<BodyGridOwner>(root) || !em.HasComponent<VoxelGrid>(root) ||
                !em.HasComponent<GridSource>(root)) return false;
            var owner = em.GetComponentData<BodyGridOwner>(root);
            if (owner.PersistDamage == 0) return false;
            var source = em.GetComponentData<GridSource>(root);
            if ((source.Flags & GridSpawnFlags.WriteBack) == 0)
                throw new InvalidOperationException("Persistent body grids require a writable owned source.");
            var structure = registry.Get(source.StructureHandle);
            if (structure == null || !structure.Data.IsCreated)
                throw new InvalidOperationException("Persistent body source was released before capture.");
            bool alreadyOwned = delta.TryGetStructure(owner.Body.LocalKey, out var saved) &&
                ReferenceEquals(saved.Structure, structure);
            if (em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root)) return alreadyOwned;
            uint revision = em.GetComponentData<VoxelGrid>(root).Revision;
            if (revision != owner.CapturedRevision) GridStructureSync.WriteBack(em, root, registry);
            if (!alreadyOwned || revision != owner.CapturedRevision)
                delta.SetStructure(owner.Body.LocalKey, revision, structure);
            owner.CapturedRevision = revision;
            em.SetComponentData(root, owner);
            return true;
        }
    }
}
