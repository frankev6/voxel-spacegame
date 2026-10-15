using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Voxel.Grid;
using Game.Voxel.PresentationStreams;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.6: core loss (a decision, authority only). The final destruction pass is StructuralDestroyJob's (ADR-0117).</summary>
    [BurstCompile] internal struct ShipDeathJob : IJob
    {
        public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        [ReadOnly] public NativeList<DamageGridResult> Results;
        public NativeList<GridCoreLost> CoreLost;
        public NativeStream.Writer Explosions;
        public EntityCommandBuffer Ecb;
        public DamageConfig Config; public uint Tick;
        public void Execute()
        {
            CoreLost.Clear(); Explosions.BeginForEachIndex(0);
            foreach (var result in Results)
            {
                Entity root = result.Grid;
                if (result.CoreLost == 0 || Hulks.HasComponent(root) || !Grids.HasComponent(root)) continue;
                var grid = Grids[root];
                if (grid.Kind == GridKind.Hulk) continue;
                var id = Ids[root].Value;
                Ecb.AddComponent<Hulk>(root); Ecb.AddComponent(root, new HulkInfo { Cause = HulkCause.CoreLost, Tick = Tick, OriginPersistentId = id, FormerKind = grid.Kind });
                grid.Kind = GridKind.Hulk; Grids[root] = grid;
                CoreLost.Add(new GridCoreLost { Grid = root, PersistentId = id, Tick = Tick, WorldPos = result.CoreWorldPos });
                Explosions.Write(new ExplosionRequest { WorldPos = result.CoreWorldPos, Radius = Config.CoreBreachRadiusCells * grid.CellSize,
                    Amount = Config.CoreBreachDamage, Type = DamageType.Explosive, Source = root, Producer = ProducerKind.Explosion });
                if (Config.CueCoreLost.Value != 0)
                    Sfx.Enqueue(new SfxEvent { Cue = Config.CueCoreLost, Pos = result.CoreWorldPos, Volume = 1 });
            }
            Explosions.EndForEachIndex();
        }
    }
}
