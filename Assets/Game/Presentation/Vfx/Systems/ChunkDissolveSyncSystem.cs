using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §5.7; uses canonical tick lifetime (ADR-0060), sole writer of ChunkDissolve.</summary>
    [BurstCompile, UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(FxUploadSystem))]
    public partial struct ChunkDissolveSyncSystem : ISystem
    {
        static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("ChunkDissolveSyncSystem.Update");
        NativeReference<int> count;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            count = new NativeReference<int>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new ChunkDissolveStats { Count = count });
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<VfxRuntimeConfig>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var scope = UpdateMarker.Auto();
            var output = SystemAPI.GetSingletonRW<ChunkDissolveStats>().ValueRW.Count;
            state.Dependency = new ResetCount { Count = output }.Schedule(state.Dependency);
            state.Dependency = new DissolveJob { Count = output, Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Seconds = SystemAPI.GetSingleton<VfxRuntimeConfig>().ChunkDissolveSeconds }.Schedule(state.Dependency);
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.Dependency.Complete(); count.Dispose(); }
        [BurstCompile] struct ResetCount : IJob { public NativeReference<int> Count; public void Execute() => Count.Value = 0; }
        [BurstCompile] partial struct DissolveJob : IJobEntity
        {
            public NativeReference<int> Count; public uint Tick; public float Seconds;
            void Execute(in ChunkBody body, ref ChunkDissolve dissolve)
            { dissolve.Value = body.Live != 0 ? FxStateMath.Dissolve(body.EndTick, Tick, Seconds) : 0; if (dissolve.Value > 0) Count.Value++; }
        }
    }
}
