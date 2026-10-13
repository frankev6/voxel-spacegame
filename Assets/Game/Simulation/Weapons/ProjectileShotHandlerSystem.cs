using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.5b: projectile delivery from the common shot list.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(WeaponFireSystem)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct ProjectileShotHandlerSystem : ISystem
    {
        ComponentLookup<OriginShiftRecord> shifts;
        ComponentLookup<PersistentId> ids;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            shifts = state.GetComponentLookup<OriginShiftRecord>(true); ids = state.GetComponentLookup<PersistentId>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponShots>(); state.RequireForUpdate<ProjectileStore>();
            state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<WeaponTuning>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var shots = SystemAPI.GetSingleton<WeaponShots>(); var store = SystemAPI.GetSingleton<ProjectileStore>();
            shifts.Update(ref state); ids.Update(ref state);
            SystemAPI.TryGetSingletonEntity<OriginShiftRecord>(out var shiftEntity);
            state.Dependency = new AppendJob { Shots = shots.Shots, Rows = store.Rows, Stats = store.Stats, Applied = store.AppliedShiftSequence,
                Shifts = shifts, Ids = ids, ShiftEntity = shiftEntity, Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value, Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Capacity = SystemAPI.GetSingleton<WeaponTuning>().MaxProjectiles }.Schedule(store.Acquire(shots.Access.BeginRead(state.Dependency)));
            shots.Access.EndRead(state.Dependency); store.Release(state.Dependency);
        }
        [BurstCompile] struct AppendJob : IJob
        {
            [ReadOnly] public NativeList<WeaponShot> Shots;
            public NativeList<ProjectileRow> Rows; public NativeReference<ProjectileStoreStats> Stats; public NativeReference<uint> Applied;
            [ReadOnly] public ComponentLookup<OriginShiftRecord> Shifts;
            [ReadOnly] public ComponentLookup<PersistentId> Ids;
            public Entity ShiftEntity; public BlobAssetReference<WeaponDefsBlob> Weapons; public uint Tick; public int Capacity;
            public void Execute()
            {
                ProjectileStore.ApplyPendingShift(ref Rows, Shifts.HasComponent(ShiftEntity) ? Shifts[ShiftEntity] : default, ref Applied); var stats = Stats.Value;
                foreach (var shot in Shots)
                {
                    if (shot.Delivery != WeaponDelivery.Projectile && shot.Delivery != WeaponDelivery.Missile) continue;
                    if (Rows.Length >= Capacity)
                    {
                        if (stats.DroppedFull == 0) { FixedString128Bytes warning = "Projectile store full; released shots are being dropped."; GameLog.Burst.Warn(warning); }
                        stats.DroppedFull++; continue;
                    }
                    ref var weapon = ref Weapons.Value.Weapons[shot.Def];
                    var random = WeaponRandom.Create(Tick, Ids.HasComponent(shot.Grid) ? Ids[shot.Grid].Value : 0, shot.ShotId);
                    random.NextFloat(); random.NextFloat(); // The same pellet consumed two spread samples.
                    uint life = weapon.LifetimeMinTicks == weapon.LifetimeMaxTicks ? weapon.LifetimeMaxTicks : random.NextUInt(weapon.LifetimeMinTicks, weapon.LifetimeMaxTicks + 1);
                    Rows.Add(new ProjectileRow { Position = shot.MuzzleWorld, Velocity = shot.DirWorld * weapon.ProjectileSpeed + shot.ShooterVelocity,
                        Source = shot.Grid, Instigator = shot.Instigator, Target = shot.Target, ShotId = shot.ShotId, DeathTick = Tick + life,
                        Weapon = shot.Def, Type = weapon.Type, Flags = shot.Flags, DamageMultiplier = shot.DamageMultiplier }); stats.Fired++;
                }
                stats.Live = Rows.Length; stats.PeakLive = math.max(stats.PeakLive, stats.Live); Stats.Value = stats;
            }
        }
    }
}
