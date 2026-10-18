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
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(Game.Data.Streams.SimEventGather.Job<Game.Simulation.Power.EnergyGrantRequest>))]

namespace Game.Simulation.Power
{
    /// <summary>Orders per-grid energy allocation before race-free functional enable-mask writes (SPEC-22 §5.3). Also the consumer of
    /// StatusChannels.EnergyGrants (HAZ-019, ADR-0119 decision 7): grants are added to ShipPower.Stored, capped by Capacity, in
    /// (ship, producer, sequence) order before this tick's allocation.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(PowerDemandGroup))]
    public partial struct PowerAllocationSystem : ISystem
    {
        NativeList<EnergyGrantRequest> grants;
        ComponentLookup<ShipPower> grantPowers;
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
            allocate.HostCharges = state.GetComponentLookup<AbilityHostState>(true);
            apply.Powers = state.GetComponentLookup<ShipPower>(true); apply.Consumers = state.GetBufferLookup<PowerConsumer>(true);
            apply.Functionals = state.GetComponentTypeHandle<FunctionalBlock>(true); apply.Links = state.GetComponentTypeHandle<PowerLink>(true);
            apply.Powered = state.GetComponentTypeHandle<Powered>(); apply.Entities = state.GetEntityTypeHandle();
            grants = new NativeList<EnergyGrantRequest>(8, Allocator.Persistent);
            grantPowers = state.GetComponentLookup<ShipPower>();
            functionals = new EntityQueryBuilder(Allocator.Temp).WithAll<FunctionalBlock, PowerLink>().WithAllRW<Powered>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build(ref state);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            allocate.Instances.Update(ref state); allocate.Requests.Update(ref state); allocate.Abilities.Update(ref state);
            allocate.Inhibits.Update(ref state); allocate.Hulks.Update(ref state); allocate.Spawning.Update(ref state); allocate.HostCharges.Update(ref state);
            allocate.Tick = SystemAPI.GetSingleton<SimClock>().Tick; allocate.Tuning = SystemAPI.GetSingleton<PowerTuning>();
            apply.Powers.Update(ref state); apply.Consumers.Update(ref state); apply.Functionals.Update(ref state);
            apply.Links.Update(ref state); apply.Powered.Update(ref state); apply.Entities.Update(ref state);
            apply.Tick = allocate.Tick; apply.Instances = allocate.Instances; apply.Requests = allocate.Requests; apply.Inhibits = allocate.Inhibits;
            if (SystemAPI.TryGetSingleton<StatusChannels>(out var status))
            {
                grantPowers.Update(ref state);
                var set = status.EnergyGrants.BeginRead(ref state);
                var cleared = new ClearGrantsJob { Grants = grants }.Schedule(state.Dependency);
                var gathered = Game.Data.Streams.SimEventGather.Schedule(set, grants, cleared);
                status.EnergyGrants.EndRead(gathered);
                state.Dependency = new ApplyGrantsJob { Grants = grants, Powers = grantPowers }.Schedule(gathered);
            }
            using (AllocateMarker.Auto()) state.Dependency = allocate.ScheduleParallel(state.Dependency);
            using (ApplyMarker.Auto()) state.Dependency = apply.ScheduleParallel(functionals, state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { state.CompleteDependency(); if (grants.IsCreated) grants.Dispose(); }

        [BurstCompile] struct ClearGrantsJob : IJob { public NativeList<EnergyGrantRequest> Grants; public void Execute() => Grants.Clear(); }

        [BurstCompile]
        struct ApplyGrantsJob : IJob
        {
            public NativeList<EnergyGrantRequest> Grants;
            public ComponentLookup<ShipPower> Powers;
            public void Execute()
            {
                Grants.AsArray().Sort(new GrantOrder());
                foreach (var g in Grants)
                {
                    if (!Powers.HasComponent(g.Ship) || !(g.Energy > 0) || !Unity.Mathematics.math.isfinite(g.Energy)) continue;
                    var power = Powers[g.Ship];
                    if ((power.Flags & ShipPowerFlags.Initialized) == 0 || (power.Flags & ShipPowerFlags.Dead) != 0) continue;
                    power.Stored = Unity.Mathematics.math.min(power.Capacity, power.Stored + g.Energy);
                    Powers[g.Ship] = power;
                }
            }
        }
        struct GrantOrder : System.Collections.Generic.IComparer<EnergyGrantRequest>
        {
            public int Compare(EnergyGrantRequest a, EnergyGrantRequest b)
            {
                int c = a.Ship.Index.CompareTo(b.Ship.Index); if (c != 0) return c;
                c = ((byte)a.Producer).CompareTo((byte)b.Producer); return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
            }
        }
    }
}
