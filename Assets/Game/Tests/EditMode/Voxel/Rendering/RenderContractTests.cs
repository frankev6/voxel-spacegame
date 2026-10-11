using System.Reflection;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>REN-001 contract layouts and allocation ownership (SPEC-11 §4).</summary>
    public sealed class RenderContractTests
    {
        struct QueueWriteJob : IJob
        {
            public Unity.Collections.NativeList<BrickMeshRequest> Requests;
            public void Execute() { Requests.Add(default); }
        }
        struct BatchWriteJob : IJob
        {
            public Unity.Collections.NativeArray<uint> Snapshots;
            public void Execute() { Snapshots[0] = 1; }
        }

        [Test]
        public void StructSizes_MatchSpec()
        {
            Assert.AreEqual(8, UnsafeUtility.SizeOf<PackedBrickVertex>());
            Assert.AreEqual(52, UnsafeUtility.SizeOf<ShapeFace>(), "SPEC-11 Q11: four bytes + four int3 fields");
            Assert.AreEqual(6, UnsafeUtility.SizeOf<BrickPropInstance>());
            Assert.AreEqual(0, typeof(BrickPropInstance).GetCustomAttribute<InternalBufferCapacityAttribute>().Capacity);
            Assert.IsTrue(typeof(IEnableableComponent).IsAssignableFrom(typeof(Dormant)));
        }

        [Test]
        public void QueueCreateDispose_NoLeaks()
        {
            // Warm up job reflection and containers before starting the native allocation check.
            var warmQueue = BrickMeshQueue.Create();
            warmQueue.Release(new QueueWriteJob { Requests = warmQueue.Damage }.Schedule(warmQueue.Acquire(default)));
            warmQueue.Dispose();
            var warmBatch = BrickMeshBatch.Create(1);
            warmBatch.Handle = new BatchWriteJob { Snapshots = warmBatch.Snapshots }.Schedule();
            warmBatch.Dispose();
            UnsafeUtility.CheckForLeaks();
            var queue = BrickMeshQueue.Create();
            var batch = BrickMeshBatch.Create();
            try
            {
                Assert.AreEqual(1024, queue.Damage.Capacity);
                Assert.AreEqual(1024, queue.Neighbour.Capacity);
                Assert.AreEqual(1024, queue.Spawn.Capacity);
                Assert.AreEqual(1024, queue.Lod.Capacity);
                Assert.AreEqual(4096, queue.Pending.Capacity);
                Assert.AreEqual(4, queue.Heads.Length);
                for (int i = 0; i < 4; i++) Assert.AreEqual(0, queue.Heads[i]);
                Assert.AreEqual(128, batch.Work.Capacity);
                Assert.AreEqual(128 * 5832, batch.Snapshots.Length);
                Assert.AreEqual(128 * 512, batch.Props.Length);
                Assert.AreEqual(15, batch.AvgApplyMicros);
                Assert.AreEqual(0, batch.InFlight);
                queue.Release(new QueueWriteJob { Requests = queue.Damage }.Schedule(queue.Acquire(default)));
                batch.Handle = new BatchWriteJob { Snapshots = batch.Snapshots }.Schedule();
            }
            finally { queue.Dispose(); batch.Dispose(); }
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
        }
    }
}
