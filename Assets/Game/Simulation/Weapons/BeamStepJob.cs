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
    /// <summary>SPEC-21 §5.7: one writer owns each weapon chunk and matching stream partition.</summary>
    [BurstCompile]
    internal struct BeamStepJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<FunctionalBlock> Functionals;
        [ReadOnly] public ComponentTypeHandle<WeaponMount> Mounts;
        [ReadOnly] public ComponentTypeHandle<WeaponAim> Aims;
        public ComponentTypeHandle<BeamState> Beams;
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
        public int MaxCandidates;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(Entities); var functionals = chunk.GetNativeArray(ref Functionals);
            var mounts = chunk.GetNativeArray(ref Mounts); var aims = chunk.GetNativeArray(ref Aims); var beams = chunk.GetNativeArray(ref Beams);
            // IJobChunk assigns one chunk and its unfiltered index to one worker; both stream lanes are exclusive to that worker.
            Damage.BeginForEachIndex(unfilteredChunkIndex); Shields.BeginForEachIndex(unfilteredChunkIndex);
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                var beam = beams[i]; if (beam.Active == 0) continue;
                var mount = mounts[i]; var functional = functionals[i]; Entity root = functional.Grid;
                if (!HitTest.Transforms.HasComponent(root) || Hulks.HasComponent(root) ||
                    (Power.HasComponent(entities[i]) && !Power.IsComponentEnabled(entities[i])) || beam.TicksLeft == 0 ||
                    mount.WeaponIndex == 0 || mount.WeaponIndex >= Weapons.Value.Weapons.Length)
                { Stop(ref beam); beams[i] = beam; continue; }
                ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
                if (weapon.Delivery != WeaponDelivery.Beam || weapon.BeamPulseTicks == 0)
                { Stop(ref beam); beams[i] = beam; continue; }
                var transform = HitTest.Transforms[root]; var aim = aims[i];
                beam.MuzzleLocal = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                float3 muzzle = transform.TransformPoint(beam.MuzzleLocal), direction = math.rotate(transform.Rotation, aim.FireDirLocal);
                float3 end = muzzle + direction * weapon.BeamRange;
                var hit = HitTest.Segment(muzzle, end, weapon.BeamWidth * .5f, root, Hash, MaxCandidates);
                beam.EndWorld = hit.Kind == HitKind.None ? end : hit.WorldPoint;
                beam.HitSomething = hit.Kind == HitKind.None ? (byte)0 : (byte)1;
                float multiplier = Multipliers.HasComponent(root) ? Multipliers[root].Get(StatId.WeaponDamage) : 1;
                float amount = weapon.Damage * multiplier / weapon.BeamPulseTicks;
                Entity instigator = Owners.HasComponent(root) ? Owners[root].Owner : root;
                uint sequence = (uint)functional.InstanceIndex;
                float3 impulse = direction * (weapon.ImpulsePerHit / weapon.BeamPulseTicks);
                if (hit.Kind == HitKind.Hull || hit.Kind == HitKind.Shield)
                {
                    var target = HitTest.Transforms[hit.Grid]; float3 localDirection = math.rotate(math.inverse(target.Rotation), direction);
                    if (hit.Kind == HitKind.Hull)
                        Damage.Write(new DamageEvent { Grid = hit.Grid, LocalPoint = target.InverseTransformPoint(hit.WorldPoint),
                            LocalDir = localDirection, Amount = amount, Radius = 0, Penetration = weapon.PenetrationCells, Type = weapon.Type,
                            Source = root, Instigator = instigator, WorldImpulse = impulse, Producer = ProducerKind.Beam, Sequence = sequence });
                    else
                        Shields.Write(new ShieldHit { Grid = hit.Grid, LocalPoint = hit.ShieldLocalPoint,
                            LocalDir = localDirection, Amount = amount, Radius = 0, Penetration = weapon.PenetrationCells, Type = weapon.Type,
                            Source = root, Instigator = instigator, WorldImpulse = impulse, Producer = ProducerKind.Beam, Sequence = sequence });
                }
                // ShieldSystem owns ripple effects; hull/celestial sparks use the exact first-hit surface and target velocity.
                if ((hit.Kind == HitKind.Hull || hit.Kind == HitKind.Celestial) && beam.TicksLeft % 6 == 0)
                {
                    var effect = FxEvents.Hit(weapon.Type == DamageType.Energy ? FxKind.HitEnergy : FxKind.HitKinetic,
                        hit.WorldPoint, hit.WorldNormal, Velocities.HasComponent(hit.Grid) ? Velocities[hit.Grid].Linear : default, amount, weapon.HitFx);
                    if (Players.HasComponent(root)) effect.Flags |= FxEventFlags.PlayerSource;
                    Fx.Enqueue(effect);
                }
                beam.TicksLeft--; if (beam.TicksLeft == 0) beam.Active = 0;
                beams[i] = beam;
            }
            Damage.EndForEachIndex(); Shields.EndForEachIndex();
        }
        static void Stop(ref BeamState beam) { beam.Active = 0; beam.TicksLeft = 0; beam.HitSomething = 0; }
    }
}
