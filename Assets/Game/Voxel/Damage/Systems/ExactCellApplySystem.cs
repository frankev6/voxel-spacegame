using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage
{
    /// <summary>ADR-0117 applier, every machine: applies exact writes queued through <see cref="DamageStreams.QueueExact"/> at the resolver's
    /// place in the tick, through the resolver's own CellWriter. Nothing queues them in solo play or on a co-op host, where it does nothing;
    /// on a co-op guest they carry the host's cell and instance changes.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageResolveSystem)), UpdateBefore(typeof(IntegritySystem))]
    [UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct ExactCellApplySystem : ISystem
    {
        DamageAccess access;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            access = DamageAccess.Create(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var data = SystemAPI.GetSingleton<DamageStreams>().Data;
            data->ExactHandle.Complete();   // last tick's apply; producers complete it too before they append
            if (data->ExactWrites.Length == 0) return;
            access.Update(ref state);
            var dependency = JobHandle.CombineDependencies(state.Dependency, data->ResolverHandle, data->TickResetHandle);
            var job = new ExactCellApplyJob
            {
                Access = access, Config = SystemAPI.GetSingleton<DamageConfig>(), Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value, Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value, Writes = data->ExactWrites,
                Cells = data->Removed, Ranges = data->RemovedGrids, Bricks = data->TouchedBricks, Destroy = data->DestroyList, Grids = data->GridResults,
                Restored = data->Restored, RestoredRanges = data->RestoredGrids, Data = data
            }.Schedule(dependency);
            data->ExactHandle = job;
            data->RemovedWriteHandle = JobHandle.CombineDependencies(data->RemovedWriteHandle, job);
            data->ResolverHandle = JobHandle.CombineDependencies(data->ResolverHandle, job);
            state.Dependency = job;
        }
    }
}
