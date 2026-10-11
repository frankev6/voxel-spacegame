using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.4. Called by GridSpawnSystem on the main thread, inside its structural update, after it created
    /// brick or functional entities for `root` (root already carries every requester component).
    /// May make batch structural changes on `created` only.</summary>
    public interface IGridSpawnHook
    {
        void OnEntitiesCreated(EntityManager em, Entity root, NativeArray<Entity> created);
    }
}
