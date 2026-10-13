using Game.Core;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.6: each 64-row lane owns its rows, stream partitions and counters.</summary>
    [BurstCompile]
    internal struct ProjectileStepJob : IJobParallelForBatch
    {
        [NativeDisableParallelForRestriction] public NativeList<ProjectileRow> Rows;
        [NativeDisableParallelForRestriction] public NativeArray<int2> BatchCounts;
        public CombatHitTest HitTest;
        [ReadOnly] public SpatialHashQueryData Hash;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
        public NativeStream.Writer Damage, Explosions, Shields;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        public CombatAudioCues Cues;
        public uint Tick;
        public int MaxCandidates;

        public void Execute(int startIndex, int count)
        {
            for (int first = startIndex; first < startIndex + count; first += 64)
            {
                int lane = first / 64;
                Damage.BeginForEachIndex(lane); Explosions.BeginForEachIndex(lane); Shields.BeginForEachIndex(lane);
                int2 counts = default;
                int end = math.min(math.min(first + 64, startIndex + count), Rows.Length);
                for (int i = first; i < end; i++)
                {
                    var row = Rows[i];
                    if ((row.Flags & ProjectileFlags.Dead) != 0) continue;
                    ref var weapon = ref Weapons.Value.Weapons[row.Weapon];
                    if ((row.Flags & ProjectileFlags.Homing) != 0 && HitTest.Transforms.HasComponent(row.Target))
                    {
                        int targetIndex = Hash.FindIndex(row.Target);
                        if (targetIndex >= 0)
                            row.Velocity = TurnTowards(row.Velocity, Hash.Proxies[targetIndex].Center - row.Position,
                                weapon.HomingTurnRateRad * SimTime.FixedTimestep);
                    }
                    float3 next = row.Position + row.Velocity * SimTime.FixedTimestep;
                    float3 direction = math.normalizesafe(row.Velocity);
                    var hit = HitTest.Segment(row.Position, next, weapon.ProjectileRadius, row.Source, Hash, MaxCandidates);
                    if (hit.Kind == HitKind.Hull || hit.Kind == HitKind.Shield)
                    {
                        var transform = HitTest.Transforms[hit.Grid];
                        float3 localDirection = math.rotate(math.inverse(transform.Rotation), direction);
                        if (hit.Kind == HitKind.Hull)
                        {
                            if (weapon.Damage > 0)
                                Damage.Write(new DamageEvent { Grid = hit.Grid, LocalPoint = transform.InverseTransformPoint(hit.WorldPoint),
                                    LocalDir = localDirection, Amount = weapon.Damage * row.DamageMultiplier, Radius = 0,
                                    Penetration = weapon.PenetrationCells, Type = row.Type, Source = row.Source, Instigator = row.Instigator,
                                    WorldImpulse = direction * weapon.ImpulsePerHit, Producer = ProducerKind.Projectile, Sequence = row.ShotId });
                            HitEffect(row, weapon, hit, direction);
                            var cue = row.Type == DamageType.Energy ? Cues.ImpactEnergy
                                : row.Type == DamageType.Explosive ? Cues.ImpactExplosive : Cues.ImpactKinetic;
                            Sfx.Enqueue(new SfxEvent { Cue = cue, Pos = hit.WorldPoint,
                                Volume = math.clamp(weapon.Damage / 200f, .3f, 1f),
                                Flags = (row.Flags & ProjectileFlags.PlayerSource) != 0 ? SfxEventFlags.PlayerSource : SfxEventFlags.None });
                            counts.x++;
                        }
                        else
                            Shields.Write(new ShieldHit { Grid = hit.Grid, LocalPoint = hit.ShieldLocalPoint,
                                LocalDir = localDirection, Amount = weapon.Damage * row.DamageMultiplier, Type = row.Type,
                                Penetration = weapon.PenetrationCells, Radius = 0, Source = row.Source, Instigator = row.Instigator,
                                WorldImpulse = direction * weapon.ImpulsePerHit, Producer = ProducerKind.Projectile, Sequence = row.ShotId });
                        Explode(row, weapon, hit.WorldPoint);
                        row.Flags |= ProjectileFlags.Dead;
                    }
                    else if (hit.Kind == HitKind.Celestial)
                    {
                        HitEffect(row, weapon, hit, direction);
                        row.Flags |= ProjectileFlags.Dead;
                    }
                    else
                    {
                        row.Position = next;
                        if (Tick >= row.DeathTick)
                        {
                            if ((row.Flags & ProjectileFlags.ExplodeOnExpire) != 0) Explode(row, weapon, next);
                            row.Flags |= ProjectileFlags.Dead; counts.y++;
                        }
                    }
                    Rows[i] = row;
                }
                BatchCounts[lane] = counts;
                Damage.EndForEachIndex(); Explosions.EndForEachIndex(); Shields.EndForEachIndex();
            }
        }

        internal static float3 TurnTowards(float3 velocity, float3 offset, float maxAngle)
        {
            float speed = math.length(velocity);
            if (speed <= 1e-8f || math.lengthsq(offset) <= 1e-12f || maxAngle <= 0) return velocity;
            float3 forward = velocity / speed, target = math.normalize(offset);
            float angle = math.acos(math.clamp(math.dot(forward, target), -1f, 1f));
            if (angle <= maxAngle) return target * speed;
            float3 axis = math.cross(forward, target);
            // Antiparallel directions have no cross axis: choose a deterministic perpendicular.
            if (math.lengthsq(axis) < 1e-12f)
                axis = math.cross(forward, math.abs(forward.y) < .9f ? new float3(0, 1, 0) : new float3(1, 0, 0));
            return math.rotate(quaternion.AxisAngle(math.normalize(axis), maxAngle), forward) * speed;
        }

        void HitEffect(in ProjectileRow row, in WeaponDef weapon, in CombatHit hit, float3 direction)
        {
            var effect = FxEvents.Hit(row.Type == DamageType.Energy ? FxKind.HitEnergy : FxKind.HitKinetic,
                hit.WorldPoint, -direction, Velocities.HasComponent(hit.Grid) ? Velocities[hit.Grid].Linear : default,
                weapon.Damage, weapon.HitFx);
            if ((row.Flags & ProjectileFlags.PlayerSource) != 0) effect.Flags |= FxEventFlags.PlayerSource;
            Fx.Enqueue(effect);
        }
        void Explode(in ProjectileRow row, in WeaponDef weapon, float3 position)
        {
            if (weapon.ExplosionRadius <= 0) return;
            Explosions.Write(new ExplosionRequest { WorldPos = position, Radius = weapon.ExplosionRadius,
                Amount = weapon.ExplosionDamage * row.DamageMultiplier, Type = DamageType.Explosive,
                Source = row.Source, Instigator = row.Instigator, Producer = ProducerKind.Projectile, Sequence = row.ShotId });
        }
    }
}
