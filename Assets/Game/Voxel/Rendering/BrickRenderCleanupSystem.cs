using Game.Core.Systems;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

namespace Game.Voxel.Rendering
{
    /// <summary>Retire deleted-brick handles before this frame's draw, including during scene teardown (SPEC-11 §5.6/5.10).</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BeginPresentationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(PresentationBridgeGroup))]
    [UpdateBefore(typeof(DeferredDisposalSystem))]
    public partial struct BrickRenderCleanupSystem : ISystem
    {
        EntityQuery destroyed;
        static readonly ProfilerMarker Retire = new ProfilerMarker(ProfilerCategory.Scripts, "BrickRenderCleanupSystem.Retire");
        public void OnCreate(ref SystemState state)
        {
            destroyed = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<BrickRenderState>() }, None = new[] { ComponentType.ReadOnly<Brick>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            state.RequireForUpdate<VoxelRenderRuntime>(); state.RequireForUpdate<DeferredDisposalQueue>();
        }
        public void OnUpdate(ref SystemState state)
        {
            if (destroyed.IsEmptyIgnoreFilter) return;
            using var marker = Retire.Auto();
            var managed = SystemAPI.GetSingleton<VoxelRenderRuntime>().Managed.Value;
            var pool = managed ? managed.Pool : null;
            if (pool != null) pool.AdvanceFrame(SystemAPI.GetSingleton<DeferredDisposalQueue>().Frame);
            using (var states = destroyed.ToComponentDataArray<BrickRenderState>(Allocator.Temp))
                foreach (var render in states) if (render.Handle.Slot != 0) pool?.Release(render.Handle);
            // Query-wide structural removal happens at the next initialization boundary.
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            using var entities = destroyed.ToEntityArray(Allocator.Temp);
            ecb.RemoveComponent<BrickRenderState>(entities);
        }
    }
}
