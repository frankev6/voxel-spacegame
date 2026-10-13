using Game.Data.Definitions;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.5: expires enabled chunks before this tick's pool allocation.</summary>
    [BurstCompile]
    internal partial struct ChunkLifetimeJob : IJobEntity
    {
        public uint Tick;
        public EntityCommandBuffer Ecb;
        public NativeList<BlobAssetReference<Collider>> Retired;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        void Execute(Entity entity, ref ChunkBody body, ref LocalTransform transform, ref PhysicsCollider collider, ref ChunkColliderBlob owner)
        {
            if (body.Live == 0) return;
            if (Tick >= body.EndTick)
            {
                body.Live = 0;
                if (owner.Value.IsCreated) Retired.Add(owner.Value);
                owner.Value = default; collider.Value = default;
                Ecb.AddComponent<Disabled>(entity);
                // SPEC-63 uses continuous ChunkDissolve state; the completed fade needs no second event.
            }
            else if (Tick >= body.DissolveStartTick)
                transform.Scale = math.max(.05f, (float)(body.EndTick - Tick) / math.max(1u, body.EndTick - body.DissolveStartTick));
        }
    }

    // Reconstruct slots from real entities every tick: ECB-deferred identities never escape into persistent state.
    [BurstCompile, WithOptions(EntityQueryOptions.IncludeDisabledEntities)]
    internal partial struct CollectChunkSlotsJob : IJobEntity
    {
        public NativeArray<Entity> Entities;
        public NativeArray<ChunkBody> Bodies;
        void Execute(Entity entity, in ChunkBody body)
        {
            if (body.Slot >= Entities.Length) return;
            Entities[body.Slot] = entity; Bodies[body.Slot] = body;
        }
    }
}
