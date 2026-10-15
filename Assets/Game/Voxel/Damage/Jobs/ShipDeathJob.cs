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
    /// <summary>SPEC-13 §5.6: core loss (a decision, authority only), queued as a HulkTransition. Its consequences are
    /// HulkTransitionJob's and the final destruction pass is StructuralDestroyJob's (ADR-0117).</summary>
    [BurstCompile] internal struct ShipDeathJob : IJob
    {
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [ReadOnly] public ComponentLookup<Unity.Transforms.LocalTransform> Transforms;
        [ReadOnly] public NativeList<DamageGridResult> Results;
        public NativeList<GridCoreLost> CoreLost;
        public NativeList<HulkTransition> Transitions;
        public uint Tick;
        public void Execute()
        {
            CoreLost.Clear();
            foreach (var result in Results)
            {
                Entity root = result.Grid;
                if (result.CoreLost == 0 || Hulks.HasComponent(root) || !Grids.HasComponent(root)) continue;
                var grid = Grids[root];
                if (grid.Kind == GridKind.Hulk) continue;
                var id = Ids[root].Value;
                // The decision; HulkTransitionSystem applies it (components, kind, breach blast and sound) on every machine (ADR-0117).
                var local = Transforms.HasComponent(root) ? Transforms[root].InverseTransformPoint(result.CoreWorldPos) : default;
                Transitions.Add(new HulkTransition { Grid = root, Blast = 1, Info = new HulkInfo { Cause = HulkCause.CoreLost, Tick = Tick,
                    OriginPersistentId = id, FormerKind = grid.Kind, CoreLocal = local } });
                CoreLost.Add(new GridCoreLost { Grid = root, PersistentId = id, Tick = Tick, WorldPos = result.CoreWorldPos });
            }
        }
    }
}
