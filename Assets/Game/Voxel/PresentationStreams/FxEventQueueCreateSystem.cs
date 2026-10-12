using Game.Data.Streams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-63 §4.1; queue ownership follows ADR-0050.</summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct FxEventQueueCreateSystem : ISystem
    {
        NativeQueue<FxEvent> queue;
        GuardedHandles access;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            queue = new NativeQueue<FxEvent>(Allocator.Persistent);
            access = GuardedHandles.Create();
            state.EntityManager.CreateSingleton(new FxEventQueue { Queue = queue, Access = access }, "FxEventQueue");
            state.Enabled = false; // never updates; exists to own the allocation
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            access.CompleteAccess();
            if (queue.IsCreated) queue.Dispose();
            access.Dispose();
        }
    }
}
