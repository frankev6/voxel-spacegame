using Game.Core.Systems;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Entities;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001 / ADR-0117 decider (authority only): a collected wreck despawns once SalvagePullSystem has streamed its last
    /// wave. A co-op guest's copy goes when the host's despawn arrives.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameInitGroup)), UpdateAfter(typeof(SalvagePullSystem)), UpdateBefore(typeof(GridSpawnSystem))]
    public partial struct SalvageDespawnSystem : ISystem
    {
        ComponentLookup<GridDespawnRequest> despawns;
        [BurstCompile] public void OnCreate(ref SystemState state)
        { despawns = state.GetComponentLookup<GridDespawnRequest>(); state.RequireForUpdate<SalvageCollected>(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            despawns.Update(ref state);
            state.Dependency = new DespawnJob { Despawns = despawns }.Schedule(state.Dependency);
        }

        [BurstCompile]
        partial struct DespawnJob : IJobEntity
        {
            public ComponentLookup<GridDespawnRequest> Despawns;
            void Execute(Entity root, in SalvageCollected collected)
            {
                // GridSpawnApi.RequestDespawn
                if (collected.Elapsed >= FxCollect.StaggerSeconds && Despawns.HasComponent(root)) Despawns.SetComponentEnabled(root, true);
            }
        }
    }
}
