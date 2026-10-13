using Game.Galaxy.Components;
using Game.Voxel.Grid;
using Unity.Collections;
using Unity.Entities;

namespace Game.Galaxy.Streaming
{
    /// <summary>Propagates a body grid's star-system ownership to newly created children (SPEC-31 §5.12).</summary>
    internal sealed class StarSystemSpawnHook : IGridSpawnHook
    {
        public void OnEntitiesCreated(EntityManager em, Entity root, NativeArray<Entity> created)
        {
            if (created.Length == 0 || !em.HasComponent<StarSystemMember>(root)) return;
            em.AddSharedComponent(created, em.GetSharedComponent<StarSystemMember>(root));
        }
    }
}
