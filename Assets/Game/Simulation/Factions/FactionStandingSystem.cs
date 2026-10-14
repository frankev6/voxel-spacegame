using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Simulation.Factions
{
    /// <summary>Single fixed-step standing writer; schedules after matrix readers without main-thread completion.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup), OrderFirst = true), UpdateBefore(typeof(ShipCommandGroup))]
    public partial struct FactionStandingSystem : ISystem
    {
        static readonly ProfilerMarker ProvokeMarker = new("FactionStandingSystem.Provoke");
        static readonly ProfilerMarker ApplyMarker = new("FactionStandingSystem.Apply");
        EntityQuery damaged, players;
        ComponentTypeHandle<Allegiance> allegiances;
        ComponentTypeHandle<GridDamageStats> damage;
        BufferLookup<FactionStanding> standings;
        BufferLookup<FactionRelationRequest> requests;
        ComponentLookup<FactionState> states;
        ComponentLookup<FactionRelations> relations;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<FactionState>(); state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<ManagedEventQueue<FactionRelationChanged>>();
            damaged = new EntityQueryBuilder(Allocator.Temp).WithAll<Allegiance, GridDamageStats>().WithNone<PlayerShip>().Build(ref state);
            damaged.SetChangedVersionFilter(ComponentType.ReadOnly<GridDamageStats>());
            players = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip>().WithNone<Parent>().Build(ref state);
            allegiances = state.GetComponentTypeHandle<Allegiance>(true); damage = state.GetComponentTypeHandle<GridDamageStats>(true);
            standings = state.GetBufferLookup<FactionStanding>(); requests = state.GetBufferLookup<FactionRelationRequest>();
            states = state.GetComponentLookup<FactionState>(); relations = state.GetComponentLookup<FactionRelations>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            allegiances.Update(ref state); damage.Update(ref state); standings.Update(ref state); requests.Update(ref state);
            states.Update(ref state); relations.Update(ref state);
            var singleton = SystemAPI.GetSingletonEntity<FactionState>();
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var provoked = CollectionHelper.CreateNativeArray<byte>(64, state.WorldUpdateAllocator);
            var playerList = players.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var gathered);
            JobHandle provoke;
            using (ProvokeMarker.Auto())
                provoke = new ProvokeJob { Allegiances = allegiances, Damage = damage, Players = playerList,
                    Provoked = provoked, Tick = tick, Count = 64 }.Schedule(damaged, gathered);
            JobHandle apply;
            using (ApplyMarker.Auto())
                apply = new FactionApplyJob { Singleton = singleton, Tick = tick, Provoked = provoked,
                    Matrix = SystemAPI.GetSingleton<FactionRelations>().Stance,
                    Standings = standings, Requests = requests, States = states, Relations = relations,
                    Events = SystemAPI.GetSingleton<ManagedEventQueue<FactionRelationChanged>>().Queue.AsParallelWriter() }.Schedule(provoke);
            state.Dependency = playerList.Dispose(apply);
        }
    }
}
