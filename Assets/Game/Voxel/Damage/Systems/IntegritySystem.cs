using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.4: deterministic selection, exclusive per-grid hybrid races, canonical output publication.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageResolveSystem)), UpdateBefore(typeof(GridMassUpdateSystem))]
    public unsafe partial struct IntegritySystem : ISystem
    {
        IntegrityRaceJob race;
        EntityQuery grids;
        static readonly ProfilerMarker Select = new ProfilerMarker(ProfilerCategory.Scripts, "IntegritySystem.Select");
        static readonly ProfilerMarker Race = new ProfilerMarker(ProfilerCategory.Scripts, "IntegritySystem.Race");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            grids = new EntityQueryBuilder(Allocator.Temp).WithAll<VoxelGrid, IntegritySeed, GridIntegrityState>().Build(ref state);
            race.Reader = GridCellReader.Create(ref state); race.Bricks = state.GetComponentLookup<Brick>(true);
            race.Instances = state.GetBufferLookup<BlockInstance>(true); race.Summaries = state.GetComponentLookup<BrickIntegrity>();
            race.States = state.GetComponentLookup<GridIntegrityState>(); race.Seeds = state.GetBufferLookup<IntegritySeed>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<OrientationTablesRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var data = SystemAPI.GetSingleton<DamageStreams>().Data;
            state.Dependency = data->AllJobs(state.Dependency);
            race.Reader.Update(ref state); race.Bricks.Update(ref state); race.Instances.Update(ref state);
            race.Summaries.Update(ref state); race.States.Update(ref state); race.Seeds.Update(ref state);
            race.Config = SystemAPI.GetSingleton<DamageConfig>(); race.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value;
            race.Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value; race.Touched = data->TouchedBricks;
            var roots = grids.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var gathered);
            var candidates = new NativeList<Entity>(Allocator.TempJob); var results = new NativeList<IntegrityResult>(Allocator.TempJob);
            var deferred = new NativeReference<int>(Allocator.TempJob);
            JobHandle selected;
            using (Select.Auto()) selected = new SelectIntegrityGridsJob { Roots = roots, Removed = data->Removed, Ranges = data->RemovedGrids,
                Reader = race.Reader, Seeds = race.Seeds, Candidates = candidates, Results = results, Deferred = deferred,
                Limit = race.Config.MaxIntegrityGridsPerTick }.Schedule(gathered);
            var constructed = NativeStream.ScheduleConstruct(out var output, candidates, selected, Allocator.TempJob);
            race.Candidates = candidates.AsDeferredJobArray(); race.Results = results.AsDeferredJobArray(); race.Output = output.AsWriter();
            JobHandle raced; using (Race.Auto()) raced = race.Schedule(candidates, 1, constructed);
            var flattened = new FlattenIntegrityJob { Results = results, Deferred = deferred, Output = output.AsReader(),
                Islands = data->Islands, Masks = data->IslandMasks, Data = data }.Schedule(raced);
            var cleanup = JobHandle.CombineDependencies(roots.Dispose(flattened), candidates.Dispose(flattened), results.Dispose(flattened));
            cleanup = JobHandle.CombineDependencies(cleanup, deferred.Dispose(flattened), output.Dispose(flattened));
            state.Dependency = cleanup; data->IntegrityHandle = cleanup;
        }
        [BurstCompile] struct FlattenIntegrityJob : IJob
        {
            [ReadOnly] public NativeList<IntegrityResult> Results;
            [ReadOnly] public NativeReference<int> Deferred;
            public NativeStream.Reader Output;
            public NativeList<IslandRecord> Islands;
            public NativeList<IslandMask> Masks;
            [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
            public void Execute()
            {
                Islands.Clear(); Masks.Clear(); Data->IntegrityNodesVisited = 0;
                Data->Current.IntegrityDeferred += Deferred.Value; Data->Current.IntegrityGrids += Results.Length;
                for (int i = 0; i < Results.Length; i++)
                {
                    var result = Results[i]; Data->IntegrityNodesVisited += result.Nodes;
                    Data->Current.IntegrityDeferred += result.Deferred; Data->Current.IslandsFound += result.Islands;
                    Output.BeginForEachIndex(i);
                    for (int n = 0; n < result.Islands; n++)
                    {
                        var island = Output.Read<IslandRecord>(); island.MaskStart = Masks.Length;
                        for (int m = 0; m < island.MaskCount; m++) Masks.Add(Output.Read<IslandMask>());
                        Islands.Add(island);
                    }
                    Output.EndForEachIndex();
                }
            }
        }
    }
}
