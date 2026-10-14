using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace Game.Voxel.Grid
{
    /// <summary>Fills a batch with one destination entity per iteration (SPEC-10 §5.7).</summary>
    [BurstCompile]
    internal struct FunctionalFillJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<int> Indices;
        [ReadOnly] public NativeArray<BlockInstance> Instances;
        [ReadOnly] public NativeArray<BatchMeshID> Meshes;
        [ReadOnly] public NativeArray<AABB> Bounds;
        public BlobAssetReference<BlockDefsBlob> Blocks;
        public BlobAssetReference<OrientationTablesBlob> Orientations;
        public Entity Root;
        public float CellSize;
        public byte Render;
        public BatchMaterialID Material;
        // Fresh distinct entities, never the same lookup destination in two iterations.
        [NativeDisableParallelForRestriction] public ComponentLookup<FunctionalBlock> Functional;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<Parent> Parents;
        [NativeDisableParallelForRestriction] public ComponentLookup<MaterialMeshInfo> MeshInfo;
        [NativeDisableParallelForRestriction] public ComponentLookup<RenderBounds> RenderBounds;
        [NativeDisableParallelForRestriction] public ComponentLookup<Game.Voxel.Rendering.PropLayer> Layers;
        public void Execute(int i)
        {
            Entity e = Entities[i]; int index = Indices[i]; var instance = Instances[index];
            Functional[e] = new FunctionalBlock { Grid = Root, InstanceIndex = index };
            Transforms[e] = LocalTransform.FromPositionRotationScale(((float3)instance.Origin + .5f) * CellSize,
                Orientations.Value.Rotations[instance.Orient], CellSize);
            Parents[e] = new Parent { Value = Root };
            if (Render == 0) return;
            int mesh = Blocks.Value.Blocks[instance.Block].PropMeshIndex;
            Layers[e] = new Game.Voxel.Rendering.PropLayer { Value = Blocks.Value.Blocks[instance.Block].MaterialLayer };
            bool valid = (uint)mesh < (uint)Meshes.Length;
            if (valid)
            {
                MeshInfo[e] = new MaterialMeshInfo(Material, Meshes[mesh]);
                RenderBounds[e] = new RenderBounds { Value = Bounds[mesh] };
            }
            MeshInfo.SetComponentEnabled(e, valid);
        }
    }
}
