using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Main thread, outside ECS system updates, or inside GridSpawnSystem.</summary>
    public static class GridStructureSync
    {
        public static int WriteBack(EntityManager em, Entity root, VoxelStructureRegistry registry) { throw new System.NotImplementedException("VOX-014"); }      // into the source; returns bricks copied
        public static int CaptureChanges(EntityManager em, Entity root, VoxelStructure changedBricks, NativeList<int3> removedBricks) { throw new System.NotImplementedException("VOX-014"); } // since spawn
        public static void WriteBackAll(EntityManager em, EntityQuery roots, VoxelStructureRegistry registry) { throw new System.NotImplementedException("VOX-014"); }  // SPEC-31 calls before unloading
    }
}
