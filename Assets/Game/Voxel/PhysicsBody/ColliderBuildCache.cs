using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public struct ColliderBuildCache : IComponentData
    {
        public Game.Data.Streams.GuardedHandles Access;
        internal ColliderCacheContainers C;
        public static ColliderBuildCache Create() => new ColliderBuildCache
        {
            Access = Game.Data.Streams.GuardedHandles.Create(),
            C = new ColliderCacheContainers
            {
                GridIndex = new NativeHashMap<uint, int>(256, Allocator.Persistent),
                Grids = new NativeList<GridColliderRecord>(256, Allocator.Persistent),
                Updates = new NativeList<ColliderUpdate>(256, Allocator.Persistent),
                FreeUpdates = new NativeList<int>(256, Allocator.Persistent),
                Collecting = new NativeHashMap<ulong, int>(256, Allocator.Persistent),
                CollectingBricks = new NativeHashMap<CollectKey, int>(256, Allocator.Persistent),
                Masks = new NativeList<BrickMaskBits>(256, Allocator.Persistent),
                FreeMasks = new NativeList<int>(256, Allocator.Persistent),
                Retired = new NativeList<BlobAssetReference<Collider>>(256, Allocator.Persistent),
                Stats = new NativeReference<ColliderPipelineStats>(Allocator.Persistent),
                NextCreateOrder = new NativeReference<uint>(Allocator.Persistent),
            },
        };
        public void Dispose()
        {
            Access.Dispose();
            // §4.7: each blob has exactly one owner. Children are copied into compounds by Unity Physics.
            if (C.Grids.IsCreated)
            {
                for (int i = 0; i < C.Grids.Length; i++)
                {
                    var record = C.Grids[i];
                    if (record.Installed.IsCreated) record.Installed.Dispose();
                    if (record.Children.IsCreated)
                    {
                        for (int j = 0; j < record.Children.Length; j++)
                        {
                            var child = record.Children[j].Blob;
                            if (child.IsCreated) child.Dispose();
                        }
                        record.Children.Dispose();
                    }
                    if (record.Updates.IsCreated) record.Updates.Dispose();
                }
                C.Grids.Dispose();
            }
            if (C.Updates.IsCreated)
            {
                for (int i = 0; i < C.Updates.Length; i++)
                {
                    var update = C.Updates[i];
                    if (update.Compound.IsCreated) update.Compound.Dispose();
                    if (update.Bricks.IsCreated) update.Bricks.Dispose();
                }
                C.Updates.Dispose();
            }
            if (C.Retired.IsCreated)
            {
                for (int i = 0; i < C.Retired.Length; i++)
                {
                    var blob = C.Retired[i];
                    if (blob.IsCreated) blob.Dispose();
                }
                C.Retired.Dispose();
            }
            if (C.GridIndex.IsCreated) C.GridIndex.Dispose();
            if (C.FreeUpdates.IsCreated) C.FreeUpdates.Dispose();
            if (C.Collecting.IsCreated) C.Collecting.Dispose();
            if (C.CollectingBricks.IsCreated) C.CollectingBricks.Dispose();
            if (C.Masks.IsCreated) C.Masks.Dispose();
            if (C.FreeMasks.IsCreated) C.FreeMasks.Dispose();
            if (C.Stats.IsCreated) C.Stats.Dispose();
            if (C.NextCreateOrder.IsCreated) C.NextCreateOrder.Dispose();
        }
        public JobHandle Acquire(JobHandle dependsOn) => Access.Acquire(dependsOn);
        public void Release(JobHandle lastJob) => Access.Release(lastJob);
        public void CompleteAccess() => Access.CompleteAccess();
    }
}
