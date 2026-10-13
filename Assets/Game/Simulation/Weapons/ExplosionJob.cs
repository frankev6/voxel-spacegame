using Game.Data.Combat;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.8: ordered blast requests, shields first, one presentation pair per blast.</summary>
    [BurstCompile]
    internal struct ExplosionJob : IJob
    {
        [ReadOnly] public NativeList<ExplosionRequest> Explosions;
        [ReadOnly] public SpatialHashQueryData Hash;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<GridSource> Sources;
        [ReadOnly] public ComponentLookup<ShieldState> Shields;
        [ReadOnly] public ComponentLookup<PlayerShip> Players;
        // Queue handles are chained at the system boundary; parallel writers permit other guarded presentation producers.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        public NativeStream.Writer Damage, ShieldHits;
        public CombatAudioCues Cues;
        public float Impulse;

        public void Execute()
        {
            Damage.BeginForEachIndex(0); ShieldHits.BeginForEachIndex(0);
            foreach (var explosion in Explosions)
            {
                if (!(explosion.Radius > 0) || !math.isfinite(explosion.Radius) || !math.all(math.isfinite(explosion.WorldPos))) continue;
                var candidates = new FixedList512Bytes<int>();
                int count = Hash.QuerySphere(explosion.WorldPos, explosion.Radius, ref candidates);
                // A blast must reach every overlapped grid. Overflow falls back to the canonical sorted proxy list.
                if (count > candidates.Capacity)
                {
                    for (int i = 0; i < Hash.Proxies.Length; i++)
                    {
                        var proxy = Hash.Proxies[i]; float reach = proxy.Radius + explosion.Radius;
                        if (math.distancesq(proxy.Center, explosion.WorldPos) <= reach * reach) Apply(explosion, proxy);
                    }
                }
                else for (int i = 0; i < candidates.Length; i++) Apply(explosion, Hash.Get(candidates[i]));
                bool player = Players.HasComponent(explosion.Source);
                var effect = FxEvents.Explosion(explosion.WorldPos, explosion.Radius, default);
                if (player) effect.Flags |= FxEventFlags.PlayerSource;
                Fx.Enqueue(effect);
                Sfx.Enqueue(new SfxEvent { Cue = explosion.Radius < 4 ? Cues.ExplosionSmall : Cues.ExplosionLarge,
                    Pos = explosion.WorldPos, Volume = 1, Flags = player ? SfxEventFlags.PlayerSource : SfxEventFlags.None });
            }
            Damage.EndForEachIndex(); ShieldHits.EndForEachIndex();
        }

        void Apply(in ExplosionRequest explosion, in SpatialProxy proxy)
        {
            Entity root = proxy.Entity;
            if (proxy.Kind == ProxyKind.Celestial || !Grids.HasComponent(root) || !Transforms.HasComponent(root) ||
                Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root) ||
                Sources.HasComponent(root) && (Sources[root].Flags & GridSpawnFlags.EditorPreview) != 0) return;
            var grid = Grids[root]; var transform = Transforms[root];
            float3 local = transform.InverseTransformPoint(explosion.WorldPos);
            float3 min = (float3)grid.CellMin * grid.CellSize, max = ((float3)grid.CellMax + 1) * grid.CellSize;
            float3 offset = transform.TransformPoint((min + max) * .5f) - explosion.WorldPos;
            float weight = math.saturate(1 - math.length(offset) / explosion.Radius);
            float3 impulse = math.normalizesafe(offset) * (explosion.Amount * Impulse * weight * weight);
            if (Shields.HasComponent(root))
            {
                var shield = Shields[root];
                if (shield.Active == 1 && ShieldMath.SphereOverlaps(local, explosion.Radius, shield.Center, shield.Radii))
                {
                    ShieldHits.Write(new ShieldHit { Grid = root, LocalPoint = local, Amount = explosion.Amount,
                        Radius = explosion.Radius, Type = explosion.Type, Source = explosion.Source, Instigator = explosion.Instigator,
                        WorldImpulse = impulse, Flags = ShieldHitFlags.Blast, Producer = ProducerKind.Explosion, Sequence = explosion.Sequence });
                    return;
                }
            }
            if (math.distancesq(local, math.clamp(local, min, max)) > explosion.Radius * explosion.Radius) return;
            Damage.Write(new DamageEvent { Grid = root, LocalPoint = local, Amount = explosion.Amount, Radius = explosion.Radius,
                Type = explosion.Type, Source = explosion.Source, Instigator = explosion.Instigator, Producer = ProducerKind.Explosion,
                Sequence = explosion.Sequence, WorldImpulse = impulse });
        }
    }
}
