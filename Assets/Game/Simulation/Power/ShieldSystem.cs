using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Simulation.Power.ShieldHit>))]

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.5: world-owned shield hit channel, stable gather and exclusive per-root resolution.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(ExplosionSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public partial struct ShieldSystem : ISystem
    {
        ShieldHitStreams owned;
        EntityQuery roots;
        ShieldUpdateJob update;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            owned = new ShieldHitStreams { Hits = SimEventChannel<ShieldHit>.Create("shield_hits") };
            state.EntityManager.CreateSingleton(owned);
            roots = new EntityQueryBuilder(Allocator.Temp).WithAllRW<ShieldState, ShieldRipple>().WithAll<ShipStats, PowerConsumer>().Build(ref state);
            update.Entities = state.GetEntityTypeHandle(); update.Shields = state.GetComponentTypeHandle<ShieldState>();
            update.Ripples = state.GetBufferTypeHandle<ShieldRipple>(); update.Stats = state.GetComponentTypeHandle<ShipStats>(true);
            update.Consumers = state.GetBufferTypeHandle<PowerConsumer>(true); update.Instances = state.GetBufferLookup<BlockInstance>(true);
            update.Controls = state.GetComponentLookup<ShieldControl>(true); update.Multipliers = state.GetComponentLookup<ShipStatMultipliers>(true);
            update.Transforms = state.GetComponentLookup<LocalTransform>(true); update.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            update.Hulks = state.GetComponentLookup<Hulk>(true); update.Spawning = state.GetComponentLookup<GridSpawning>(true);
            update.Reader = GridCellReader.Create(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<PowerTuning>();
            state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<FxEventQueue>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        { state.CompleteDependency(); owned.Hits.Dispose(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            update.Entities.Update(ref state); update.Shields.Update(ref state); update.Ripples.Update(ref state);
            update.Stats.Update(ref state); update.Consumers.Update(ref state); update.Instances.Update(ref state);
            update.Controls.Update(ref state); update.Multipliers.Update(ref state); update.Transforms.Update(ref state);
            update.Velocities.Update(ref state); update.Hulks.Update(ref state); update.Spawning.Update(ref state); update.Reader.Update(ref state);
            update.Tick = SystemAPI.GetSingleton<SimClock>().Tick; update.Tuning = SystemAPI.GetSingleton<PowerTuning>();
            update.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value; update.DamageConfig = SystemAPI.GetSingleton<DamageConfig>();
            owned.Hits.BeginTick(update.Tick);
            var hits = new NativeList<ShieldHit>(Allocator.TempJob);
            var ranges = new NativeParallelHashMap<Entity, int2>(1, Allocator.TempJob);
            var set = owned.Hits.BeginRead(ref state);
            var gather = SimEventGather.Schedule(set, hits, state.Dependency);
            gather = new ShieldHitGatherJob { Hits = hits, Ranges = ranges }.Schedule(gather);
            var damage = SystemAPI.GetSingleton<DamageStreams>(); var fx = SystemAPI.GetSingleton<FxEventQueue>();
            update.Hits = hits; update.Ranges = ranges;
            update.Damage = damage.Damage.BeginWrite(ref state, ProducerKind.ShieldOverflow, math.max(1, roots.CalculateChunkCountWithoutFiltering()));
            update.Fx = fx.Queue.AsParallelWriter();
            var handle = update.ScheduleParallel(roots, fx.Access.BeginRead(JobHandle.CombineDependencies(gather, state.Dependency)));
            damage.Damage.EndWrite(handle); owned.Hits.EndRead(handle); fx.Access.EndRead(handle);
            state.Dependency = JobHandle.CombineDependencies(hits.Dispose(handle), ranges.Dispose(handle));
        }
    }
}
