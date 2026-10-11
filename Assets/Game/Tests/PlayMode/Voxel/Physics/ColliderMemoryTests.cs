using System.Collections;
using Game.Tests.Common.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Physics;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Voxel.Physics.PhysicsScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Voxel.Physics.Perf
{
    internal struct ColliderMemorySample
    {
        public long Installed, Cached, Pending, Retired, Batch;
        public long Total => Installed + Cached + Pending + Retired + Batch;
    }
    /// <summary>Samples all blob owners before deferred disposal can hide same-frame overlap. Completion is outside the fixed step.</summary>
    [DisableAutoCreation, UpdateInGroup(typeof(PresentationSystemGroup)), UpdateAfter(typeof(GridColliderCleanupSystem)), UpdateBefore(typeof(DeferredDisposalSystem))]
    internal partial struct ColliderMemorySampleSystem : ISystem
    {
        public bool Capture;
        public NativeList<ColliderMemorySample> Samples;
        public void OnCreate(ref SystemState state) { Samples = new NativeList<ColliderMemorySample>(512, Allocator.Persistent); state.RequireForUpdate<ColliderBuildCache>(); }
        public void OnDestroy(ref SystemState state) => Samples.Dispose();
        public void OnUpdate(ref SystemState state)
        {
            if (!Capture) return;
            var cache = SystemAPI.GetSingleton<ColliderBuildCache>(); cache.CompleteAccess();
            var batch = SystemAPI.GetSingleton<ColliderBatchQueue>(); batch.State.Value.Handle.Complete();
            var sample = new ColliderMemorySample(); var seen = new NativeHashSet<ulong>(2048, Allocator.Temp);
            try
            {
                foreach (var grid in cache.C.Grids)
                {
                    Add(grid.Installed, ref sample.Installed, ref seen);
                    for (int i = 0; i < grid.Children.Length; i++) Add(grid.Children[i].Blob, ref sample.Cached, ref seen);
                }
                foreach (var update in cache.C.Updates) Add(update.Compound, ref sample.Pending, ref seen);
                foreach (var blob in cache.C.Retired) Add(blob, ref sample.Retired, ref seen);
                foreach (var blob in SystemAPI.GetSingleton<DeferredDisposalQueue>().Blobs) Add(blob.Blob, ref sample.Retired, ref seen);
                foreach (var result in batch.B.Results) Add(result.Blob, ref sample.Batch, ref seen);
                foreach (var result in batch.B.AssemblyResults) Add(result.Compound, ref sample.Batch, ref seen);
                Samples.Add(sample);
            }
            finally { seen.Dispose(); }
        }
        static unsafe void Add(BlobAssetReference<Collider> blob, ref long bytes, ref NativeHashSet<ulong> seen)
        {
            if (!blob.IsCreated) return;
            Assert.IsTrue(seen.Add((ulong)blob.GetUnsafePtr()), "A blob has two live owners"); bytes += blob.Value.MemorySize;
        }
    }

    public sealed class ColliderMemoryTests
    {
        [UnityTest, Performance, Category("Performance")]
        public IEnumerator HollowStation1M_Under3_5MB()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(typeof(ColliderMemorySampleSystem)); Entity root = f.Spawn(station: true); f.Ready(root);
            for (int i = 0; i < 60; i++) { f.Tick(); yield return null; }
            var handle = f.Test.World.GetExistingSystem<ColliderMemorySampleSystem>();
            f.Test.World.Unmanaged.GetUnsafeSystemRef<ColliderMemorySampleSystem>(handle).Capture = true;
            for (int frame = 0; frame < 300; frame++)
            {
                // Four consecutive damage buckets exercise simultaneous installed, pending, batch and deferred ownership.
                if (frame < 16 && frame % 4 == 0)
                {
                    int n = frame / 4;
                    for (int z = 8; z < 12; z++) for (int y = 8 + n * 8; y < 12 + n * 8; y++) for (int x = 0; x < 4; x++)
                        PhysicsPerfDamage.Remove(f, root, new int3(x, y, z));
                    PhysicsPerfDamage.Finish(f, root);
                }
                f.Tick(); yield return null;
            }
            var samples = f.Test.World.Unmanaged.GetUnsafeSystemRef<ColliderMemorySampleSystem>(handle).Samples;
            Assert.AreEqual(300, samples.Length); var timeline = new double[samples.Length]; ColliderMemorySample peak = default;
            for (int i = 0; i < samples.Length; i++) { timeline[i] = samples[i].Total; if (samples[i].Total > peak.Total) peak = samples[i]; }
            TestContext.WriteLine($"Station collider blob peak {peak.Total} B: installed {peak.Installed}, cached {peak.Cached}, pending {peak.Pending}, retired {peak.Retired}, batch {peak.Batch}; final steady {samples[samples.Length - 1].Total} B. Native pool capacity and voxel storage are separate from blob MemorySize.");
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("ColliderPeakBytes", new[] { (double)peak.Total }, "bytes")
                .AddSamples("OwnedBlobTimeline", timeline, "bytes");
            run.Write("ColliderPeakBytes", 3.5 * 1024 * 1024); run.AssertMax("ColliderPeakBytes", 3.5 * 1024 * 1024);
        }
    }
}
