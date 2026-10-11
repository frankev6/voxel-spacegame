using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Unity.Burst;
using Unity.Entities;

namespace Game.App.Systems
{
    /// <summary>Advances SimClock once per fixed tick, last in the gameplay pipeline (after the GameplayEnd command buffer), so every
    /// system of tick N reads Tick == N. UniversalTime is recomputed from the tick, never accumulated (SPEC-02 decision 6). Implements
    /// SPEC-01 §5.8.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(GameplayEndCommandBufferSystem))]
    public partial struct SimClockAdvanceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SimClock>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var c = ref SystemAPI.GetSingletonRW<SimClock>().ValueRW;
            c.Tick += 1;
            c.UniversalTime = c.SessionStartUniversalTime + c.Tick * SimTime.FixedTimestepD;
        }
    }
}
