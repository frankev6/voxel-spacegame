using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Owns the SfxEvent queue for the lifetime of the world (SPEC-64 §4.1, a copy of SPEC-63's FxEventQueueCreateSystem).
    /// Structural changes happen only in OnCreate/OnDestroy (world lifetime), never per tick.</summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct SfxEventQueueCreateSystem : ISystem
    {
        NativeQueue<SfxEvent> queue;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            queue = new NativeQueue<SfxEvent>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new SfxEventQueue { Queue = queue }, "SfxEventQueue");
            state.Enabled = false; // never updates; exists to own the allocation
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            if (queue.IsCreated) queue.Dispose();
        }
    }
}
