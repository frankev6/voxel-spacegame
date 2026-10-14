using System;
using Game.ShipEditor.Services;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.ShipEditor.Rendering
{
    /// <summary>SPEC-51 §5.13: one reused mesh and at most one independently owned snapshot job.</summary>
    internal sealed class BrickRenderSlot : IDisposable
    {
        internal readonly int3 Coordinate;
        internal Mesh Mesh { get; private set; }
        internal bool Empty { get; private set; } = true;
        internal bool InFlight { get; private set; }
        internal int ScheduledRevision { get; private set; }
        internal int AppliedRevision { get; private set; }
        internal JobHandle Handle { get; private set; }
        NativeArray<uint> snapshot;
        NativeArray<float3> bounds;
        NativeList<PackedBrickVertex> vertices;
        NativeList<uint> indices;
        bool disposed;
        internal BrickRenderSlot(int3 coordinate)
        {
            Coordinate = coordinate;
            Mesh = new Mesh { name = "Editor brick " + coordinate, hideFlags = HideFlags.DontSave };
            Mesh.MarkDynamic();
        }
        internal void Schedule(EditSession session, EditorContext content, BlobAssetReference<BrickShapeTables> shapes, int maxCellY)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BrickRenderSlot));
            if (InFlight) throw new InvalidOperationException("EDT: brick mesh job is already in flight.");
            try
            {
                snapshot = new NativeArray<uint>(MeshCell.SnapCells, Allocator.TempJob);
                bounds = new NativeArray<float3>(2, Allocator.TempJob);
                vertices = new NativeList<PackedBrickVertex>(Allocator.TempJob);
                indices = new NativeList<uint>(Allocator.TempJob);
                session.Draft.Pin();
                try
                {
                    BrickSnapshotUtility.FromStructure(session.Draft.Data, Coordinate,
                        ref content.Definitions.Value, ref content.Orientations.Value, snapshot);
                }
                finally { session.Draft.Unpin(); }
                if (maxCellY != int.MaxValue)
                    for (int z = -1; z <= 16; z++)
                        for (int y = -1; y <= 16; y++)
                            if ((long)Coordinate.y * 16 + y > maxCellY)
                                for (int x = -1; x <= 16; x++) snapshot[MeshCell.SnapIndex(x, y, z)] = 0;
                ScheduledRevision = session.Revision;
                Handle = new MeshJob { Snapshot = snapshot, Shapes = shapes, Vertices = vertices, Indices = indices, Bounds = bounds }.Schedule();
                InFlight = true;
            }
            catch { ReleaseBuffers(); throw; }
        }
        internal bool CompleteAndApply()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BrickRenderSlot));
            if (!InFlight) return false;
            Handle.Complete();
            try
            {
                Empty = vertices.Length == 0 || indices.Length == 0;
                if (Empty) Mesh.Clear();
                else
                {
                    // Matches the existing Game/Voxel/BrickLit packed-vertex shader, without decoding/repacking geometry.
                    var meshBounds = new Bounds((Vector3)((bounds[0] + bounds[1]) * .5f), (Vector3)(bounds[1] - bounds[0]));
                    var writable = UnityEngine.Mesh.AllocateWritableMeshData(1);
                    bool applied = false;
                    try
                    {
                        var data = writable[0];
                        data.SetVertexBufferParams(vertices.Length,
                            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.UNorm8, 4),
                            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4));
                        data.GetVertexData<PackedBrickVertex>().CopyFrom(vertices.AsArray());
                        data.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                        data.GetIndexData<uint>().CopyFrom(indices.AsArray());
                        data.subMeshCount = 1;
                        data.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length) { vertexCount = vertices.Length, bounds = meshBounds }, MeshUpdateFlags.DontRecalculateBounds);
                        UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(writable, Mesh, MeshUpdateFlags.DontRecalculateBounds);
                        applied = true; Mesh.bounds = meshBounds;
                    }
                    finally { if (!applied) writable.Dispose(); }
                }
                AppliedRevision = ScheduledRevision; return true;
            }
            finally { InFlight = false; Handle = default; ReleaseBuffers(); }
        }
        void ReleaseBuffers()
        {
            if (snapshot.IsCreated) snapshot.Dispose(); if (bounds.IsCreated) bounds.Dispose();
            if (vertices.IsCreated) vertices.Dispose(); if (indices.IsCreated) indices.Dispose();
            snapshot = default; bounds = default; vertices = default; indices = default;
        }
        public void Dispose()
        {
            if (disposed) return;
            Handle.Complete(); InFlight = false; ReleaseBuffers();
            if (Mesh != null)
            { if (Application.isPlaying) UnityEngine.Object.Destroy(Mesh); else UnityEngine.Object.DestroyImmediate(Mesh); Mesh = null; }
            disposed = true;
        }
        [BurstCompile]
        struct MeshJob : IJob
        {
            [ReadOnly] public NativeArray<uint> Snapshot;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public NativeArray<float3> Bounds;
            public void Execute()
            {
                BrickMesher.MeshLod0(Snapshot, ref Shapes.Value, Vertices, Indices, out var min, out var max);
                Bounds[0] = min; Bounds[1] = max;
            }
        }
    }
}
