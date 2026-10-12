using System.Diagnostics;
using Game.Core;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Voxel.Rendering
{
    /// <summary>Register shared render inputs, upload completed geometry and publish applied state (SPEC-11 §5.5/5.10).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup), OrderFirst = true)]
    public partial struct BrickMeshApplySystem : ISystem
    {
        UnityObjectRef<VoxelRenderManagedState> owned;
        NativeArray<BrickRenderState> applied;
        ComponentLookup<Brick> bricks;
        ComponentLookup<BrickRenderState> states;
        ComponentLookup<LocalToWorld> transforms;
        BufferLookup<BrickPropInstance> props;
        byte failedRegistration;
        static readonly ProfilerMarker Register = new ProfilerMarker(ProfilerCategory.Scripts, "BrickMeshApplySystem.Register");
        static readonly ProfilerMarker Apply = new ProfilerMarker(ProfilerCategory.Scripts, "BrickMeshApplySystem.Apply");
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VoxelRenderConfig>(); state.RequireForUpdate<VoxelRenderRuntime>();
            state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<BrickMeshBatch>(); state.RequireForUpdate<DeferredDisposalQueue>();
            bricks = state.GetComponentLookup<Brick>(true); states = state.GetComponentLookup<BrickRenderState>();
            transforms = state.GetComponentLookup<LocalToWorld>(true); props = state.GetBufferLookup<BrickPropInstance>();
            applied = new NativeArray<BrickRenderState>(256, Allocator.Persistent);
        }
        public void OnUpdate(ref SystemState state)
        {
            ref var runtime = ref SystemAPI.GetSingletonRW<VoxelRenderRuntime>().ValueRW;
            if (runtime.IsReady == 0 && !TryRegister(ref state, ref runtime)) return;
            var pool = runtime.Managed.Value.Pool;
            uint frame = SystemAPI.GetSingleton<DeferredDisposalQueue>().Frame; pool.AdvanceFrame(frame);
            ref var batch = ref SystemAPI.GetSingletonRW<BrickMeshBatch>().ValueRW;
            if (batch.InFlight == 0 || (!batch.Handle.IsCompleted && frame - batch.ScheduledFrame < 4)) return;
            batch.Handle.Complete(); state.CompleteDependency();
            bricks.Update(ref state); states.Update(ref state); transforms.Update(ref state); props.Update(ref state);
            using var marker = Apply.Auto(); long start = Stopwatch.GetTimestamp(); int count = batch.Work.Length;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var work = batch.Work[i];
                    if (!bricks.HasComponent(work.Brick) || !states.HasComponent(work.Brick)) continue;
                    var previous = states[work.Brick]; var geometry = batch.Results[i];
                    var handle = pool.Upload(previous.Handle, Borrow(geometry.Vertices), Borrow(geometry.Indices),
                        new AABB { Center = (work.BoundsMin + work.BoundsMax) * .5f, Extents = (work.BoundsMax - work.BoundsMin) * .5f },
                        transforms[work.Brick].Value);
                    pool.GetRanges(handle, out var vertices, out var indices);
                    applied[i] = new BrickRenderState { Handle = handle, Vertices = vertices, Indices = indices,
                        VertexCount = work.VertexCount, IndexCount = work.IndexCount, ShownLod = work.Lod, HasMesh = 1 };
                    pool.SetVisible(handle, work.VertexCount != 0);
                    if ((work.SnapshotFlags & 1) != 0) GameLog.WarnOnce("Voxel.MeshMissingContent", "Voxel", "Invalid cell reference rendered with missing-content appearance.");
                    if ((work.SnapshotFlags & 2) != 0) GameLog.WarnOnce("Voxel.MeshPropOverflow", "Voxel", "prop overflow");
                }
                new ApplyResultsJob { Work = batch.Work.AsArray(), Applied = applied, Props = batch.Props,
                    Bricks = bricks, States = states, PropBuffers = props }.Schedule(count, 1).Complete();
            }
            finally
            {
                batch.ReleaseResults(); batch.InFlight = 0;
                if (count != 0)
                {
                    float micros = (float)((Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency / count);
                    batch.AvgApplyMicros = math.lerp(batch.AvgApplyMicros, micros, .2f);
                }
                batch.Work.Clear();
            }
        }
        static unsafe NativeArray<T> Borrow<T>(UnsafeList<T> list) where T : unmanaged
        {
            // Synchronous upload only, after the owning mesh handle completed. No borrowed view escapes this update.
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list.Ptr, list.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return array;
        }
        bool TryRegister(ref SystemState state, ref VoxelRenderRuntime runtime)
        {
            if (failedRegistration != 0 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
            var graphics = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null) return false;
            using var marker = Register.Auto();
            try
            {
                var config = SystemAPI.GetSingleton<VoxelRenderConfig>();
                var managed = ScriptableObject.CreateInstance<VoxelRenderManagedState>(); managed.hideFlags = HideFlags.DontSave;
                owned = managed; runtime.Managed = managed;
                // One runtime copy of each shared material protects source assets from temporary palette references.
                managed.BrickMaterial = new Material(config.BrickMaterial.Value) { hideFlags = HideFlags.DontSave };
                managed.PropMaterial = new Material(config.PropMaterial.Value) { hideFlags = HideFlags.DontSave };
                managed.BrickMaterial.SetTexture("_PaintPalette", config.Palette.Value);
                managed.PropMaterial.SetTexture("_PaintPalette", config.Palette.Value);
                managed.Pool = new BrickPoolRenderer(managed.BrickMaterial, config.BrickPoolCopy.Value);
                runtime.PropMaterial = graphics.RegisterMaterial(managed.PropMaterial);
                managed.Placeholder = new Mesh { name = "Functional placeholder", hideFlags = HideFlags.DontSave };
                managed.Placeholder.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero }; managed.Placeholder.triangles = new[] { 0, 1, 2 };
                runtime.Placeholder = managed.Placeholder; runtime.PlaceholderMesh = graphics.RegisterMesh(managed.Placeholder);
                runtime.PropMeshIds = new NativeArray<BatchMeshID>(config.PropMeshes.Length, Allocator.Persistent);
                runtime.PropMeshBounds = new NativeArray<AABB>(config.PropMeshes.Length, Allocator.Persistent);
                for (int i = 0; i < config.PropMeshes.Length; i++)
                {
                    var mesh = config.PropMeshes[i].Value; if (!mesh) continue;
                    runtime.PropMeshIds[i] = graphics.RegisterMesh(mesh);
                    runtime.PropMeshBounds[i] = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents };
                }
                var orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
                runtime.Shapes = BrickShapeTablesBuilder.Build(ref orientations.Value);
                var matrices = new Matrix4x4[24];
                for (int i = 0; i < matrices.Length; i++) matrices[i] = (float4x4)new float4x4(orientations.Value.Rotations[i], float3.zero);
                Shader.SetGlobalMatrixArray("_OrientMatrices", matrices);
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline && config.Lod1Distance < pipeline.shadowDistance)
                    GameLog.Warn("Voxel", "LOD1 distance is below the URP shadow distance.");
                runtime.IsReady = 1; return true;
            }
            catch
            {
                failedRegistration = 1; // Keep partial resources owned for shutdown; never leak a new set on every frame.
                throw;
            }
        }
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (applied.IsCreated) applied.Dispose();
            var managed = owned.Value;
            if (managed) { managed.Dispose(); if (Application.isPlaying) Object.Destroy(managed); else Object.DestroyImmediate(managed); }
        }
    }
}
