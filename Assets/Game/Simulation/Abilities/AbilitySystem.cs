using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Abilities
{
    /// <summary>SPEC-24 §5.1: root-exclusive slot resync and activation (ADR-0117 decider, authority only). Timers are
    /// AbilityTimerSystem's and activation feedback AbilityFeedbackSystem's, on every machine.</summary>
    [BurstCompile, CreateAfter(typeof(GridSpawnSystem)), UpdateInGroup(typeof(AbilityActivationGroup))]
    public partial struct AbilitySystem : ISystem
    {
        AbilityJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            AbilityGridSetup.Register(SystemAPI.GetSingletonBuffer<RootComponentRegistration>());
            job.Stats = state.GetComponentLookup<ShipStats>(true); job.Power = state.GetComponentLookup<ShipPower>(true);
            job.Commands = state.GetComponentLookup<ShipCommand>(true); job.Instances = state.GetBufferLookup<BlockInstance>(true);
            job.Hulks = state.GetComponentLookup<Hulk>(true); job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AbilityDefsRef>(); state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Stats.Update(ref state); job.Power.Update(ref state); job.Commands.Update(ref state); job.Instances.Update(ref state);
            job.Hulks.Update(ref state); job.Spawning.Update(ref state);
            job.Definitions = SystemAPI.GetSingleton<AbilityDefsRef>().Value; job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value;
            job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }
}
