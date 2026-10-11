using Game.Core;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Spawn, rebuild, despawn and edit requests for managed callers (App, Galaxy, ShipEditor, tests).</summary>
    public static class GridSpawnApi
    {
        public static Entity RequestSpawn(EntityManager em, VoxelStructureRegistry registry, int structureHandle, in GridSpawnParams p) { throw new System.NotImplementedException("VOX-013"); }
        public static Entity RequestSpawn(EntityCommandBuffer ecb, VoxelStructureRegistry registry, int structureHandle, in GridSpawnParams p) { throw new System.NotImplementedException("VOX-013"); } // BeginInitialization ECB
        public static Entity RequestSpawnBlueprint(EntityManager em, ContentHash blueprint, in GridSpawnParams p) { throw new System.NotImplementedException("VOX-013"); }     // WriteBack is cleared
        public static Entity RequestSpawnBlueprint(EntityCommandBuffer ecb, ContentHash blueprint, in GridSpawnParams p) { throw new System.NotImplementedException("VOX-013"); }
        public static void RequestRebuild(EntityManager em, VoxelStructureRegistry registry, Entity root, int structureHandle, GridSpawnFlags flags) { throw new System.NotImplementedException("VOX-014"); }
        public static void RequestDespawn(EntityManager em, Entity root) { throw new System.NotImplementedException("VOX-014"); }          // enables GridDespawnRequest
        public static void RequestEdit(EntityManager em, Entity root, NativeArray<int3> changedBrickCoords) { throw new System.NotImplementedException("VOX-020"); }   // M3
    }
}
