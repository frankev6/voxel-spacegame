using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxel.Rendering
{
    internal struct BrickPoolItem
    {
        public PoolRange Vertices, Indices;
        public AABB Bounds;
        public int VertexCount, IndexCount;
        public uint Generation;
        public byte Allocated, Visible;
    }
    internal struct BrickCullSummary
    {
        public float3 BoundsMin, BoundsMax;
        public int VisibleCount;
    }
    /// <summary>Derived visibility and upload-cache writer; never mutates simulation or applied geometry.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct BrickCullJob : IJob
    {
        [ReadOnly] public NativeArray<BrickPoolItem> Items;
        [ReadOnly] public NativeArray<float4x4> WorldMatrices;
        [ReadOnly] public NativeArray<float4> Planes;
        public NativeArray<float4x4> UploadedMatrices;
        public NativeList<GraphicsBuffer.IndirectDrawIndexedArgs> Arguments;
        public NativeReference<BrickCullSummary> Summary;
        public NativeList<int2> MatrixRuns;
        public NativeList<PoolRange> VisibleRanges;
        public float3 CameraPosition;
        public float ShadowDistanceSquared;
        struct RangeComparer : IComparer<PoolRange>
        { public int Compare(PoolRange a, PoolRange b) => a.Start.CompareTo(b.Start); }
        public void Execute()
        {
            Arguments.Clear(); MatrixRuns.Clear(); VisibleRanges.Clear();
            var result = new BrickCullSummary { BoundsMin = float.PositiveInfinity, BoundsMax = float.NegativeInfinity };
            for (int slot = 1; slot < Items.Length; slot++)
            {
                var item = Items[slot]; if (item.Allocated == 0) continue;
                var matrix = WorldMatrices[slot]; var previous = UploadedMatrices[slot];
                if (math.any(matrix.c0 != previous.c0) || math.any(matrix.c1 != previous.c1) || math.any(matrix.c2 != previous.c2) || math.any(matrix.c3 != previous.c3))
                {
                    UploadedMatrices[slot] = matrix;
                    int last = MatrixRuns.Length - 1;
                    if (last >= 0 && MatrixRuns[last].y == slot) { var run = MatrixRuns[last]; run.y++; MatrixRuns[last] = run; }
                    else MatrixRuns.Add(new int2(slot, slot + 1));
                }
                if (item.Visible == 0 || item.IndexCount == 0) continue;
                float3 center = math.transform(matrix, item.Bounds.Center);
                float3 extent = math.abs(matrix.c0.xyz) * item.Bounds.Extents.x + math.abs(matrix.c1.xyz) * item.Bounds.Extents.y + math.abs(matrix.c2.xyz) * item.Bounds.Extents.z;
                bool visible = true;
                for (int p = 0; p < 6; p++)
                {
                    float4 plane = Planes[p];
                    if (math.dot(plane.xyz, center) + plane.w + math.dot(math.abs(plane.xyz), extent) < 0) { visible = false; break; }
                }
                float3 delta = math.max(math.abs(center - CameraPosition) - extent, 0);
                if (!visible && (ShadowDistanceSquared <= 0 || math.lengthsq(delta) > ShadowDistanceSquared)) continue;
                VisibleRanges.Add(new PoolRange { Start = item.Indices.Start, Count = item.IndexCount }); result.VisibleCount++;
                result.BoundsMin = math.min(result.BoundsMin, center - extent); result.BoundsMax = math.max(result.BoundsMax, center + extent);
            }
            VisibleRanges.Sort(new RangeComparer());
            for (int i = 0; i < VisibleRanges.Length; i++)
            {
                var range = VisibleRanges[i]; int last = Arguments.Length - 1;
                if (last >= 0 && Arguments[last].startIndex + Arguments[last].indexCountPerInstance == (uint)range.Start)
                { var command = Arguments[last]; command.indexCountPerInstance += (uint)range.Count; Arguments[last] = command; }
                else Arguments.Add(new GraphicsBuffer.IndirectDrawIndexedArgs { startIndex = (uint)range.Start, indexCountPerInstance = (uint)range.Count, instanceCount = 1 });
            }
            Summary.Value = result;
        }
    }
}
