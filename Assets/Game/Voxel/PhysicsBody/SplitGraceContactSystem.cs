using Game.Core.Components;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Suppresses split-piece contacts with their parent until the grace tick (SPEC-12 §5.12).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateBodyPairsGroup))]
    [UpdateBefore(typeof(PhysicsCreateContactsGroup))]
    public partial struct SplitGraceContactSystem : ISystem
    {
        ComponentLookup<SplitGrace> grace;
        EntityQuery graceQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            grace = state.GetComponentLookup<SplitGrace>(true);
            graceQuery = SystemAPI.QueryBuilder().WithAll<SplitGrace>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (graceQuery.IsEmptyIgnoreFilter) return;
            grace.Update(ref state);
            var job = new SplitGraceBodyPairsJob
            {
                Grace = grace,
                Tick = SystemAPI.GetSingleton<SimClock>().Tick
            };
            // Unity Physics' in-simulation scheduling pattern, confirmed by PHY-002.
            state.Dependency = job.Schedule(SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW,
                ref SystemAPI.GetSingletonRW<PhysicsWorldSingleton>().ValueRW.PhysicsWorld, state.Dependency);
        }
    }
}
