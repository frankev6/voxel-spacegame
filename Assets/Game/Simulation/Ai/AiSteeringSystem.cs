using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Simulation.Ai
{
    /// <summary>Rewrites AI commands every fixed tick after decisions, without touching flight or physics (SPEC-23 §5.5).</summary>
    [BurstCompile, UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(AiDecisionSystem))]
    public partial struct AiSteeringSystem : ISystem
    {
        static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AiSteeringSystem.Schedule");
        AiSteerJob job;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AiConfig>();
            state.RequireForUpdate<FlightConfig>(); state.RequireForUpdate<SimClock>();
            job.Flights = state.GetComponentLookup<FlightState>(true);
            job.Members = state.GetComponentLookup<FleetMember>(true);
            job.Propulsion = state.GetComponentLookup<PropulsionStats>(true);
            job.Targets = state.GetComponentLookup<ShipTarget>(true);
            job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            job.Grids = state.GetComponentLookup<VoxelGrid>(true);
            job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            job.Multipliers = state.GetComponentLookup<ShipStatMultipliers>(true);
            job.Shields = state.GetComponentLookup<ShieldState>(true);
            job.Thrusters = state.GetBufferLookup<FlightThruster>(true);
            job.Flight3D = state.GetComponentLookup<Game.Data.Components.Flight3D>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = ScheduleMarker.Auto();
            job.Members.Update(ref state); job.Flights.Update(ref state); job.Propulsion.Update(ref state); job.Targets.Update(ref state);
            job.Transforms.Update(ref state); job.Grids.Update(ref state); job.Hulks.Update(ref state); job.Spawning.Update(ref state);
            job.Velocities.Update(ref state); job.Multipliers.Update(ref state); job.Shields.Update(ref state); job.Thrusters.Update(ref state);
            job.Flight3D.Update(ref state);
            job.FlightConfig = SystemAPI.GetSingleton<FlightConfig>();
            job.Config = SystemAPI.GetSingleton<AiConfig>(); job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }
}
