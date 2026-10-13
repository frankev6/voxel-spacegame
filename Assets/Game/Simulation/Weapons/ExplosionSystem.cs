using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.8: consumes ordered explosions after weapons and before shield/hull resolution.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(BeamStepSystem)),
        UpdateAfter(typeof(ProjectileStepSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public partial struct ExplosionSystem : ISystem
    {
        ExplosionJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true); job.Sources = state.GetComponentLookup<GridSource>(true);
            job.Shields = state.GetComponentLookup<ShieldState>(true); job.Players = state.GetComponentLookup<PlayerShip>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<WeaponTuning>();
            state.RequireForUpdate<ShieldHitStreams>(); state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Transforms.Update(ref state); job.Spawning.Update(ref state); job.Sources.Update(ref state);
            job.Shields.Update(ref state); job.Players.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var shields = SystemAPI.GetSingleton<ShieldHitStreams>();
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            job.Explosions = new NativeList<ExplosionRequest>(Allocator.TempJob);
            streams.ScheduleDrainExplosions(ref state, job.Explosions);
            job.Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Explosion, 1);
            job.ShieldHits = shields.Hits.BeginWrite(ref state, ProducerKind.Explosion, 1);
            job.Hash = hash.AsReadOnly(); job.Impulse = SystemAPI.GetSingleton<WeaponTuning>().ExplosionImpulsePerDamage;
            SystemAPI.TryGetSingleton(out job.Cues);
            var fx = SystemAPI.GetSingleton<FxEventQueue>(); var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            job.Fx = fx.Queue.AsParallelWriter(); job.Sfx = sfx.Queue.AsParallelWriter();
            var handle = job.Schedule(sfx.Access.BeginRead(fx.Access.BeginRead(hash.BeginRead(state.Dependency))));
            streams.Damage.EndWrite(handle); shields.Hits.EndWrite(handle);
            hash.EndRead(handle); fx.Access.EndRead(handle); sfx.Access.EndRead(handle);
            state.Dependency = job.Explosions.Dispose(handle);
        }
    }
}
