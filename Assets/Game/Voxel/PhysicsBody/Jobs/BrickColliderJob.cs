using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Builds independent owned blobs from batch masks; no live ECS access (SPEC-12 §5.8).</summary>
    [BurstCompile]
    internal struct BrickColliderJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BrickBuildItem> Items;
        [ReadOnly] public NativeArray<BrickMaskBits> Masks;
        public NativeArray<BrickBuildResult> Results;
        public int MaxBoxes;
        public float Bevel;
        public void Execute(int index)
        {
            var item = Items[index];
            if (item.Removed != 0) { Results[index] = new BrickBuildResult { Removed = 1 }; return; }
            var scratch = new NativeList<BrickBox>(64, Allocator.Temp);
            try
            {
                bool preview = item.EditorPreview != 0;
                var blob = BrickColliderBuilder.Build(Masks[index], item.CellSize, MaxBoxes, Bevel,
                    preview ? PhysicsLayers.EditorPreview : PhysicsLayers.Grid,
                    preview ? PhysicsLayers.EditorPreviewMaterial : PhysicsLayers.GridMaterial, ref scratch, out int boxes);
                Results[index] = new BrickBuildResult { Blob = blob, BoxCount = boxes, Removed = blob.IsCreated ? (byte)0 : (byte)1 };
            }
            finally { scratch.Dispose(); }
        }
    }
}
