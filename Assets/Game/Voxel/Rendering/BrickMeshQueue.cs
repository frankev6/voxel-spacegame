using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct BrickMeshQueue : IComponentData
    {
        public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;
        public NativeArray<int> Heads;
        public NativeHashMap<Entity, byte> Pending;
        public Game.Data.Streams.GuardedHandles Access;
        public static BrickMeshQueue Create() => new BrickMeshQueue
        {
            Damage = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Neighbour = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Spawn = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Lod = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Heads = new NativeArray<int>(4, Allocator.Persistent),
            Pending = new NativeHashMap<Entity, byte>(4096, Allocator.Persistent),
            Access = Game.Data.Streams.GuardedHandles.Create(),
        };
        public void Dispose()
        {
            Access.Dispose();
            if (Damage.IsCreated) Damage.Dispose();
            if (Neighbour.IsCreated) Neighbour.Dispose();
            if (Spawn.IsCreated) Spawn.Dispose();
            if (Lod.IsCreated) Lod.Dispose();
            if (Heads.IsCreated) Heads.Dispose();
            if (Pending.IsCreated) Pending.Dispose();
        }
        public JobHandle Acquire(JobHandle dependsOn) => Access.Acquire(dependsOn);
        public void Release(JobHandle lastJob) => Access.Release(lastJob);
        public void CompleteAccess() => Access.CompleteAccess();
        public void Enqueue(Entity brick, Entity grid, MeshPriority p) => throw new System.NotImplementedException("REN-010");
        public bool TryDequeue(out BrickMeshRequest r) => throw new System.NotImplementedException("REN-010");
        public int Count => throw new System.NotImplementedException("REN-010");
    }
}
