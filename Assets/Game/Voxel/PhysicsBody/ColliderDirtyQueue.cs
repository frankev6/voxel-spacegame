using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Request queue with handle-guarded access (SPEC-12 §4.4/§5.1). Fixed-step users read the singleton, schedule
    /// jobs with Requests after Acquire(dependency), then Release(lastJob). Outside fixed step call CompleteAccess before direct use.
    /// Never use GetSingletonRW, mutate this struct after Create, or pass Access into jobs.</summary>
    public struct ColliderDirtyQueue : IComponentData
    {
        public NativeList<ColliderRequest> Requests;
        public Game.Data.Streams.GuardedHandles Access;
        public static ColliderDirtyQueue Create() => new ColliderDirtyQueue
        {
            Requests = new NativeList<ColliderRequest>(1024, Allocator.Persistent),
            Access = Game.Data.Streams.GuardedHandles.Create(),
        };
        public void Dispose()
        {
            Access.Dispose();
            if (Requests.IsCreated) Requests.Dispose();
        }
        public JobHandle Acquire(JobHandle dependsOn) => Access.Acquire(dependsOn);
        public void Release(JobHandle lastJob) => Access.Release(lastJob);
        public void CompleteAccess() => Access.CompleteAccess();
        public void Enqueue(uint serial, Entity grid, Entity brick, int3 coord, ColliderPriority priority)
            => Requests.Add(new ColliderRequest { Serial = serial, Grid = grid, Brick = brick, Coord = coord, Priority = priority });
    }
}
