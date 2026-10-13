using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.4 fixed-step manual and target-lead aiming.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct WeaponAimSystem : ISystem
    {
        WeaponAimJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(true);
            job.Transforms = state.GetComponentLookup<LocalTransform>(true); job.Commands = state.GetComponentLookup<ShipCommand>(true);
            job.Hulks = state.GetComponentLookup<Hulk>(true); job.Spawning = state.GetComponentLookup<GridSpawning>(true); job.Dormant = state.GetComponentLookup<Dormant>(true);
            job.Velocities = state.GetComponentLookup<Unity.Physics.PhysicsVelocity>(true);
            job.Controls = state.GetComponentLookup<WeaponControl>(true); job.Targets = state.GetComponentLookup<ShipTarget>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<WeaponTuning>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Transforms.Update(ref state); job.Commands.Update(ref state); job.Hulks.Update(ref state); job.Spawning.Update(ref state); job.Dormant.Update(ref state);
            job.Velocities.Update(ref state); job.Controls.Update(ref state); job.Targets.Update(ref state);
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value; job.Convergence = SystemAPI.GetSingleton<WeaponTuning>().FixedConvergenceRad;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }
}
