using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
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
