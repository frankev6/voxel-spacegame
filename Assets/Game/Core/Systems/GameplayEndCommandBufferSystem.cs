using Unity.Entities;

namespace Game.Core.Systems
{
    /// <summary>THE only structural sync point in the fixed step (architecture §1 rule 3, §5). Implements SPEC-02 §4.11, §5.8.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderLast = true)]
    public unsafe partial class GameplayEndCommandBufferSystem : EntityCommandBufferSystem
    {
        /// <summary>ECB singleton for fixed-step systems that record structural changes (SPEC-02 §5.8).</summary>
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal Unity.Collections.AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
                => EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            public void SetPendingBufferList(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<EntityCommandBuffer> buffers)
                => pendingBuffers = (Unity.Collections.LowLevel.Unsafe.UnsafeList<EntityCommandBuffer>*)
                                    Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref buffers);
            public void SetAllocator(Unity.Collections.Allocator allocatorIn) => allocator = allocatorIn;
            public void SetAllocator(Unity.Collections.AllocatorManager.AllocatorHandle allocatorIn) => allocator = allocatorIn;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }
}
