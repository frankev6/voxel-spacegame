using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.11: incremental removal/restoration, deterministic parallel recompute and mass-frame application.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public partial struct GridMassUpdateSystem : ISystem
    {
        static readonly ProfilerMarker Remove = new ProfilerMarker(ProfilerCategory.Scripts, "GridMassUpdateSystem.Remove");
        static readonly ProfilerMarker AddRestored = new ProfilerMarker(ProfilerCategory.Scripts, "GridMassUpdateSystem.AddRestored");
        static readonly ProfilerMarker Recompute = new ProfilerMarker(ProfilerCategory.Scripts, "GridMassUpdateSystem.Recompute");
        static readonly ProfilerMarker Apply = new ProfilerMarker(ProfilerCategory.Scripts, "GridMassUpdateSystem.Apply");
        EntityQuery roots;
        GridMassRemoveJob remove;
        GridMassAddJob add;
        RecomputeListJob list;
        BrickMassJob bricks;
        RecomputeSumJob sum;
        PhysicsMassApplyJob apply;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            roots = new EntityQueryBuilder(Allocator.Temp).WithAll<GridMassRecompute, GridMass, VoxelGrid, BrickSlot>().WithDisabled<GridSpawning>().Build(ref state);
            remove.Grids = state.GetComponentLookup<VoxelGrid>(true); remove.Masses = state.GetComponentLookup<GridMass>();
            add.Grids = state.GetComponentLookup<VoxelGrid>(true); add.Masses = state.GetComponentLookup<GridMass>();
            list.Grids = state.GetComponentLookup<VoxelGrid>(true); list.Bricks = state.GetComponentLookup<Brick>(true); list.Slots = state.GetBufferLookup<BrickSlot>(true);
            bricks.Cells = state.GetBufferLookup<BrickCells>(true); bricks.Sparse = state.GetBufferLookup<BrickSparseCells>(true);
            bricks.Masks = state.GetBufferLookup<BrickSolidMask>(true); bricks.Palettes = state.GetBufferLookup<PaletteEntry>(true); bricks.Instances = state.GetBufferLookup<BlockInstance>(true);
            sum.Masses = state.GetComponentLookup<GridMass>(); sum.Recompute = state.GetComponentLookup<GridMassRecompute>();
            apply.Masses = state.GetComponentLookup<GridMass>(true); apply.States = state.GetComponentLookup<GridColliderState>(true);
            apply.Grids = state.GetComponentLookup<VoxelGrid>(true); apply.Sources = state.GetComponentLookup<GridSource>(true); apply.Transforms = state.GetComponentLookup<LocalTransform>(true);
            apply.PhysicsMasses = state.GetComponentLookup<PhysicsMass>(); apply.Velocities = state.GetComponentLookup<PhysicsVelocity>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<BlockDefsRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            remove.Grids.Update(ref state); remove.Masses.Update(ref state); list.Grids.Update(ref state); list.Bricks.Update(ref state); list.Slots.Update(ref state);
            add.Grids.Update(ref state); add.Masses.Update(ref state);
            bricks.Cells.Update(ref state); bricks.Sparse.Update(ref state); bricks.Masks.Update(ref state); bricks.Palettes.Update(ref state); bricks.Instances.Update(ref state);
            sum.Masses.Update(ref state); sum.Recompute.Update(ref state); apply.Masses.Update(ref state); apply.States.Update(ref state);
            apply.Grids.Update(ref state); apply.Sources.Update(ref state); apply.Transforms.Update(ref state); apply.PhysicsMasses.Update(ref state); apply.Velocities.Update(ref state);
            var allocator = state.WorldUpdateAllocator;
            var changed = new NativeList<Entity>(allocator); var blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value;
            using (Remove.Auto())
                if (SystemAPI.TryGetSingleton<DamageStreams>(out var streams))
                {
                    var view = streams.BeginRemovedRead(ref state);
                    remove.Cells = view.Cells; remove.Ranges = view.Grids; remove.Blocks = blocks; remove.Changed = changed;
                    state.Dependency = remove.Schedule(state.Dependency); streams.EndRemovedRead(state.Dependency);
                }
            using (AddRestored.Auto())
                if (SystemAPI.TryGetSingleton<DamageStreams>(out var restoredStreams))
                {
                    // TODO(DMG-019): the resolver restore pass must populate these canonical views; they are empty until then.
                    var view = restoredStreams.BeginRestoredRead(ref state);
                    add.Cells = view.Cells; add.Ranges = view.Grids; add.Blocks = blocks; add.Changed = changed;
                    state.Dependency = add.Schedule(state.Dependency); restoredStreams.EndRestoredRead(state.Dependency);
                }
            using (Recompute.Auto())
            {
                list.Entities = roots.ToEntityListAsync(allocator, state.Dependency, out var gather);
                var rootList = new NativeList<RecomputeRoot>(allocator); var work = new NativeList<RecomputeBrick>(allocator);
                var results = new NativeList<StructureMassData>(allocator);
                list.Roots = rootList; list.Work = work; list.Results = results;
                var planned = list.Schedule(JobHandle.CombineDependencies(state.Dependency, gather));
                bricks.Work = work.AsDeferredJobArray(); bricks.Roots = rootList.AsDeferredJobArray(); bricks.Results = results.AsDeferredJobArray(); bricks.Blocks = blocks;
                var built = bricks.Schedule(work, 4, planned);
                sum.Roots = rootList; sum.Results = results; sum.Changed = changed; state.Dependency = sum.Schedule(built);
            }
            using (Apply.Auto()) { apply.Changed = changed; state.Dependency = apply.Schedule(state.Dependency); }
        }
    }
}
