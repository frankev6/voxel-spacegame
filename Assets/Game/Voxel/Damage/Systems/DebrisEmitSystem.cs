using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.5 debris and break sounds for the tick's removed cells; ADR-0117 applier, every machine. It was the first step
    /// of SplitSystem, which only the authority runs; the removals it reads come from the resolver or from exact writes alike.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(IntegritySystem)), UpdateBefore(typeof(SplitSystem))]
    public unsafe partial struct DebrisEmitSystem : ISystem
    {
        ComponentLookup<VoxelGrid> grids;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            grids = state.GetComponentLookup<VoxelGrid>(true);
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>(); state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>(); state.RequireForUpdate<BlockDefsRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            grids.Update(ref state);
            var data = SystemAPI.GetSingleton<DamageStreams>().Data;
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            var dependency = JobHandle.CombineDependencies(state.Dependency, data->IntegrityHandle, data->RemovedWriteHandle);
            dependency = new EmitDebrisJob { Removed = data->Removed, Ranges = data->RemovedGrids, Grids = grids, Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value,
                Config = SystemAPI.GetSingleton<DamageConfig>(), Fx = fx.Queue.AsParallelWriter(), Sfx = sfx.Queue.AsParallelWriter(), Data = data
            }.Schedule(sfx.Access.BeginRead(fx.Access.BeginRead(dependency)));
            sfx.Access.EndRead(dependency); fx.Access.EndRead(dependency);
            data->DebrisHandle = dependency; state.Dependency = dependency;
        }
    }
}
