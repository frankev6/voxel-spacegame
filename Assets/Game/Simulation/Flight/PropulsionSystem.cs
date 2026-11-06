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
    [UpdateAfter(typeof(PropulsionStatsSystem))]
    public partial struct PropulsionSystem : ISystem
    {
        static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "PropulsionSystem.Schedule");
        EntityStorageInfoLookup entities;
        ComponentLookup<GridSpawning> spawning;
        ComponentLookup<ThrusterState> thrusters;
        ComponentLookup<Powered> powered;
        ComponentLookup<ShipStatMultipliers> multipliers;
        ComponentLookup<Game.Voxel.Damage.ExternalPush> pushes;
        ComponentLookup<ShieldState> shields;
        ComponentLookup<Cruise> cruises;
        ComponentLookup<Flight3D> flight3D;   // ADR-0123
        ComponentLookup<Flight3DRequest> flight3DRequests;
        ComponentLookup<Weapons.ShipTarget> targets;
        ComponentLookup<Unity.Transforms.LocalTransform> poses;

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
            pushes = state.GetComponentLookup<Game.Voxel.Damage.ExternalPush>(true);
            shields = state.GetComponentLookup<ShieldState>(true);
            cruises = state.GetComponentLookup<Cruise>(true);
            flight3D = state.GetComponentLookup<Flight3D>(true); flight3DRequests = state.GetComponentLookup<Flight3DRequest>(true);
            targets = state.GetComponentLookup<Weapons.ShipTarget>(true); poses = state.GetComponentLookup<Unity.Transforms.LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = ScheduleMarker.Auto();
            entities.Update(ref state);
            spawning.Update(ref state);
            thrusters.Update(ref state);
            powered.Update(ref state); multipliers.Update(ref state); pushes.Update(ref state); shields.Update(ref state); cruises.Update(ref state);
            flight3D.Update(ref state); flight3DRequests.Update(ref state); targets.Update(ref state); poses.Update(ref state);
            uint tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            state.Dependency = new PropulsionJob
            {
                Config = SystemAPI.GetSingleton<FlightConfig>(),
                Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value,
                Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value,
                Entities = entities, Spawning = spawning, Thrusters = thrusters, Powered = powered, Multipliers = multipliers,
                Pushes = pushes, Shields = shields, Cruises = cruises, Tick = tick,
                Flight3D = flight3D, Flight3DRequests = flight3DRequests, Targets = targets, Poses = poses
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ThrusterIdleJob { Thrusters = thrusters }.Schedule(state.Dependency);
        }
    }
}
