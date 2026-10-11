using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Entities;

namespace Game.Voxel.Grid
{
    /// <summary>SPEC-10 §5.8: serial chunk-order drain, with native queue ownership chained across fixed-step users.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateBefore(typeof(GameplayEndCommandBufferSystem))]
    public partial struct BrickDirtyScheduleSystem : ISystem
    {
        EntityQuery dirty;
        EntityTypeHandle entities;
        ComponentTypeHandle<Brick> bricks;
        ComponentTypeHandle<BrickDirty> dirties;
        ComponentLookup<VoxelGrid> grids;
        BufferLookup<BrickSlot> slots;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            dirty = SystemAPI.QueryBuilder().WithAll<Brick>().WithAllRW<BrickDirty>().Build();
            entities = state.GetEntityTypeHandle(); bricks = state.GetComponentTypeHandle<Brick>(true);
            dirties = state.GetComponentTypeHandle<BrickDirty>(); grids = state.GetComponentLookup<VoxelGrid>(true);
            slots = state.GetBufferLookup<BrickSlot>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<BrickMeshQueue>(); state.RequireForUpdate<ColliderDirtyQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            // IsEmpty would synchronize writers to the enableable type. Filtering belongs to the scheduled job (Q39).
            if (dirty.IsEmptyIgnoreFilter) return;
            entities.Update(ref state); bricks.Update(ref state); dirties.Update(ref state); grids.Update(ref state); slots.Update(ref state);
            var mesh = SystemAPI.GetSingleton<BrickMeshQueue>(); var collider = SystemAPI.GetSingleton<ColliderDirtyQueue>();
            var dependency = mesh.Acquire(collider.Acquire(state.Dependency));
            var handle = new BrickDirtyDrainJob
            {
                Entities = entities, Bricks = bricks, Dirties = dirties, Grids = grids, Slots = slots,
                Damage = mesh.Damage, Neighbour = mesh.Neighbour, Spawn = mesh.Spawn, Lod = mesh.Lod,
                Heads = mesh.Heads, Pending = mesh.Pending, Colliders = collider.Requests
            }.Schedule(dirty, dependency);
            mesh.Release(handle); collider.Release(handle); state.Dependency = handle;
        }
    }
}
