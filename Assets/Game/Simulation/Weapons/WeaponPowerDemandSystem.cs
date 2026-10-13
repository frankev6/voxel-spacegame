using Game.Core.Components;
using Game.Data.Blocks;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.12: weapon-owned current-tick power demand, independent of shot/cooldown state.</summary>
    [BurstCompile, UpdateInGroup(typeof(PowerDemandGroup))]
    public partial struct WeaponPowerDemandSystem : ISystem
    {
        DemandJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>();
            job.Commands = state.GetComponentLookup<ShipCommand>(true);
            job.Controls = state.GetComponentLookup<WeaponControl>(true);
            job.Targets = state.GetComponentLookup<ShipTarget>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Commands.Update(ref state); job.Controls.Update(ref state); job.Targets.Update(ref state);
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct DemandJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ShipCommand> Commands;
            [ReadOnly] public ComponentLookup<WeaponControl> Controls;
            [ReadOnly] public ComponentLookup<ShipTarget> Targets;
            void Execute(in FunctionalBlock functional, in WeaponMount mount, in WeaponState weapon, DynamicBuffer<PowerRequest> requests)
            {
                Entity root = functional.Grid;
                var command = Commands.HasComponent(root) ? Commands[root] : default;
                bool held = (command.FireGroups & weapon.Group) != 0;
                bool auto = Controls.HasComponent(root) && Controls[root].Autofire == 1 &&
                    Targets.HasComponent(root) && Targets[root].Value != Entity.Null;
                PowerRegistration.SetRequesting(requests, BlockModuleId.WeaponMount, mount.WeaponIndex != 0 && (held || auto));
            }
        }
    }
}
