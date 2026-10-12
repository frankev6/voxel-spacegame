using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct BrickMeshBatch : IComponentData
    {
        public NativeList<BrickMeshWork> Work;
        public NativeArray<uint> Snapshots;
        public NativeArray<BrickPropInstance> Props;
        public NativeArray<BrickMeshResult> Results;
        public JobHandle Handle;
        public byte InFlight;
        public uint ScheduledFrame;
        public float AvgApplyMicros;
        public static BrickMeshBatch Create(int maxBricks = 128) => new BrickMeshBatch
        {
            Work = new NativeList<BrickMeshWork>(maxBricks, Allocator.Persistent),
            Snapshots = new NativeArray<uint>(maxBricks * MeshCell.SnapCells, Allocator.Persistent),
            Props = new NativeArray<BrickPropInstance>(maxBricks * 512, Allocator.Persistent),
            Results = new NativeArray<BrickMeshResult>(maxBricks, Allocator.Persistent),
            AvgApplyMicros = 15,
        };
        /// <summary>Only call with no batch in flight. Preserve the running apply-time estimate when resizing.</summary>
        public void EnsureCapacity(int maxBricks)
        {
            if (Results.Length >= maxBricks) return;
            ReleaseResults();
            Snapshots.Dispose(); Props.Dispose(); Results.Dispose();
            Snapshots = new NativeArray<uint>(maxBricks * MeshCell.SnapCells, Allocator.Persistent);
            Props = new NativeArray<BrickPropInstance>(maxBricks * 512, Allocator.Persistent);
            Results = new NativeArray<BrickMeshResult>(maxBricks, Allocator.Persistent);
            Work.Capacity = maxBricks;
        }
        /// <summary>Complete the independent job before disposing even results whose entities have died.</summary>
        public void ReleaseResults()
        {
            Handle.Complete();
            if (!Results.IsCreated) return;
            for (int i = 0; i < Results.Length; i++) { var result = Results[i]; result.Dispose(); Results[i] = default; }
        }
        public void Dispose()
        {
            ReleaseResults();
            if (Results.IsCreated) Results.Dispose();
            if (Work.IsCreated) Work.Dispose();
            if (Snapshots.IsCreated) Snapshots.Dispose();
            if (Props.IsCreated) Props.Dispose();
        }
    }
}
