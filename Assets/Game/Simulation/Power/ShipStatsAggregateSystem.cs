using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Profiling;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.4 shared functional/mass/modifier stats followed by power/shield module totals.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(PowerAllocationSystem))]
    public partial struct ShipStatsAggregateSystem : ISystem
    {
        static readonly ProfilerMarker AggregateMarker = new ProfilerMarker("ShipStatsAggregateSystem.Aggregate");
        static readonly ProfilerMarker PowerMarker = new ProfilerMarker("ShipStatsAggregateSystem.Power");
        EntityQuery roots;
        ShipStatsJob stats;
        PowerStatsJob power;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<PowerTuning>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>();
            roots = new EntityQueryBuilder(Allocator.Temp).WithAllRW<ShipStats, ShipStatMultipliers>().WithAll<BlockInstance, VoxelGrid>().WithNone<Hulk>().Build(ref state);
            stats.Entities = state.GetEntityTypeHandle(); stats.Stats = state.GetComponentTypeHandle<ShipStats>();
            stats.Multipliers = state.GetComponentTypeHandle<ShipStatMultipliers>(); stats.Instances = state.GetBufferTypeHandle<BlockInstance>(true);
            stats.Grids = state.GetComponentTypeHandle<VoxelGrid>(true); stats.Masses = state.GetComponentTypeHandle<PhysicsMass>(true);
            stats.Modifiers = state.GetBufferTypeHandle<ShipStatModifier>(true); stats.Spawning = state.GetComponentLookup<GridSpawning>(true);
            stats.Dormancy = state.GetComponentLookup<Dormant>(true); power.Requests = state.GetBufferLookup<PowerRequest>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            stats.Entities.Update(ref state); stats.Stats.Update(ref state); stats.Multipliers.Update(ref state);
            stats.Instances.Update(ref state); stats.Grids.Update(ref state); stats.Masses.Update(ref state);
            stats.Modifiers.Update(ref state); stats.Spawning.Update(ref state); stats.Dormancy.Update(ref state); power.Requests.Update(ref state);
            stats.LastSystemVersion = state.LastSystemVersion; stats.Definitions = SystemAPI.GetSingleton<BlockDefsRef>().Value;
            power.Definitions = stats.Definitions; power.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value;
            power.Spawning = stats.Spawning; power.Dormancy = stats.Dormancy;
            using (AggregateMarker.Auto()) state.Dependency = stats.ScheduleParallel(roots, state.Dependency);
            using (PowerMarker.Auto()) state.Dependency = power.ScheduleParallel(state.Dependency);
        }
    }
}
