using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
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
            [ReadOnly] public ComponentLookup<Unity.Physics.PhysicsMass> Masses;
            [ReadOnly] public ComponentLookup<WeaponControl> Controls;
            [ReadOnly] public ComponentLookup<ShipTarget> Targets;
            [ReadOnly] public SpatialHashQueryData Hash; public byte HasHash;
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
                    // QA-001: aim at the live centre of mass. Spawn bounds never shrink, so once the top of a hull was
                    // shot away their centre floated above what was left and every later shot skimmed over the ship.
                    if (Masses.HasComponent(target))
                        point = targetPose.TransformPoint(Masses[target].CenterOfMass);
                    else if (Grids.HasComponent(target))
                    {
                        var grid = Grids[target];
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
                    var command = Commands[root];
                    point = command.AimWorld;
                    aim.InRange = 1;
                    // Auto-aim: when the aim point is on a ship, lead that ship like a locked target (the input sampler snaps a
                    // near miss onto the hull). The point stays where the player put it, so pointing at an engine still hits the engine.
                    if ((command.Flags & ShipCommandFlags.AimAssist) != 0 && weapon.Delivery == Game.Data.Definitions.WeaponDelivery.Projectile
                        && AssistVelocity(root, point, out float3 targetVelocity))
                        point = LeadMath.Point(muzzleWorld, Velocities.HasComponent(root) ? Velocities[root].Linear : float3.zero,
                            point, targetVelocity, weapon.ProjectileSpeed, weapon.LifetimeMaxTicks * SimTime.FixedTimestep);
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
                    /// <summary>Velocity of the ship or station whose hull sphere holds the aim point (the smallest relative distance wins).</summary>
            bool AssistVelocity(Entity self, float3 point, out float3 velocity)
            {
                velocity = default;
                if (HasHash == 0) return false;
                var candidates = new FixedList512Bytes<int>();
                int total = Hash.QuerySphere(point, AssistMarginM, ref candidates);
                bool overflow = total > candidates.Capacity;
                int count = overflow ? Hash.Proxies.Length : candidates.Length;
                float best = float.MaxValue; bool found = false;
                for (int i = 0; i < count; i++)
                {
                    var proxy = Hash.Get(overflow ? i : candidates[i]);
                    if (proxy.Entity == self || (proxy.Kind != ProxyKind.Ship && proxy.Kind != ProxyKind.Station) || Hulks.HasComponent(proxy.Entity)) continue;
                    float reach = proxy.HullRadius + AssistMarginM, distance = math.distance(point, proxy.Center);
                    if (!(distance <= reach) || distance / reach >= best) continue;
                    best = distance / reach; velocity = proxy.Velocity; found = true;
                }
                return found;
            }
            const float AssistMarginM = 2f;
        }
}
