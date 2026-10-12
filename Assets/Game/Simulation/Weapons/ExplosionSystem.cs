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
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    // TODO(WPN-010): shields, effects and full explosion tests.
    /// <summary>SPEC-21 §5.8: drains same-tick projectile and prior-tick reactor/core explosions into hull damage.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ProjectileStepSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public partial struct ExplosionSystem : ISystem
    {
        EntityQuery grids; ExplosionJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            grids = new EntityQueryBuilder(Allocator.Temp).WithAll<VoxelGrid, LocalTransform>().Build(ref state);
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true); job.Sources = state.GetComponentLookup<GridSource>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<WeaponTuning>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Transforms.Update(ref state); job.Spawning.Update(ref state); job.Sources.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            job.Explosions = new NativeList<ExplosionRequest>(state.WorldUpdateAllocator); streams.ScheduleDrainExplosions(ref state, job.Explosions);
            job.Roots = grids.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var gather);
            job.Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Explosion, 1);
            job.Impulse = SystemAPI.GetSingleton<WeaponTuning>().ExplosionImpulsePerDamage;
            state.Dependency = job.Schedule(JobHandle.CombineDependencies(state.Dependency, gather)); streams.Damage.EndWrite(state.Dependency);
        }
        [BurstCompile] struct ExplosionJob : IJob
        {
            [ReadOnly] public NativeList<ExplosionRequest> Explosions;
            public NativeList<Entity> Roots;
            [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            [ReadOnly] public ComponentLookup<GridSource> Sources;
            public NativeStream.Writer Damage; public float Impulse;
            public void Execute()
            {
                Damage.BeginForEachIndex(0); Roots.Sort(new EntityOrder());
                foreach (var explosion in Explosions)
                {
                    if (!(explosion.Radius > 0) || !math.isfinite(explosion.Radius) || !math.all(math.isfinite(explosion.WorldPos))) continue;
                    foreach (var root in Roots)
                    {
                        if ((Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) ||
                            (Sources.HasComponent(root) && (Sources[root].Flags & GridSpawnFlags.EditorPreview) != 0)) continue;
                        var grid = Grids[root]; var transform = Transforms[root]; float3 local = transform.InverseTransformPoint(explosion.WorldPos);
                        float3 min = (float3)grid.CellMin * grid.CellSize, max = ((float3)grid.CellMax + 1) * grid.CellSize;
                        if (math.distancesq(local, math.clamp(local, min, max)) > explosion.Radius * explosion.Radius) continue;
                        float3 offset = transform.TransformPoint((min + max) * .5f) - explosion.WorldPos;
                        float weight = math.saturate(1 - math.length(offset) / explosion.Radius);
                        Damage.Write(new DamageEvent { Grid = root, LocalPoint = local, Amount = explosion.Amount, Radius = explosion.Radius,
                            Type = explosion.Type, Source = explosion.Source, Instigator = explosion.Instigator, Producer = ProducerKind.Explosion,
                            Sequence = explosion.Sequence, WorldImpulse = math.normalizesafe(offset) * (explosion.Amount * Impulse * weight * weight) });
                    }
                }
                Damage.EndForEachIndex();
            }
            struct EntityOrder : System.Collections.Generic.IComparer<Entity> { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
        }
    }
}
