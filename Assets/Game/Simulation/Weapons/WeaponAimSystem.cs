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
    // TODO(WPN-006): target lead/autofire and full aim tests.
    /// <summary>SPEC-21 §5.4 manual mount aim. Target-lead/autofire remain WPN-005/006.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct WeaponAimSystem : ISystem
    {
        AimJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Transforms = state.GetComponentLookup<LocalTransform>(true); job.Commands = state.GetComponentLookup<ShipCommand>(true);
            job.Hulks = state.GetComponentLookup<Hulk>(true); job.Spawning = state.GetComponentLookup<GridSpawning>(true); job.Dormant = state.GetComponentLookup<Dormant>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<WeaponTuning>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Transforms.Update(ref state); job.Commands.Update(ref state); job.Hulks.Update(ref state); job.Spawning.Update(ref state); job.Dormant.Update(ref state);
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value; job.Convergence = SystemAPI.GetSingleton<WeaponTuning>().FixedConvergenceRad;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct AimJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<ShipCommand> Commands;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            [ReadOnly] public ComponentLookup<Dormant> Dormant;
            public BlobAssetReference<WeaponDefsBlob> Weapons; public float Convergence;
            void Execute(in FunctionalBlock functional, in WeaponMount mount, ref WeaponAim aim)
            {
                aim.HasAim = 0; Entity root = functional.Grid;
                if (mount.Initialized == 0 || mount.WeaponIndex == 0 || !Commands.HasComponent(root) || !Transforms.HasComponent(root) || Hulks.HasComponent(root) ||
                    (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) || (Dormant.HasComponent(root) && Dormant.IsComponentEnabled(root))) return;
                var command = Commands[root]; if (command.AimValid == 0) return;
                var transform = Transforms[root]; ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
                float3 muzzle = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                float3 direction = math.normalizesafe(transform.InverseTransformPoint(command.AimWorld) - muzzle, mount.ForwardLocal);
                float3 horizontal = direction - mount.UpLocal * math.dot(direction, mount.UpLocal);
                aim.DesiredYaw = math.atan2(math.dot(math.cross(mount.ForwardLocal, horizontal), mount.UpLocal), math.dot(mount.ForwardLocal, horizontal));
                float elevation = math.clamp(math.asin(math.clamp(math.dot(direction, mount.UpLocal), -1f, 1f)), -weapon.MaxElevationRad, weapon.MaxElevationRad);
                if (weapon.IsFixed != 0)
                {
                    aim.Yaw = math.clamp(aim.DesiredYaw, -Convergence, Convergence);
                    aim.Aligned = (byte)(math.abs(aim.DesiredYaw) <= Convergence + weapon.FireConeRad ? 1 : 0);
                }
                else
                {
                    float target = math.clamp(aim.DesiredYaw, -weapon.TraverseHalfArcRad, weapon.TraverseHalfArcRad);
                    aim.Yaw += math.clamp(target - aim.Yaw, -weapon.TraverseRateRad * SimTime.FixedTimestep, weapon.TraverseRateRad * SimTime.FixedTimestep);
                    aim.Aligned = (byte)(math.abs(aim.DesiredYaw - aim.Yaw) <= weapon.FireConeRad ? 1 : 0);
                }
                aim.FireDirLocal = math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.ForwardLocal) * math.cos(elevation) + mount.UpLocal * math.sin(elevation);
                aim.HasAim = 1; aim.InRange = 1;
            }
        }
    }
}
