using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.6: guarded origin shift, parallel hit streams and stable compaction.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(ImpactEventSystem)), UpdateAfter(typeof(SpatialHashBuildSystem)), UpdateBefore(typeof(ExplosionSystem))]
    public partial struct ProjectileStepSystem : ISystem, ISystemStartStop
    {
        ProjectileStore owned;
        ProjectileStepJob job;
        ComponentLookup<OriginShiftRecord> shifts;
        NativeArray<int2> batchCounts;
        int scheduledRows;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            int capacity = math.max(1, SystemAPI.TryGetSingleton<WeaponTuning>(out var tuning) ? tuning.MaxProjectiles : 8192);
            owned = ProjectileStore.Create(capacity); state.EntityManager.CreateSingleton(owned);
            scheduledRows = ((capacity + 63) / 64) * 64;
            batchCounts = new NativeArray<int2>(scheduledRows / 64, Allocator.Persistent);
            job.HitTest = CombatHitTest.Create(ref state);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            shifts = state.GetComponentLookup<OriginShiftRecord>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<DamageStreams>();
            state.RequireForUpdate<ShieldHitStreams>(); state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        { state.CompleteDependency(); owned.Dispose(); batchCounts.Dispose(); }
        [BurstCompile] public void OnStartRunning(ref SystemState state)
        { owned.CompleteAccess(); owned.Rows.Clear(); owned.Stats.Value = default; owned.AppliedShiftSequence.Value = 0; }
        [BurstCompile] public void OnStopRunning(ref SystemState state)
        { owned.CompleteAccess(); owned.Rows.Clear(); owned.Stats.Value = default; }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.HitTest.Update(ref state); job.Velocities.Update(ref state); shifts.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            var shields = SystemAPI.GetSingleton<ShieldHitStreams>();
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            job.Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Projectile, batchCounts.Length);
            job.Explosions = streams.Explosions.BeginWrite(ref state, ProducerKind.Projectile, batchCounts.Length);
            job.Shields = shields.Hits.BeginWrite(ref state, ProducerKind.Projectile, batchCounts.Length);
            SystemAPI.TryGetSingletonEntity<OriginShiftRecord>(out var shiftEntity);
            var shift = new ProjectileShiftJob { Rows = owned.Rows, Applied = owned.AppliedShiftSequence,
                Shifts = shifts, ShiftEntity = shiftEntity };
            var shifted = shift.Schedule(owned.Acquire(state.Dependency));
            job.Rows = owned.Rows; job.BatchCounts = batchCounts; job.Hash = hash.AsReadOnly();
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            job.MaxCandidates = SystemAPI.TryGetSingleton<WeaponTuning>(out var tuning) ? tuning.MaxCandidates : 32;
            SystemAPI.TryGetSingleton(out job.Cues);
            job.Fx = fx.Queue.AsParallelWriter(); job.Sfx = sfx.Queue.AsParallelWriter();
            var reads = sfx.Access.BeginRead(fx.Access.BeginRead(hash.BeginRead(shifted)));
            // The installed batch API accepts explicit lengths only. Empty upper-bound lanes still publish empty streams.
            var stepped = job.ScheduleBatch(scheduledRows, 64, reads);
            hash.EndRead(stepped); fx.Access.EndRead(stepped); sfx.Access.EndRead(stepped);
            streams.Damage.EndWrite(stepped); streams.Explosions.EndWrite(stepped); shields.Hits.EndWrite(stepped);
            state.Dependency = new ProjectileCompactJob { Rows = owned.Rows, Stats = owned.Stats, BatchCounts = batchCounts }.Schedule(stepped);
            owned.Release(state.Dependency);
        }
    }
}
