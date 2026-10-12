using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Rendering
{
    /// <summary>O(grids) transforms and conservative bounds, including movement without a prop rebuild.</summary>
    [BurstCompile]
    internal struct GridMatrixJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Roots;
        [ReadOnly] public NativeArray<AABB> LocalBounds;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
        public NativeArray<float4x4> Matrices;
        public NativeArray<AABB> Bounds;
        public void Execute()
        {
            float3 min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i < Roots.Length; i++)
            {
                var matrix = math.mul(Transforms[Roots[i]].Value, float4x4.Scale(Grids[Roots[i]].CellSize));
                Matrices[i] = matrix;
                var local = LocalBounds[i]; float3 center = math.transform(matrix, local.Center);
                float3 extent = math.abs(matrix.c0.xyz) * local.Extents.x + math.abs(matrix.c1.xyz) * local.Extents.y + math.abs(matrix.c2.xyz) * local.Extents.z;
                min = math.min(min, center - extent); max = math.max(max, center + extent);
            }
            Bounds[0] = Roots.Length == 0 ? default : new AABB { Center = (min + max) * .5f, Extents = (max - min) * .5f };
        }
    }
}
