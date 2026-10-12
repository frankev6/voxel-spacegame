using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Rendering
{
    /// <summary>Snapshot-only cross-frame meshing (SPEC-11 §5.4/5.10). Each index owns disjoint output lists.</summary>
    [BurstCompile]
    internal unsafe struct BrickMeshJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<uint> Snapshots;
        [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
        public NativeArray<BrickMeshWork> Work;
        public NativeArray<BrickMeshResult> Results;
        public void Execute(int index)
        {
            var work = Work[index];
            using var vertices = new NativeList<PackedBrickVertex>(Allocator.Temp);
            using var indices = new NativeList<uint>(Allocator.Temp);
            var snapshot = Snapshots.GetSubArray(index * MeshCell.SnapCells, MeshCell.SnapCells);
            if (work.Lod == 0) BrickMesher.MeshLod0(snapshot, ref Shapes.Value, vertices, indices, out work.BoundsMin, out work.BoundsMax);
            else BrickMesher.MeshLod1(snapshot, ref Shapes.Value, vertices, indices, out work.BoundsMin, out work.BoundsMax);
            var result = new BrickMeshResult
            {
                Vertices = new UnsafeList<PackedBrickVertex>(vertices.Length, Allocator.Persistent),
                Indices = new UnsafeList<uint>(indices.Length, Allocator.Persistent)
            };
            result.Vertices.AddRange(vertices.GetUnsafeReadOnlyPtr(), vertices.Length);
            result.Indices.AddRange(indices.GetUnsafeReadOnlyPtr(), indices.Length);
            work.VertexCount = vertices.Length; work.IndexCount = indices.Length;
            Results[index] = result; Work[index] = work;
        }
    }
}
