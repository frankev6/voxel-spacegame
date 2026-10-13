using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Blocks;
using Unity.Profiling;
using Game.Simulation.Abilities;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Orders per-grid energy allocation before race-free functional enable-mask writes (SPEC-22 §5.3).</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(PowerDemandGroup))]
    public partial struct PowerAllocationSystem : ISystem
    {
        static readonly ProfilerMarker AllocateMarker = new ProfilerMarker("PowerAllocationSystem.Allocate");
        static readonly ProfilerMarker ApplyMarker = new ProfilerMarker("PowerAllocationSystem.Apply");
        PowerAllocateJob allocate;
        PoweredApplyJob apply;
        EntityQuery functionals;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<PowerTuning>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>();
            allocate.Instances = state.GetBufferLookup<BlockInstance>(true); allocate.Requests = state.GetBufferLookup<PowerRequest>(true);
            allocate.Abilities = state.GetBufferLookup<AbilitySlot>(true); allocate.Inhibits = state.GetComponentLookup<Inhibited>(true);
            allocate.Hulks = state.GetComponentLookup<Hulk>(true); allocate.Spawning = state.GetComponentLookup<GridSpawning>(true);
            apply.Powers = state.GetComponentLookup<ShipPower>(true); apply.Consumers = state.GetBufferLookup<PowerConsumer>(true);
            apply.Functionals = state.GetComponentTypeHandle<FunctionalBlock>(true); apply.Links = state.GetComponentTypeHandle<PowerLink>(true);
            apply.Powered = state.GetComponentTypeHandle<Powered>(); apply.Entities = state.GetEntityTypeHandle();
            functionals = new EntityQueryBuilder(Allocator.Temp).WithAll<FunctionalBlock, PowerLink>().WithAllRW<Powered>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build(ref state);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            allocate.Instances.Update(ref state); allocate.Requests.Update(ref state); allocate.Abilities.Update(ref state);
            allocate.Inhibits.Update(ref state); allocate.Hulks.Update(ref state); allocate.Spawning.Update(ref state);
            allocate.Tick = SystemAPI.GetSingleton<SimClock>().Tick; allocate.Tuning = SystemAPI.GetSingleton<PowerTuning>();
            apply.Powers.Update(ref state); apply.Consumers.Update(ref state); apply.Functionals.Update(ref state);
            apply.Links.Update(ref state); apply.Powered.Update(ref state); apply.Entities.Update(ref state);
            apply.Tick = allocate.Tick; apply.Instances = allocate.Instances; apply.Requests = allocate.Requests; apply.Inhibits = allocate.Inhibits;
            using (AllocateMarker.Auto()) state.Dependency = allocate.ScheduleParallel(state.Dependency);
            using (ApplyMarker.Auto()) state.Dependency = apply.ScheduleParallel(functionals, state.Dependency);
        }
    }
}
