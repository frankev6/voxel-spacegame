using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>One instanced draw per prop mesh; cell uploads change-driven, moving transforms O(grids).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(BrickMeshApplySystem))]
    public partial struct PropRenderSystem : ISystem
    {
        EntityQuery bricks, changed, grids;
        ComponentLookup<Brick> brickLookup;
        ComponentLookup<VoxelGrid> gridLookup;
        ComponentLookup<LocalToWorld> transforms;
        BufferLookup<BrickPropInstance> props;
        NativeList<PropEntry> entries;
        NativeList<PropInstanceGpu> instances;
        NativeHashMap<Entity, int> slots;
        NativeList<Entity> roots;
        NativeList<AABB> localBounds;
        NativeList<float4x4> matrices;
        NativeArray<AABB> bounds;
        NativeArray<int> counts, starts;
        UnityObjectRef<VoxelRenderManagedState> owned;
        int brickOrder, gridOrder;
        bool initialized;
        static readonly ProfilerMarker Gather = new ProfilerMarker("PropRenderSystem.Gather");
        static readonly ProfilerMarker Transform = new ProfilerMarker("PropRenderSystem.Matrices");
        static readonly ProfilerMarker Draw = new ProfilerMarker("PropRenderSystem.Draw");
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VoxelRenderConfig>(); state.RequireForUpdate<VoxelRenderRuntime>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<VoxelRenderCamera>();
            bricks = state.GetEntityQuery(ComponentType.ReadOnly<Brick>(), ComponentType.ReadOnly<BrickPropInstance>(), ComponentType.ReadOnly<BrickRenderState>());
            changed = state.GetEntityQuery(ComponentType.ReadOnly<Brick>(), ComponentType.ReadOnly<BrickPropInstance>());
            changed.SetChangedVersionFilter(ComponentType.ReadOnly<BrickPropInstance>());
            grids = state.GetEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalToWorld>());
            brickLookup = state.GetComponentLookup<Brick>(true); gridLookup = state.GetComponentLookup<VoxelGrid>(true);
            transforms = state.GetComponentLookup<LocalToWorld>(true); props = state.GetBufferLookup<BrickPropInstance>(true);
            entries = new NativeList<PropEntry>(1024, Allocator.Persistent); instances = new NativeList<PropInstanceGpu>(1024, Allocator.Persistent);
            slots = new NativeHashMap<Entity, int>(64, Allocator.Persistent); roots = new NativeList<Entity>(64, Allocator.Persistent);
            localBounds = new NativeList<AABB>(64, Allocator.Persistent); matrices = new NativeList<float4x4>(64, Allocator.Persistent);
            bounds = new NativeArray<AABB>(1, Allocator.Persistent);
        }
        public void OnUpdate(ref SystemState state)
        {
            var runtime = SystemAPI.GetSingleton<VoxelRenderRuntime>();
            if (runtime.IsReady == 0) return;
            var camera = SystemAPI.GetSingleton<VoxelRenderCamera>().Value.Value;
            if (!camera) return;
            var config = SystemAPI.GetSingleton<VoxelRenderConfig>(); var managed = runtime.Managed.Value;
            if (!initialized)
            {
                owned = managed;
                counts = new NativeArray<int>(config.PropMeshes.Length, Allocator.Persistent);
                starts = new NativeArray<int>(config.PropMeshes.Length, Allocator.Persistent);
                managed.PropBlocks = new MaterialPropertyBlock[counts.Length];
                managed.PropInstanceBuffers = new GraphicsBuffer[counts.Length];
                for (int i = 0; i < counts.Length; i++) managed.PropBlocks[i] = new MaterialPropertyBlock();
            }
            brickLookup.Update(ref state); gridLookup.Update(ref state); transforms.Update(ref state); props.Update(ref state);
            int nextBrickOrder = bricks.GetCombinedComponentOrderVersion(true), nextGridOrder = grids.GetCombinedComponentOrderVersion(true);
            if (!initialized || nextBrickOrder != brickOrder || nextGridOrder != gridOrder || !changed.IsEmpty)
            {
                using var marker = Gather.Auto();
                using var entities = bricks.ToEntityArray(Allocator.TempJob);
                new PropGatherJob { Entities = entities, Bricks = brickLookup, Grids = gridLookup, Transforms = transforms, Props = props,
                    Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value, Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value,
                    MeshBounds = runtime.PropMeshBounds, Entries = entries, Instances = instances, Slots = slots,
                    Roots = roots, LocalBounds = localBounds, Counts = counts, Starts = starts }.Schedule(state.Dependency).Complete();
                for (int m = 0; m < counts.Length; m++)
                {
                    if (counts[m] == 0) continue;
                    Grow(ref managed.PropInstanceBuffers[m], counts[m], 16);
                    managed.PropInstanceBuffers[m].SetData(instances.AsArray(), starts[m], 0, counts[m]);
                    managed.PropBlocks[m].SetBuffer("_PropInstances", managed.PropInstanceBuffers[m]);
                }
                matrices.ResizeUninitialized(roots.Length);
                managed.PropCounts.Clear(); for (int m = 0; m < counts.Length; m++) managed.PropCounts.Add(counts[m]);
                brickOrder = nextBrickOrder; gridOrder = nextGridOrder; initialized = true;
            }
            if (roots.Length == 0) return;
            using (Transform.Auto())
            {
                new GridMatrixJob { Roots = roots.AsArray(), LocalBounds = localBounds.AsArray(), Grids = gridLookup,
                    Transforms = transforms, Matrices = matrices.AsArray(), Bounds = bounds }.Schedule(state.Dependency).Complete();
                Grow(ref managed.GridMatrices, roots.Length, 64); managed.GridMatrices.SetData(matrices.AsArray());
            }
            using (Draw.Auto())
            {
                // A conservative union is shared by mesh draws; recomputed from local bounds every frame.
                var worldBounds = new Bounds(bounds[0].Center, bounds[0].Extents * 2);
                for (int m = 0; m < counts.Length; m++)
                {
                    if (counts[m] == 0 || !config.PropMeshes[m].Value) continue;
                    managed.PropBlocks[m].SetBuffer("_GridMatrices", managed.GridMatrices);
                    var parameters = new RenderParams(managed.PropMaterial) { camera = camera, matProps = managed.PropBlocks[m],
                        worldBounds = worldBounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, renderingLayerMask = 1,
                        lightProbeUsage = LightProbeUsage.BlendProbes, reflectionProbeUsage = ReflectionProbeUsage.BlendProbes };   // QA-001 ambient
                    Graphics.RenderMeshPrimitives(parameters, config.PropMeshes[m].Value, 0, counts[m]);
                }
            }
        }
        static void Grow(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count >= count) return;
            int capacity = math.max(count, buffer == null ? 16 : buffer.count * 2);
            buffer?.Dispose(); buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride);
        }
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            entries.Dispose(); instances.Dispose(); slots.Dispose(); roots.Dispose(); localBounds.Dispose(); matrices.Dispose(); bounds.Dispose();
            if (counts.IsCreated) counts.Dispose(); if (starts.IsCreated) starts.Dispose();
            if (owned.Value) owned.Value.DisposeProps();
        }
    }
}
