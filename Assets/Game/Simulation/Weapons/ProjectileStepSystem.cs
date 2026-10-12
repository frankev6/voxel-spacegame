using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    // TODO(WPN-008): parallel stream/compaction pipeline, homing, shield hits and full projectile contract tests.
    /// <summary>SPEC-21 §5.6 projectiles and lifetime. ADR-0053 uses an ordered grid broad phase for the initial range.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public partial struct ProjectileStepSystem : ISystem, ISystemStartStop
    {
        ProjectileStore owned; EntityQuery grids; StepJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            owned = ProjectileStore.Create(8192); state.EntityManager.CreateSingleton(owned);
            grids = new EntityQueryBuilder(Allocator.Temp).WithAll<VoxelGrid>().Build(ref state); job.HitTest = CombatHitTest.Create(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<DamageStreams>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); owned.Dispose(); }
        [BurstCompile] public void OnStartRunning(ref SystemState state) { owned.CompleteAccess(); owned.Rows.Clear(); owned.Stats.Value = default; owned.AppliedShiftSequence.Value = 0; }
        [BurstCompile] public void OnStopRunning(ref SystemState state) { owned.CompleteAccess(); owned.Rows.Clear(); owned.Stats.Value = default; }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.HitTest.Update(ref state);
            job.Grids = grids.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var gather);
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            job.Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Projectile, 1);
            job.Explosions = streams.Explosions.BeginWrite(ref state, ProducerKind.Projectile, 1);
            job.Rows = owned.Rows; job.Stats = owned.Stats; job.Applied = owned.AppliedShiftSequence;
            job.Shift = SystemAPI.TryGetSingleton<OriginShiftRecord>(out var shift) ? shift : default;
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value; job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = job.Schedule(owned.Acquire(JobHandle.CombineDependencies(state.Dependency, gather)));
            streams.Damage.EndWrite(state.Dependency); streams.Explosions.EndWrite(state.Dependency); owned.Release(state.Dependency);
        }
        [BurstCompile] struct StepJob : IJob
        {
            public CombatHitTest HitTest;
            public NativeList<Entity> Grids;
            public NativeList<ProjectileRow> Rows;
            public NativeReference<ProjectileStoreStats> Stats;
            public NativeReference<uint> Applied;
            public OriginShiftRecord Shift;
            public BlobAssetReference<WeaponDefsBlob> Weapons;
            public NativeStream.Writer Damage, Explosions;
            public uint Tick;
            public void Execute()
            {
                ProjectileStore.ApplyPendingShift(ref Rows, Shift, ref Applied);
                var stats = Stats.Value; int live = 0; Damage.BeginForEachIndex(0); Explosions.BeginForEachIndex(0);
                Grids.Sort(new EntityOrder());
                for (int i = 0; i < Rows.Length; i++)
                {
                    var row = Rows[i]; ref var weapon = ref Weapons.Value.Weapons[row.Weapon];
                    float3 end = row.Position + row.Velocity * SimTime.FixedTimestep;
                    var hit = HitTest.Segment(row.Position, end, row.Source, Grids);
                    if (hit.Kind == HitKind.Hull)
                    {
                        float3 direction = math.normalizesafe(row.Velocity); var transform = HitTest.Transforms[hit.Grid];
                        if (weapon.Damage > 0) Damage.Write(new DamageEvent { Grid = hit.Grid, LocalPoint = transform.InverseTransformPoint(hit.WorldPoint),
                            LocalDir = math.rotate(math.inverse(transform.Rotation), direction), Amount = weapon.Damage * row.DamageMultiplier,
                            Penetration = weapon.PenetrationCells, Type = row.Type, Source = row.Source, Instigator = row.Instigator,
                            Producer = ProducerKind.Projectile, Sequence = row.ShotId, WorldImpulse = direction * weapon.ImpulsePerHit });
                        Explode(row, weapon, hit.WorldPoint); stats.Hits++; continue;
                    }
                    if (Tick >= row.DeathTick)
                    { if ((row.Flags & ProjectileFlags.ExplodeOnExpire) != 0) Explode(row, weapon, end); stats.Expired++; continue; }
                    row.Position = end; Rows[live++] = row;
                }
                Rows.ResizeUninitialized(live); stats.Live = live; stats.PeakLive = math.max(stats.PeakLive, live); Stats.Value = stats;
                Damage.EndForEachIndex(); Explosions.EndForEachIndex();
            }
            void Explode(in ProjectileRow row, in WeaponDef weapon, float3 point)
            {
                if (weapon.ExplosionRadius <= 0 || weapon.ExplosionDamage <= 0) return;
                Explosions.Write(new ExplosionRequest { WorldPos = point, Radius = weapon.ExplosionRadius, Amount = weapon.ExplosionDamage * row.DamageMultiplier,
                    Type = DamageType.Explosive, Source = row.Source, Instigator = row.Instigator, Producer = ProducerKind.Projectile, Sequence = row.ShotId });
            }
            struct EntityOrder : System.Collections.Generic.IComparer<Entity> { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
        }
    }
}
