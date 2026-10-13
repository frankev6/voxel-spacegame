using Game.Core.Systems;
using Game.Voxel.Damage;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>DMG-011: draws up to 32,000 chunk cells with the existing palette-lit instanced voxel material.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(BrickPoolRenderSystem))]
    public partial struct ChunkRenderSystem : ISystem
    {
        UnityObjectRef<ChunkRenderResources> owned;
        NativeArray<PropInstanceGpu> scratch;
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VoxelRenderRuntime>(); state.RequireForUpdate<VoxelRenderCamera>();
            scratch = new NativeArray<PropInstanceGpu>(32000, Allocator.Persistent);
        }
        public void OnUpdate(ref SystemState state)
        {
            var runtime = SystemAPI.GetSingleton<VoxelRenderRuntime>();
            var camera = SystemAPI.GetSingleton<VoxelRenderCamera>().Value.Value;
            if (runtime.IsReady == 0 || !camera || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var resources = owned.Value;
            if (!resources)
            {
                resources = ScriptableObject.CreateInstance<ChunkRenderResources>(); resources.hideFlags = HideFlags.DontSave;
                resources.Cube = ChunkRenderResources.CreateCube(); owned = resources;
            }
            int budget = scratch.Length;
            foreach (var (body, transform, cells, dissolve) in SystemAPI.Query<RefRO<ChunkBody>, RefRO<LocalToWorld>, DynamicBuffer<ChunkCell>, RefRO<ChunkDissolve>>())
            {
                if (body.ValueRO.Live == 0 || cells.Length == 0 || budget == 0) continue;
                int count = math.min(cells.Length, budget), slot = body.ValueRO.Slot; budget -= count;
                float size = body.ValueRO.CellSize; float3 offset = body.ValueRO.LocalOrigin / size;
                for (int i = 0; i < count; i++)
                {
                    var cell = cells[i]; scratch[i] = new PropInstanceGpu
                    { LocalCellCenter = offset + new float3(cell.X, cell.Y, cell.Z) + .5f, Packed = (uint)(cell.Orient | (cell.Color << 5)) };
                }
                resources.Ensure(slot, count); resources.Cells[slot].SetData(scratch, 0, 0, count);
                float4x4 matrix = math.mul(transform.ValueRO.Value, float4x4.Scale(size));
                resources.Matrix[0] = matrix; resources.Matrices[slot].SetData(resources.Matrix);
                var properties = resources.Properties[slot]; properties.SetBuffer("_PropInstances", resources.Cells[slot]);
                properties.SetBuffer("_GridMatrices", resources.Matrices[slot]); properties.SetFloat("_Dissolve01", dissolve.ValueRO.Value);
                // Conservative sphere around COM, including translated cell origins and body scale.
                float radius = 0;
                for (int i = 0; i < count; i++) radius = math.max(radius, math.length(scratch[i].LocalCellCenter) + .866026f);
                radius *= math.length(matrix.c0.xyz);
                var parameters = new RenderParams(runtime.Managed.Value.PropMaterial)
                {
                    camera = camera, matProps = properties, worldBounds = new Bounds(transform.ValueRO.Value.c3.xyz, Vector3.one * (2 * radius)),
                    shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, renderingLayerMask = 1
                };
                Graphics.RenderMeshPrimitives(parameters, resources.Cube, 0, count);
            }
        }
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency(); if (scratch.IsCreated) scratch.Dispose();
            var resources = owned.Value;
            if (resources) { resources.Dispose(); if (Application.isPlaying) Object.Destroy(resources); else Object.DestroyImmediate(resources); }
        }
    }
}
