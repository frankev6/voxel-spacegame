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
    /// <summary>SPEC-21 §5.4 parallel manual and target-lead mount aiming.</summary>
        [BurstCompile] internal partial struct WeaponAimJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<ShipCommand> Commands;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            [ReadOnly] public ComponentLookup<Dormant> Dormant;
            [ReadOnly] public ComponentLookup<Unity.Physics.PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<WeaponControl> Controls;
            [ReadOnly] public ComponentLookup<ShipTarget> Targets;
            public BlobAssetReference<WeaponDefsBlob> Weapons; public float Convergence;
            void Execute(in FunctionalBlock functional, in WeaponMount mount, in WeaponState state, ref WeaponAim aim)
            {
                aim.HasAim = 0; Entity root = functional.Grid;
                if (mount.Initialized == 0 || mount.WeaponIndex == 0 || !Transforms.HasComponent(root) || Hulks.HasComponent(root) ||
                    (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) || (Dormant.HasComponent(root) && Dormant.IsComponentEnabled(root))) return;
                var transform = Transforms[root]; ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
                float3 muzzle = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                float3 muzzleWorld = transform.TransformPoint(muzzle);
                float3 point;
                Entity target = Targets.HasComponent(root) ? Targets[root].Value : Entity.Null;
                if (Controls.HasComponent(root) && Controls[root].AimMode == WeaponAimMode.TargetLead && Transforms.HasComponent(target))
                {
                    var targetPose = Transforms[target];
                    point = targetPose.Position;
                    if (Grids.HasComponent(target))
                    {
                        var grid = Grids[target];
                        // Match SpatialProxy's occupied-bounds center, not the blueprint's arbitrary origin.
                        float3 center = ((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize);
                        point = targetPose.TransformPoint(center);
                    }
                    if (weapon.Delivery == Game.Data.Definitions.WeaponDelivery.Projectile)
                        point = LeadMath.Point(muzzleWorld, Velocities.HasComponent(root) ? Velocities[root].Linear : float3.zero,
                            point, Velocities.HasComponent(target) ? Velocities[target].Linear : float3.zero,
                            weapon.ProjectileSpeed, weapon.LifetimeMaxTicks * SimTime.FixedTimestep);
                    aim.InRange = (byte)(math.distance(muzzleWorld, point) <= weapon.Range ? 1 : 0);
                }
                else if (Commands.HasComponent(root) && Commands[root].AimValid != 0)
                {
                    point = Commands[root].AimWorld;
                    aim.InRange = 1;
                }
                else return;
                float3 direction = math.normalizesafe(transform.InverseTransformPoint(point) - muzzle, mount.ForwardLocal);
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
                    float targetYaw = math.clamp(aim.DesiredYaw, -weapon.TraverseHalfArcRad, weapon.TraverseHalfArcRad);
                    aim.Yaw += math.clamp(targetYaw - aim.Yaw, -weapon.TraverseRateRad * SimTime.FixedTimestep, weapon.TraverseRateRad * SimTime.FixedTimestep);
                    aim.Aligned = (byte)(math.abs(aim.DesiredYaw - aim.Yaw) <= weapon.FireConeRad ? 1 : 0);
                }
                aim.FireDirLocal = math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.ForwardLocal) * math.cos(elevation) + mount.UpLocal * math.sin(elevation);
                aim.HasAim = 1;
            }
        }
}
