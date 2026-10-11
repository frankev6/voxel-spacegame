using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Grid
{
    /// <summary>Serial deterministic spawn enqueue after the main-thread owner has completed queue access.</summary>
    [BurstCompile]
    internal struct EnqueueSpawnMeshesJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        public Entity Root;
        public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;
        public NativeArray<int> Heads;
        public NativeHashMap<Entity, byte> Pending;
        public void Execute()
        {
            // Guards stay with the singleton owner; the job receives only its borrowed containers (T2.11).
            var queue = new BrickMeshQueue { Damage = Damage, Neighbour = Neighbour, Spawn = Spawn, Lod = Lod, Heads = Heads, Pending = Pending };
            foreach (var entity in Entities) queue.Enqueue(entity, Root, MeshPriority.Spawn);
        }
    }
}
