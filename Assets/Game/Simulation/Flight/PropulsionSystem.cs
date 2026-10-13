using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.Model;
using Game.Simulation.Power;
using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;

namespace Game.Simulation.Flight
{
    /// <summary>Schedules physical thrust and one-time hulk shutdown. Implements SPEC-20 §5.3.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    public partial struct PropulsionSystem : ISystem
    {
        static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "PropulsionSystem.Schedule");
        EntityStorageInfoLookup entities;
        ComponentLookup<GridSpawning> spawning;
        ComponentLookup<ThrusterState> thrusters;
        ComponentLookup<Powered> powered;
        ComponentLookup<ShipStatMultipliers> multipliers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<FlightConfig>();
            state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<BlockModulesRef>();
            state.RequireForUpdate<OrientationTablesRef>();
            entities = state.GetEntityStorageInfoLookup();
            spawning = state.GetComponentLookup<GridSpawning>(true);
            thrusters = state.GetComponentLookup<ThrusterState>();
            powered = state.GetComponentLookup<Powered>(true);
            multipliers = state.GetComponentLookup<ShipStatMultipliers>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = ScheduleMarker.Auto();
            entities.Update(ref state);
            spawning.Update(ref state);
            thrusters.Update(ref state);
            powered.Update(ref state); multipliers.Update(ref state);
            state.Dependency = new PropulsionJob
            {
                Config = SystemAPI.GetSingleton<FlightConfig>(),
                Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value,
                Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value,
                Entities = entities, Spawning = spawning, Thrusters = thrusters, Powered = powered, Multipliers = multipliers
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ThrusterIdleJob { Thrusters = thrusters }.Schedule(state.Dependency);
        }
    }
}
