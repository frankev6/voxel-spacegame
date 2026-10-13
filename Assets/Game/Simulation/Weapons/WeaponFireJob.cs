using Game.Core;
using Game.Core.Components;
using Game.Data.Components;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.5: each chunk owns one ordered shot-stream lane and its weapon states.</summary>
    [BurstCompile] internal struct WeaponFireJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<FunctionalBlock> Functionals;
        [ReadOnly] public ComponentTypeHandle<WeaponMount> Mounts;
        [ReadOnly] public ComponentTypeHandle<WeaponAim> Aims;
        public ComponentTypeHandle<WeaponState> States;
        [ReadOnly] public ComponentLookup<ShipCommand> Commands;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<OwnedBy> Owners;
        [ReadOnly] public ComponentLookup<PlayerShip> Players;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [ReadOnly] public ComponentLookup<WeaponControl> Controls;
        [ReadOnly] public ComponentLookup<ShipTarget> Targets;
        [ReadOnly] public ComponentLookup<Allegiance> Allegiances;
        [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
        [ReadOnly] public ComponentLookup<Powered> Power;
        [ReadOnly] public ComponentLookup<BeamState> Beams;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<Dormant> Dormant;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public FactionRelations Relations;
        [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
        public NativeStream.Writer Shots;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        public uint Tick;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(Entities); var functionals = chunk.GetNativeArray(ref Functionals);
            var mounts = chunk.GetNativeArray(ref Mounts); var aims = chunk.GetNativeArray(ref Aims); var states = chunk.GetNativeArray(ref States);
            Shots.BeginForEachIndex(unfilteredChunkIndex);
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                var state = states[i]; state.Firing = 0; var mount = mounts[i]; var aim = aims[i]; var functional = functionals[i];
                Entity root = functional.Grid, entity = entities[i];
                if (mount.Initialized == 0 || mount.WeaponIndex == 0 || mount.WeaponIndex >= Weapons.Value.Weapons.Length ||
                    !Transforms.HasComponent(root) || aim.HasAim == 0 || Hulks.HasComponent(root) ||
                    (Dormant.HasComponent(root) && Dormant.IsComponentEnabled(root)) || (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)))
                { states[i] = state; continue; }
                ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
                float rate = Multipliers.HasComponent(root) ? Multipliers[root].Get(StatId.FireRate) : 1;
                state.CooldownTicks = weapon.TriggerIntervalTicks / math.max(.0001f, rate);
                if (state.CooldownRemainingTicks > 0) state.CooldownRemainingTicks -= 1;
                if (state.RoundsLeftInBurst > 0) state.BurstRemainingTicks -= 1;
                var command = Commands.HasComponent(root) ? Commands[root] : default;
                Entity target = Targets.HasComponent(root) ? Targets[root].Value : Entity.Null;
                bool held = command.AimValid != 0 && (command.FireGroups & state.Group) != 0;
                bool automatic = Controls.HasComponent(root) && Controls[root].Autofire != 0 && aim.InRange != 0 &&
                    Transforms.HasComponent(target) && !Hulks.HasComponent(target) && Allegiances.HasComponent(root) && Allegiances.HasComponent(target) &&
                    Relations.IsHostile(Allegiances[root], Allegiances[target]);
                bool wants = (held || automatic) && (aim.Aligned != 0 || (weapon.IsFixed != 0 && held));
                bool powered = !Power.HasComponent(entity) || Power.IsComponentEnabled(entity);
                bool beamActive = weapon.Delivery == WeaponDelivery.Beam && Beams.HasComponent(entity) && Beams[entity].Active != 0;
                if (!wants || !powered || beamActive) state.CooldownRemainingTicks = math.max(0, state.CooldownRemainingTicks);
                if (wants && powered && !beamActive && state.RoundsLeftInBurst == 0 && state.CooldownRemainingTicks <= 0)
                {
                    // Preserve fractional overshoot only while actively firing; idle time never banks triggers.
                    state.CooldownRemainingTicks = math.max(-1, state.CooldownRemainingTicks) + state.CooldownTicks;
                    state.RoundsLeftInBurst = (ushort)weapon.BurstCount; state.BurstRemainingTicks = 0;
                }
                var transform = Transforms[root];
                float3 muzzle = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                float3 worldMuzzle = transform.TransformPoint(muzzle);
                float3 velocity = Velocities.HasComponent(root) ? Velocities[root].Linear : default;
                while (state.RoundsLeftInBurst > 0 && state.BurstRemainingTicks <= 0 && powered)
                {
                    state.ShotCounter++;
                    uint shotId = ((uint)functional.InstanceIndex & 0xFFFF) << 16 | (state.ShotCounter & 0xFFFF);
                    float3 forward = aim.FireDirLocal, side = math.normalizesafe(math.cross(mount.UpLocal, forward), new float3(1,0,0));
                    float3 up = math.cross(forward, side);
                    int pellets = weapon.Delivery == WeaponDelivery.Beam ? 1 : weapon.PelletsPerRound;
                    var flags = (Players.HasComponent(root) ? ProjectileFlags.PlayerSource : 0) |
                        (weapon.HomingTurnRateRad > 0 ? ProjectileFlags.Homing : 0) |
                        (weapon.ExplodeOnExpire != 0 ? ProjectileFlags.ExplodeOnExpire : 0);
                    for (int pellet = 0; pellet < pellets; pellet++)
                    {
                        var random = WeaponRandom.Create(Tick, Ids.HasComponent(root) ? Ids[root].Value : 0, shotId + (uint)pellet);
                        float angle = weapon.SpreadRad * math.sqrt(random.NextFloat()), azimuth = random.NextFloat(0, 2 * math.PI);
                        float3 direction = forward * math.cos(angle) + (side * math.cos(azimuth) + up * math.sin(azimuth)) * math.sin(angle);
                        Shots.Write(new WeaponShot { Weapon = entity, Grid = root, Instigator = Owners.HasComponent(root) ? Owners[root].Owner : root,
                            Target = (flags & ProjectileFlags.Homing) != 0 ? target : Entity.Null, MuzzleWorld = worldMuzzle,
                            DirWorld = math.rotate(transform.Rotation, direction), ShooterVelocity = velocity, ShotId = shotId + (uint)pellet,
                            Def = mount.WeaponIndex, Delivery = weapon.Delivery, Flags = flags,
                            DamageMultiplier = Multipliers.HasComponent(root) ? Multipliers[root].Get(StatId.WeaponDamage) : 1 });
                    }
                    state.ShotCounter += (uint)(pellets - 1); state.RoundsLeftInBurst--; state.BurstRemainingTicks += weapon.BurstIntervalTicks; state.Firing = 1;
                    var effect = FxEvents.Muzzle(worldMuzzle, math.rotate(transform.Rotation, forward), velocity,
                        .25f + .05f * math.sqrt(weapon.Damage), weapon.TracerColor, weapon.MuzzleFx);
                    if (Players.HasComponent(root)) effect.Flags |= FxEventFlags.PlayerSource;
                    Fx.Enqueue(effect);
                    if (weapon.FireCue.Value != 0) Sfx.Enqueue(SfxEvents.At(weapon.FireCue, worldMuzzle, 1, Players.HasComponent(root)));
                }
                states[i] = state;
            }
            Shots.EndForEachIndex();
        }
    }
    internal static class WeaponRandom
    {
        internal static Random Create(uint tick, ulong persistentId, uint shotId) => Random.CreateFromIndex((uint)StableHash.Combine(
            StableHash.Combine(StableHash.Combine(tick, persistentId), shotId), (ulong)RngPurpose.WeaponSpread));
    }
}
