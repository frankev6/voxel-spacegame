using Game.Data.Combat;
using Game.Data.Streams;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>ADR-0117 applier: turns each queued <see cref="HulkTransition"/> into a hulk (components, kind) and, for a core loss,
    /// the breach explosion request and sound. On the authority the explosion damages; on a co-op guest its damage is drained like any
    /// cosmetic shot's, and only its effects show.</summary>
    [BurstCompile] internal struct HulkTransitionJob : IJob
    {
        public NativeList<HulkTransition> Transitions;
        public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        public NativeStream.Writer Explosions;
        public EntityCommandBuffer Ecb;
        public DamageConfig Config;
        public void Execute()
        {
            Explosions.BeginForEachIndex(0);
            foreach (var transition in Transitions)
            {
                Entity root = transition.Grid;
                if (!Grids.HasComponent(root) || Hulks.HasComponent(root)) continue;
                var grid = Grids[root];
                HulkSetup.Add(ref Ecb, root, transition.Info);
                if (grid.Kind != GridKind.Asteroid) { grid.Kind = GridKind.Hulk; Grids[root] = grid; }
                if (transition.Blast == 0 || !Transforms.HasComponent(root)) continue;
                var world = Transforms[root].TransformPoint(transition.Info.CoreLocal);
                Explosions.Write(new ExplosionRequest { WorldPos = world, Radius = Config.CoreBreachRadiusCells * grid.CellSize,
                    Amount = Config.CoreBreachDamage, Type = DamageType.Explosive, Source = root, Producer = ProducerKind.Explosion });
                if (Config.CueCoreLost.Value != 0) Sfx.Enqueue(new SfxEvent { Cue = Config.CueCoreLost, Pos = world, Volume = 1 });
            }
            Explosions.EndForEachIndex();
            Transitions.Clear();
        }
    }
}
