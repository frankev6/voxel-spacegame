using Game.Core.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Voxel.Rendering
{
    /// <summary>Update applied brick transforms after the camera bridge and submit the shared pool (SPEC-11 §5.10).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(BrickMeshApplySystem))]
    public partial struct BrickPoolRenderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<VoxelRenderRuntime>(); state.RequireForUpdate<VoxelRenderCamera>(); }
        public void OnUpdate(ref SystemState state)
        {
            var runtime = SystemAPI.GetSingleton<VoxelRenderRuntime>();
            var camera = SystemAPI.GetSingleton<VoxelRenderCamera>().Value.Value;
            if (runtime.IsReady == 0 || !camera) return;
            var pool = runtime.Managed.Value.Pool;
            new TransformJob { Matrices = pool.WorldMatrices }.ScheduleParallel(state.Dependency).Complete();
            float shadowDistance = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline ? pipeline.shadowDistance : 0;
            pool.Render(camera, shadowDistance);
        }
        [BurstCompile]
        partial struct TransformJob : IJobEntity
        {
            // Applied handles are unique per brick. Only live bricks participate; deleted slots retire before submission.
            [NativeDisableParallelForRestriction] public NativeArray<float4x4> Matrices;
            void Execute(in Game.Voxel.Model.Brick brick, in BrickRenderState render, in LocalToWorld transform)
            { if (render.HasMesh != 0) Matrices[render.Handle.Slot] = transform.Value; }
        }
    }
}
