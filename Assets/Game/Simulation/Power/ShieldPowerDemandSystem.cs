using Game.Core.Components;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.5b: owns only shield-emitter demand, sampled before allocation.</summary>
    [BurstCompile, UpdateInGroup(typeof(PowerDemandGroup))]
    public partial struct ShieldPowerDemandSystem : ISystem
    {
        ComponentLookup<ShieldControl> controls;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            controls = state.GetComponentLookup<ShieldControl>(true);
            state.RequireForUpdate<GameplayActive>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            controls.Update(ref state);
            state.Dependency = new DemandJob { Controls = controls }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct DemandJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ShieldControl> Controls;
            void Execute(in FunctionalBlock functional, DynamicBuffer<PowerRequest> requests)
            {
                bool requested = Controls.HasComponent(functional.Grid) && Controls[functional.Grid].RequestedOn == 1;
                PowerRegistration.SetRequesting(requests, BlockModuleId.ShieldEmitter, requested);
            }
        }
    }
}
