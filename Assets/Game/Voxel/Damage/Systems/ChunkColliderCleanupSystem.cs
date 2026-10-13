using Game.Core.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.9: moves expired, replaced and unloaded chunk colliders into deferred disposal.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(DeferredDisposalSystem))]
    public partial struct ChunkColliderCleanupSystem : ISystem
    {
        NativeList<BlobAssetReference<Collider>> retired;
        EntityQuery dead;
        public void OnCreate(ref SystemState state)
        {
            retired = new NativeList<BlobAssetReference<Collider>>(64, Allocator.Persistent);
            dead = SystemAPI.QueryBuilder().WithAll<ChunkColliderBlob>().WithNone<ChunkBody>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DeferredDisposalQueue>();
        }
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingleton<DamageStreams>().DrainRetiredChunkBlobs(retired);
            var deferred = SystemAPI.GetSingleton<DeferredDisposalQueue>();
            if (SystemAPI.TryGetSingleton<SimClock>(out var clock)) deferred.Tick = clock.Tick;
            foreach (var blob in retired) if (blob.IsCreated) DeferredDisposal.RetireBlob(ref deferred, blob);
            retired.Clear();
            foreach (var owner in SystemAPI.Query<RefRW<ChunkColliderBlob>>().WithNone<ChunkBody>().WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                if (owner.ValueRO.Value.IsCreated) DeferredDisposal.RetireBlob(ref deferred, owner.ValueRO.Value);
                owner.ValueRW.Value = default;
            }
            // Explicit SPEC-13 cleanup exception: one batch removal outside the fixed step.
            state.EntityManager.RemoveComponent<ChunkColliderBlob>(dead);
        }
        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            // World teardown may precede another presentation sweep; these owners have not entered the retirement queue.
            foreach (var owner in SystemAPI.Query<RefRW<ChunkColliderBlob>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                var blob = owner.ValueRO.Value;
                if (blob.IsCreated) blob.Dispose();
                owner.ValueRW.Value = default;
            }
            if (retired.IsCreated) retired.Dispose();
        }
    }
}
