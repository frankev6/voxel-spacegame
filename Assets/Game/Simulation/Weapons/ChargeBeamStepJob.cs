using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §4.6d (WPN-019): one writer owns each weapon chunk and its matching stream partition, as BeamStepJob.</summary>
    [BurstCompile]
    internal struct ChargeBeamStepJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<FunctionalBlock> Functionals;
        [ReadOnly] public ComponentTypeHandle<WeaponMount> Mounts;
        [ReadOnly] public ComponentTypeHandle<WeaponAim> Aims;
        public ComponentTypeHandle<ChargeBeamState> Charges;
        public CombatHitTest HitTest;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<Powered> Power;
        [ReadOnly] public ComponentLookup<OwnedBy> Owners;
        [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
        [ReadOnly] public ComponentLookup<PlayerShip> Players;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
        [ReadOnly] public SpatialHashQueryData Hash;
        public NativeStream.Writer Damage, Shields;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        public int MaxCandidates;
        public float Dt;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(Entities); var functionals = chunk.GetNativeArray(ref Functionals);
            var mounts = chunk.GetNativeArray(ref Mounts); var aims = chunk.GetNativeArray(ref Aims); var charges = chunk.GetNativeArray(ref Charges);
            Damage.BeginForEachIndex(unfilteredChunkIndex); Shields.BeginForEachIndex(unfilteredChunkIndex);
            ref var blob = ref Weapons.Value;
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                var beam = charges[i]; if (beam.Phase == ChargeBeamPhase.Idle) continue;
                var mount = mounts[i]; var functional = functionals[i]; Entity root = functional.Grid;
                // Power, core or root loss, or a different weapon under the sequence, ends it at once (as a beam pulse).
                if (!HitTest.Transforms.HasComponent(root) || Hulks.HasComponent(root) ||
                    (Power.HasComponent(entities[i]) && !Power.IsComponentEnabled(entities[i])) || beam.TicksLeft == 0 ||
                    mount.WeaponIndex == 0 || mount.WeaponIndex != beam.Weapon || mount.WeaponIndex >= blob.Weapons.Length ||
                    mount.WeaponIndex >= blob.ChargeBeams.Length)
                { charges[i] = Stopped(beam); continue; }
                ref var weapon = ref blob.Weapons[mount.WeaponIndex]; ref var charge = ref blob.ChargeBeams[mount.WeaponIndex];
                if (weapon.Delivery != WeaponDelivery.ChargeBeam || weapon.BeamPulseTicks == 0 || charge.ChargeTicks == 0)
                { charges[i] = Stopped(beam); continue; }

                var transform = HitTest.Transforms[root]; var aim = aims[i];
                beam.MuzzleLocal = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                float3 muzzle = transform.TransformPoint(beam.MuzzleLocal);
                // The beam chases the weapon's aim (the cursor, or the target under autofire) no faster than the track rate.
                float3 current = math.lengthsq(beam.DirWorld) > 1e-8f ? math.normalize(beam.DirWorld) : math.rotate(transform.Rotation, aim.FireDirLocal);
                if (aim.HasAim != 0)
                    current = RotateToward(current, math.normalizesafe(math.rotate(transform.Rotation, aim.FireDirLocal), current), charge.TrackRateRad * Dt);
                beam.DirWorld = current;

                bool firing = beam.Phase == ChargeBeamPhase.Firing;
                float3 end = muzzle + current * weapon.BeamRange;
                float radius = (firing ? weapon.BeamWidth : charge.ConvergeBeamWidth) * .5f;
                var hit = HitTest.Segment(muzzle, end, radius, root, Hash, MaxCandidates);
                beam.EndWorld = hit.Kind == HitKind.None ? end : hit.WorldPoint;
                beam.HitSomething = hit.Kind == HitKind.None ? (byte)0 : (byte)1;
                beam.Target = hit.Kind == HitKind.Hull || hit.Kind == HitKind.Shield ? hit.Grid : Entity.Null;

                if (firing)
                {
                    float multiplier = Multipliers.HasComponent(root) ? Multipliers[root].Get(StatId.WeaponDamage) : 1;
                    float amount = weapon.Damage * multiplier / weapon.BeamPulseTicks;
                    Entity instigator = Owners.HasComponent(root) ? Owners[root].Owner : root;
                    uint sequence = (uint)functional.InstanceIndex;
                    float3 impulse = current * (weapon.ImpulsePerHit / weapon.BeamPulseTicks);
                    if (hit.Kind == HitKind.Hull || hit.Kind == HitKind.Shield)
                    {
                        var target = HitTest.Transforms[hit.Grid]; float3 localDirection = math.rotate(math.inverse(target.Rotation), current);
                        if (hit.Kind == HitKind.Hull)
                            Damage.Write(new DamageEvent { Grid = hit.Grid, LocalPoint = target.InverseTransformPoint(hit.WorldPoint),
                                LocalDir = localDirection, Amount = amount, Radius = 0, Penetration = weapon.PenetrationCells, Type = weapon.Type,
                                Source = root, Instigator = instigator, WorldImpulse = impulse, Producer = ProducerKind.Beam, Sequence = sequence });
                        else
                            Shields.Write(new ShieldHit { Grid = hit.Grid, LocalPoint = hit.ShieldLocalPoint,
                                LocalDir = localDirection, Amount = amount, Radius = 0, Penetration = weapon.PenetrationCells, Type = weapon.Type,
                                Source = root, Instigator = instigator, WorldImpulse = impulse, Producer = ProducerKind.Beam, Sequence = sequence });
                    }
                    if ((hit.Kind == HitKind.Hull || hit.Kind == HitKind.Celestial) && beam.TicksLeft % 6 == 0)
                    {
                        var effect = FxEvents.Hit(weapon.Type == DamageType.Energy ? FxKind.HitEnergy : FxKind.HitKinetic,
                            hit.WorldPoint, hit.WorldNormal, Velocities.HasComponent(hit.Grid) ? Velocities[hit.Grid].Linear : default, amount, weapon.HitFx);
                        if (Players.HasComponent(root)) effect.Flags |= FxEventFlags.PlayerSource;
                        Fx.Enqueue(effect);
                    }
                }

                beam.TicksLeft--;
                if (beam.TicksLeft == 0)
                {
                    if (firing) beam = Stopped(beam);
                    else
                    {
                        // Converged: the main beam fires from the next tick for the pulse length.
                        ushort ticks = (ushort)math.min(weapon.BeamPulseTicks, ushort.MaxValue);
                        beam.Phase = ChargeBeamPhase.Firing; beam.TicksLeft = ticks; beam.PhaseTicks = ticks;
                        // The discharge: its own sound and a big muzzle flash (the weapon's fireCue already played at the trigger).
                        bool player = Players.HasComponent(root);
                        if (charge.FireCue.Value != 0) Sfx.Enqueue(SfxEvents.At(charge.FireCue, muzzle, 1, player));
                        var flash = FxEvents.Muzzle(muzzle, current, Velocities.HasComponent(root) ? Velocities[root].Linear : default,
                            1.5f + weapon.BeamWidth, weapon.TracerColor, weapon.MuzzleFx);
                        if (player) flash.Flags |= FxEventFlags.PlayerSource;
                        Fx.Enqueue(flash);
                    }
                }
                charges[i] = beam;
            }
            Damage.EndForEachIndex(); Shields.EndForEachIndex();
        }

        static ChargeBeamState Stopped(ChargeBeamState beam)
        {
            beam.Phase = ChargeBeamPhase.Idle; beam.TicksLeft = 0; beam.PhaseTicks = 0; beam.HitSomething = 0; beam.Target = Entity.Null;
            return beam;
        }

        /// <summary>Turns unit <paramref name="from"/> toward unit <paramref name="to"/> by at most <paramref name="maxAngle"/> radians.</summary>
        internal static float3 RotateToward(float3 from, float3 to, float maxAngle)
        {
            float angle = math.acos(math.clamp(math.dot(from, to), -1f, 1f));
            if (angle <= maxAngle || angle < 1e-6f) return to;
            float3 axis = math.cross(from, to);
            // Opposite directions: any perpendicular axis turns the beam round.
            if (math.lengthsq(axis) < 1e-10f) axis = math.cross(from, math.abs(from.y) < .9f ? new float3(0, 1, 0) : new float3(1, 0, 0));
            return math.normalize(math.rotate(quaternion.AxisAngle(math.normalize(axis), maxAngle), from));
        }
    }
}
