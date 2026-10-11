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
        public JobHandle Handle;
        public byte InFlight;
        public uint ScheduledFrame;
        public float AvgApplyMicros;
        public static BrickMeshBatch Create(int maxBricks = 128) => new BrickMeshBatch
        {
            Work = new NativeList<BrickMeshWork>(maxBricks, Allocator.Persistent),
            Snapshots = new NativeArray<uint>(maxBricks * MeshCell.SnapCells, Allocator.Persistent),
            Props = new NativeArray<BrickPropInstance>(maxBricks * 512, Allocator.Persistent),
            AvgApplyMicros = 15,
        };
        public void Dispose()
        {
            Handle.Complete();
            if (Work.IsCreated) Work.Dispose();
            if (Snapshots.IsCreated) Snapshots.Dispose();
            if (Props.IsCreated) Props.Dispose();
        }
    }
}
