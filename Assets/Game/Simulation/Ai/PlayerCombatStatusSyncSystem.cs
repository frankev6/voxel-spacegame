using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Simulation.Ai
{
    /// <summary>ADR-0117 derivation, every machine: the PlayerCombatStatus singleton is the PilotCombatStatus of the ship this machine
    /// flies, written where PlayerCombatStatusSystem wrote it before. Without exactly one PlayerShip no hostiles engage and the last
    /// damage tick is kept, as before.</summary>
    [BurstCompile, UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(PlayerCombatStatusSystem))]
    public partial struct PlayerCombatStatusSyncSystem : ISystem
    {
        EntityQuery players;
        ComponentLookup<PilotCombatStatus> pilots;
        ComponentLookup<PlayerCombatStatus> statuses;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            players = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip>().Build(ref state);
            pilots = state.GetComponentLookup<PilotCombatStatus>(true); statuses = state.GetComponentLookup<PlayerCombatStatus>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<PlayerCombatStatus>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            pilots.Update(ref state); statuses.Update(ref state);
            var list = players.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var gathered);
            var handle = new SyncJob { Players = list, Pilots = pilots, Statuses = statuses, Singleton = SystemAPI.GetSingletonEntity<PlayerCombatStatus>() }
                .Schedule(JobHandle.CombineDependencies(state.Dependency, gathered));
            state.Dependency = list.Dispose(handle);
        }

        [BurstCompile]
        struct SyncJob : IJob
        {
            [ReadOnly] public NativeList<Entity> Players;
            [ReadOnly] public ComponentLookup<PilotCombatStatus> Pilots;
            public ComponentLookup<PlayerCombatStatus> Statuses;
            public Entity Singleton;
            public void Execute()
            {
                if (!Statuses.HasComponent(Singleton)) return;
                var status = Statuses[Singleton]; status.HostilesEngaging = 0; status.Reserved = 0;
                // Multiple player tags are an invalid/ambiguous scene; never choose by chunk order.
                if (Players.Length == 1 && Pilots.HasComponent(Players[0]))
                { var pilot = Pilots[Players[0]]; status.HostilesEngaging = pilot.HostilesEngaging; status.LastDamagedTick = pilot.LastDamagedTick; }
                Statuses[Singleton] = status;
            }
        }
    }
}
