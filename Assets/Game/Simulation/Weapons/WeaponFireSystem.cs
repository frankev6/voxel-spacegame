using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Simulation.Commands;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Game.Voxel.PresentationStreams;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    // TODO(WPN-007): parallel stream/flatten pipeline, power integration, beam delivery/pulses and full firing contract tests.
    /// <summary>SPEC-21 §5.5 delivery-agnostic fire. ADR-0053 initially schedules one ordered mount job.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(WeaponAimSystem)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct WeaponFireSystem : ISystem, ISystemStartStop
    {
        WeaponShots owned; EntityQuery query; FireJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            owned = new WeaponShots { Shots = new NativeList<WeaponShot>(128, Allocator.Persistent), Access = GuardedHandles.Create() };
            state.EntityManager.CreateSingleton(owned);
            query = new EntityQueryBuilder(Allocator.Temp).WithAll<FunctionalBlock, WeaponMount, WeaponAim, WeaponState>().Build(ref state);
            job.Functionals = state.GetComponentLookup<FunctionalBlock>(true); job.Mounts = state.GetComponentLookup<WeaponMount>(true);
            job.Aims = state.GetComponentLookup<WeaponAim>(true); job.States = state.GetComponentLookup<WeaponState>();
            job.Commands = state.GetComponentLookup<ShipCommand>(true); job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true); job.Owners = state.GetComponentLookup<OwnedBy>(true);
            job.Players = state.GetComponentLookup<PlayerShip>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); owned.Access.Dispose(); owned.Shots.Dispose(); }
        [BurstCompile] public void OnStartRunning(ref SystemState state) { owned.Access.CompleteAccess(); owned.Shots.Clear(); }
        [BurstCompile] public void OnStopRunning(ref SystemState state) { owned.Access.CompleteAccess(); owned.Shots.Clear(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Functionals.Update(ref state); job.Mounts.Update(ref state); job.Aims.Update(ref state); job.States.Update(ref state);
            job.Commands.Update(ref state); job.Transforms.Update(ref state); job.Velocities.Update(ref state); job.Owners.Update(ref state); job.Players.Update(ref state);
            job.Entities = query.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var gather);
            job.Shots = owned.Shots; job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value; job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var fx = SystemAPI.GetSingleton<FxEventQueue>(); job.Fx = fx.Queue.AsParallelWriter();
            state.Dependency = job.Schedule(fx.Access.BeginRead(owned.Access.Acquire(JobHandle.CombineDependencies(state.Dependency, gather))));
            owned.Access.Release(state.Dependency); fx.Access.EndRead(state.Dependency);
        }
        [BurstCompile] struct FireJob : IJob
        {
            public NativeList<Entity> Entities;
            [ReadOnly] public ComponentLookup<FunctionalBlock> Functionals;
            [ReadOnly] public ComponentLookup<WeaponMount> Mounts;
            [ReadOnly] public ComponentLookup<WeaponAim> Aims;
            public ComponentLookup<WeaponState> States;
            [ReadOnly] public ComponentLookup<ShipCommand> Commands;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<OwnedBy> Owners;
            [ReadOnly] public ComponentLookup<PlayerShip> Players;
            public NativeList<WeaponShot> Shots;
            [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
            public BlobAssetReference<WeaponDefsBlob> Weapons; public uint Tick;
            public void Execute()
            {
                Shots.Clear(); Entities.Sort(new EntityOrder());
                foreach (var entity in Entities)
                {
                    var state = States[entity]; state.Firing = 0; var mount = Mounts[entity]; var aim = Aims[entity]; var functional = Functionals[entity];
                    Entity root = functional.Grid;
                    if (mount.WeaponIndex == 0 || aim.HasAim == 0 || !Commands.HasComponent(root)) { States[entity] = state; continue; }
                    ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
                    state.CooldownRemainingTicks = math.max(0, state.CooldownRemainingTicks - 1); state.CooldownTicks = weapon.TriggerIntervalTicks;
                    if (state.RoundsLeftInBurst > 0) state.BurstRemainingTicks--;
                    bool held = (Commands[root].FireGroups & state.Group) != 0;
                    if (held && (aim.Aligned != 0 || weapon.IsFixed != 0) && state.RoundsLeftInBurst == 0 && state.CooldownRemainingTicks <= 0)
                    { state.CooldownRemainingTicks += state.CooldownTicks; state.RoundsLeftInBurst = (ushort)weapon.BurstCount; state.BurstRemainingTicks = 0; }
                    while (state.RoundsLeftInBurst > 0 && state.BurstRemainingTicks <= 0)
                    {
                        var transform = Transforms[root]; state.ShotCounter++;
                        uint shot = ((uint)functional.InstanceIndex & 0xFFFF) << 16 | (state.ShotCounter & 0xFFFF);
                        var random = Unity.Mathematics.Random.CreateFromIndex((uint)StableHash.Combine(Tick, ((ulong)(uint)root.Index << 32) | shot));
                        float3 forward = aim.FireDirLocal, side = math.normalizesafe(math.cross(mount.UpLocal, forward), new float3(1, 0, 0));
                        float3 up = math.cross(forward, side);
                        float3 muzzle = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                        for (int pellet = 0; pellet < weapon.PelletsPerRound; pellet++)
                        {
                            float angle = weapon.SpreadRad * math.sqrt(random.NextFloat()), azimuth = random.NextFloat(0, 2 * math.PI);
                            float3 direction = forward * math.cos(angle) + (side * math.cos(azimuth) + up * math.sin(azimuth)) * math.sin(angle);
                            Shots.Add(new WeaponShot { Weapon = entity, Grid = root, Instigator = Owners.HasComponent(root) ? Owners[root].Owner : root,
                                MuzzleWorld = transform.TransformPoint(muzzle), DirWorld = math.rotate(transform.Rotation, direction),
                                ShooterVelocity = Velocities.HasComponent(root) ? Velocities[root].Linear : default, ShotId = shot + (uint)pellet,
                                Def = mount.WeaponIndex, Delivery = weapon.Delivery, DamageMultiplier = 1,
                                Flags = (Players.HasComponent(root) ? ProjectileFlags.PlayerSource : ProjectileFlags.None) |
                                    (weapon.ExplodeOnExpire != 0 ? ProjectileFlags.ExplodeOnExpire : ProjectileFlags.None) });
                        }
                        state.ShotCounter += (uint)(weapon.PelletsPerRound - 1); state.RoundsLeftInBurst--; state.BurstRemainingTicks += weapon.BurstIntervalTicks; state.Firing = 1;
                    }
                    if (state.Firing != 0)
                    {
                        var transform = Transforms[root];
                        float3 muzzle = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                        var effect = FxEvents.Muzzle(transform.TransformPoint(muzzle), math.rotate(transform.Rotation, aim.FireDirLocal),
                            Velocities.HasComponent(root) ? Velocities[root].Linear : default, math.max(.01f, weapon.ProjectileRadius * 2), weapon.TracerColor, weapon.MuzzleFx);
                        if (Players.HasComponent(root)) effect.Flags |= FxEventFlags.PlayerSource;
                        Fx.Enqueue(effect);
                    }
                    States[entity] = state;
                }
            }
            struct EntityOrder : System.Collections.Generic.IComparer<Entity> { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
        }
    }
}
