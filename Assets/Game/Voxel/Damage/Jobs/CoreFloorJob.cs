using Game.Core.Components;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.6 core floor (a decision, authority only): an armed ship whose live cells (GridMass) fell below its
    /// anchor core's floor malfunctions and becomes a hulk, like core loss, with the breach blast at the core. Runs after
    /// ShipDeathJob, so a ship that lost its core this tick is not queued twice; the counts are last tick's, one tick late.</summary>
    [BurstCompile] internal struct CoreFloorJob : IJob
    {
        [ReadOnly] public NativeList<Entity> Ships;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<GridMass> Masses;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<CoreFloorArmed> Armed;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        public NativeList<HulkTransition> Transitions;
        public NativeList<GridCoreLost> CoreLost;
        public EntityCommandBuffer Ecb;
        public uint Tick;
        public void Execute()
        {
            foreach (var root in Ships)
            {
                var grid = Grids[root];
                if (grid.Kind != GridKind.Ship || grid.AnchorInstance < 0 || Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) continue;
                var instances = Instances[root];
                if (grid.AnchorInstance >= instances.Length) continue;
                var core = instances[grid.AnchorInstance];
                if (core.Block == 0 || (core.Flags & BlockInstanceFlags.Destroyed) != 0 || !Modules.Value.TryGet(BlockModuleId.Core, core.Block, out CoreParams row)) continue;
                int cells = (int)Masses[root].Data.CellCount, floor = row.Floor;
                if (cells <= 0) continue;
                if (!Armed.HasComponent(root)) { if (cells >= floor) Ecb.AddComponent<CoreFloorArmed>(root); continue; }
                if (cells >= floor || Queued(root)) continue;
                var local = ((float3)core.Origin + .5f) * grid.CellSize;
                var world = Transforms.HasComponent(root) ? Transforms[root].TransformPoint(local) : local;
                ulong id = Ids.HasComponent(root) ? Ids[root].Value : 0;
                Transitions.Add(new HulkTransition { Grid = root, Blast = 1, Info = new HulkInfo { Cause = HulkCause.CoreMalfunction, Tick = Tick,
                    OriginPersistentId = id, FormerKind = grid.Kind, CoreLocal = local } });
                CoreLost.Add(new GridCoreLost { Grid = root, PersistentId = id, Tick = Tick, WorldPos = world });
            }
        }
        bool Queued(Entity root)
        {
            foreach (var transition in Transitions) if (transition.Grid == root) return true;
            return false;
        }
    }
}
