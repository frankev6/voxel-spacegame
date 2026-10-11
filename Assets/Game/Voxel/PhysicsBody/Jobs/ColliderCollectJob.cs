using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Serial request-order collection; no job retains references to live masks (SPEC-12 §5.7).</summary>
    [BurstCompile]
    internal struct ColliderCollectJob : IJob
    {
        public NativeList<ColliderRequest> Requests;
        public ColliderCacheContainers Cache;
        [ReadOnly] public BufferLookup<BrickSolidMask> Masks;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public EntityStorageInfoLookup Entities;
        public uint Tick;
        public int Delay, Bucket;
        public void Execute()
        {
            int dropped = 0;
            for (int i = 0; i < Requests.Length; i++)
            {
                var request = Requests[i];
                if (!Cache.GridIndex.ContainsKey(request.Serial)) { dropped++; continue; }
                BrickMaskBits mask = default;
                if (Entities.Exists(request.Brick) && Bricks.HasComponent(request.Brick)
                    && Bricks[request.Brick].Grid == request.Grid && Masks.HasBuffer(request.Brick))
                {
                    var source = Masks[request.Brick];
                    if (source.Length == BrickMath.MaskWords) mask.CopyFrom(source);
                }
                uint installTick = ColliderInstallSchedule.InstallTick(request.Priority, Tick, Delay, Bucket);
                int update = ColliderPipeline.GetOrCreateUpdate(ref Cache, request.Serial, installTick, Tick, request.Priority);
                ColliderPipeline.SetSnapshot(ref Cache, update, request.Coord, mask, mask.IsEmpty());
            }
            Requests.Clear();
            var stats = Cache.Stats.Value; stats.DroppedRequests += dropped; Cache.Stats.Value = stats;
        }
    }
}
