using System.Collections.Generic;
using System.Diagnostics;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Tests.EditMode.Core.SimEventChannelTests.Ev>))]

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-012: SimEventChannel ordering, late registration, stale disposal, misuse and overhead (SPEC-02 §5.13, §8).
    /// Uses its own World (Decision 16): two parallel IJobChunk producers and one consumer.</summary>
    public sealed partial class SimEventChannelTests
    {
        public struct Ev { public ProducerKind Producer; public int Chunk; public int Index; }
        public struct Streams : IComponentData { public SimEventChannel<Ev> Channel; }
        public struct TagA : IComponentData { public int V; }
        public struct TagB : IComponentData { public int V; }

        [BurstCompile]
        struct WriteJob : IJobChunk
        {
            public NativeStream.Writer Writer;
            public ProducerKind Producer;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Writer.BeginForEachIndex(unfilteredChunkIndex);
                for (int i = 0; i < chunk.Count; i++) Writer.Write(new Ev { Producer = Producer, Chunk = unfilteredChunkIndex, Index = i });
                Writer.EndForEachIndex();
            }
        }

        [DisableAutoCreation]
        public partial class Producer : SystemBase
        {
            public ComponentType Tag; public ProducerKind Kind;
            EntityQuery query, streams;
            protected override void OnCreate() { streams = GetEntityQuery(ComponentType.ReadOnly<Streams>()); }
            protected override void OnUpdate()
            {
                if (query == default) query = GetEntityQuery(Tag);
                var channel = streams.GetSingleton<Streams>().Channel;
                int chunks = query.CalculateChunkCountWithoutFiltering();
                var writer = channel.BeginWrite(ref CheckedStateRef, Kind, chunks);
                Dependency = new WriteJob { Writer = writer, Producer = Kind }.ScheduleParallel(query, Dependency);
                channel.EndWrite(Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class Consumer : SystemBase
        {
            public NativeList<Ev> Output;
            public JobHandle LastJob;
            public List<ProducerKind> LastProducers = new List<ProducerKind>();
            EntityQuery streams;
            protected override void OnCreate() { streams = GetEntityQuery(ComponentType.ReadOnly<Streams>()); }
            protected override void OnUpdate()
            {
                var channel = streams.GetSingleton<Streams>().Channel;
                var set = channel.BeginRead(ref CheckedStateRef);
                LastProducers.Clear();
                for (int i = 0; i < set.Count; i++) LastProducers.Add(set.Producers[i]);
                Dependency = SimEventGather.Schedule(set, Output, Dependency);
                channel.EndRead(Dependency);
                LastJob = Dependency;
            }
        }

        World world;
        SimEventChannel<Ev> channel;
        Producer prodA, prodB;
        Consumer consumer;
        int savedWorkers;
        NativeLeakDetectionMode savedLeakMode;
        const int CountA = 3000, CountB = 2000;

        [SetUp]
        public void SetUp()
        {
            savedWorkers = JobsUtility.JobWorkerCount;
            JobsUtility.JobWorkerCount = Mathf.Min(8, JobsUtility.JobWorkerMaximumCount);
            savedLeakMode = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;

            world = new World("sim-event-channel");
            var em = world.EntityManager;
            var streamsEntity = em.CreateEntity(typeof(Streams));
            em.CreateEntity(em.CreateArchetype(typeof(TagA)), CountA, Allocator.Temp).Dispose();
            em.CreateEntity(em.CreateArchetype(typeof(TagB)), CountB, Allocator.Temp).Dispose();
            prodA = world.CreateSystemManaged<Producer>(); prodA.Tag = ComponentType.ReadOnly<TagA>(); prodA.Kind = ProducerKind.Impact;
            prodB = world.CreateSystemManaged<Producer>(); prodB.Tag = ComponentType.ReadOnly<TagB>(); prodB.Kind = ProducerKind.Projectile;
            consumer = world.CreateSystemManaged<Consumer>();
            consumer.Output = new NativeList<Ev>(CountA + CountB, Allocator.Persistent);
            channel = SimEventChannel<Ev>.Create("test");
            em.SetComponentData(streamsEntity, new Streams { Channel = channel });
        }

        [TearDown]
        public void TearDown()
        {
            world.EntityManager.CompleteAllTrackedJobs();
            consumer.LastJob.Complete();
            consumer.Output.Dispose();
            channel.Dispose();
            world.Dispose();
            NativeLeakDetection.Mode = savedLeakMode;
            JobsUtility.JobWorkerCount = savedWorkers;
        }

        List<Ev> Collect()
        {
            consumer.LastJob.Complete();
            var list = new List<Ev>(consumer.Output.Length);
            foreach (var e in consumer.Output) list.Add(e);
            consumer.Output.Clear();
            return list;
        }

        static void AssertGatherOrder(List<Ev> events)
        {
            for (int i = 1; i < events.Count; i++)
            {
                Ev a = events[i - 1], b = events[i];
                bool ordered = a.Producer != b.Producer
                    ? true
                    : a.Chunk < b.Chunk || (a.Chunk == b.Chunk && a.Index + 1 == b.Index);
                Assert.IsTrue(ordered, $"event {i} out of order: {a.Producer}/{a.Chunk}/{a.Index} then {b.Producer}/{b.Chunk}/{b.Index}");
            }
        }

        [Test]
        public void TwoParallelProducers_GatherInRegistrationThenIndexOrder_20Runs()
        {
            List<Ev> first = null;
            for (uint run = 1; run <= 20; run++)
            {
                channel.BeginTick(run);
                prodA.Update(); prodB.Update(); consumer.Update();
                var events = Collect();
                Assert.AreEqual(CountA + CountB, events.Count, $"run {run}");
                CollectionAssert.AreEqual(new[] { ProducerKind.Impact, ProducerKind.Projectile }, consumer.LastProducers);
                for (int i = 0; i < CountA; i++) Assert.AreEqual(ProducerKind.Impact, events[i].Producer);
                for (int i = CountA; i < events.Count; i++) Assert.AreEqual(ProducerKind.Projectile, events[i].Producer);
                AssertGatherOrder(events);
                if (first == null) first = events;
                else CollectionAssert.AreEqual(first, events, $"run {run} differs from run 1");
                Assert.AreEqual(0, channel.PendingCount);
            }
            Assert.Greater(first[CountA - 1].Chunk, 8, "expected many chunks so the parallel writers interleave");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RegistrationAfterBeginRead_ArrivesNextTick()
        {
            channel.BeginTick(1);
            prodA.Update(); consumer.Update(); prodB.Update();
            var tick1 = Collect();
            Assert.AreEqual(CountA, tick1.Count);
            Assert.AreEqual(1, channel.PendingCount);

            channel.BeginTick(2);
            consumer.Update();
            var tick2 = Collect();
            Assert.AreEqual(CountB, tick2.Count);
            Assert.IsTrue(tick2.TrueForAll(e => e.Producer == ProducerKind.Projectile));
            Assert.AreEqual(0, channel.PendingCount);
        }

        [Test]
        public void ConsumerDisabled_StaleRegistrationsDisposed_OneWarning()
        {
            // Warm up so lazy allocations (queries, world update allocator) happen first. CheckForLeaks reports and resets the tracked
            // set, so the second call counts only allocations made by this scenario that are still alive.
            for (uint t = 1; t <= 3; t++) { channel.BeginTick(t); prodA.Update(); prodB.Update(); consumer.Update(); Collect(); }
            world.EntityManager.CompleteAllTrackedJobs();
            UnsafeUtility.CheckForLeaks();
            int staleWarnings = 0;
            Application.LogCallback count = (msg, _, type) => { if (type == LogType.Warning && msg.StartsWith("[Core] stale stream")) staleWarnings++; };
            Application.logMessageReceived += count;
            channel.BeginTick(10);
            prodA.Update(); prodB.Update();
            channel.BeginTick(11);
            Assert.AreEqual(2, channel.PendingCount, "a registration may still be read in the next tick");
            LogAssert.Expect(LogType.Warning, "[Core] stale stream: test");
            channel.BeginTick(12);
            Assert.AreEqual(0, channel.PendingCount, "unread for a full tick: disposed");

            prodA.Update();
            channel.BeginTick(13);
            channel.BeginTick(14);                                      // disposed again, no second warning
            Assert.AreEqual(0, channel.PendingCount);

            Application.logMessageReceived -= count;
            Assert.AreEqual(1, staleWarnings, "one warning per channel");

            world.EntityManager.CompleteAllTrackedJobs();
            channel.Dispose();                                          // completes the disposal jobs
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "every stale stream freed");
        }

        [Test]
        public void BeginWriteTwice_And_BeginReadTwice_Throw()
        {
            ref var state = ref world.Unmanaged.ResolveSystemStateRef(prodA.SystemHandle);
            channel.BeginTick(1);
            channel.BeginWrite(ref state, ProducerKind.Test, 1);
            Assert.Throws<System.InvalidOperationException>(() => channel.BeginWrite(ref world.Unmanaged.ResolveSystemStateRef(prodA.SystemHandle), ProducerKind.Test, 1));
            channel.EndWrite(default);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => channel.BeginWrite(ref world.Unmanaged.ResolveSystemStateRef(prodA.SystemHandle), ProducerKind.Test, 0));

            channel.BeginRead(ref state);
            Assert.Throws<System.InvalidOperationException>(() => channel.BeginRead(ref world.Unmanaged.ResolveSystemStateRef(prodA.SystemHandle)));
            channel.EndRead(default);
            Assert.Throws<System.InvalidOperationException>(() => channel.EndRead(default));
            Assert.AreEqual(0, channel.PendingCount);
        }

        [Test]
        public void PostFromMainThread_IsFirstRegistration()
        {
            channel.PostFromMainThread(new Ev { Producer = ProducerKind.MainThread, Chunk = -1, Index = 0 });
            channel.PostFromMainThread(new Ev { Producer = ProducerKind.MainThread, Chunk = -1, Index = 1 });
            channel.BeginTick(1);
            prodA.Update(); consumer.Update();
            var events = Collect();
            CollectionAssert.AreEqual(new[] { ProducerKind.MainThread, ProducerKind.Impact }, consumer.LastProducers);
            Assert.AreEqual(CountA + 2, events.Count);
            Assert.AreEqual(-1, events[0].Chunk); Assert.AreEqual(0, events[0].Index);
            Assert.AreEqual(-1, events[1].Chunk); Assert.AreEqual(1, events[1].Index);
            Assert.AreEqual(ProducerKind.Impact, events[2].Producer);

            channel.BeginTick(2); consumer.Update();
            Assert.AreEqual(0, Collect().Count, "the inbox is consumed once");
        }

        [TestCase(1, false)]
        [TestCase(8, false)]
        [TestCase(8, true)]
        public void Dispose_CompletesReadCleanup(int cycles, bool stale)
        {
            // No worker can opportunistically finish unowned disposal jobs before the assertion.
            JobsUtility.JobWorkerCount = 0;
            UnsafeUtility.CheckForLeaks();
            ref var state = ref world.Unmanaged.ResolveSystemStateRef(prodA.SystemHandle);
            for (uint i = 0; i < cycles; i++)
            {
                channel.BeginTick(i * 3 + 1);
                var writer = channel.BeginWrite(ref state, ProducerKind.Test, 1);
                writer.BeginForEachIndex(0); writer.Write(new Ev()); writer.EndForEachIndex();
                channel.EndWrite(default);
                if (stale)
                {
                    if (i == 0) LogAssert.Expect(LogType.Warning, "[Core] stale stream: test");
                    channel.BeginTick(i * 3 + 3);
                }
                channel.BeginRead(ref state);
                channel.EndRead(default);
            }
            channel.Dispose();
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "Dispose includes stream and read-set cleanup jobs");
        }

        [Test]
        public void Dispose_OpenReadCompletesProducer()
        {
            bool safety = BurstCompiler.Options.EnableBurstSafetyChecks;
            bool debugger = JobsUtility.JobDebuggerEnabled;
            try
            {
                BurstCompiler.Options.EnableBurstSafetyChecks = true;
                JobsUtility.JobDebuggerEnabled = true;
                JobsUtility.JobWorkerCount = 0;
                channel.BeginTick(1);
                prodA.Update(); // scheduled writer cannot run until completed on the main thread
                UnsafeUtility.CheckForLeaks();
                channel.BeginRead(ref world.Unmanaged.ResolveSystemStateRef(consumer.SystemHandle));
                channel.Dispose(); // no EndRead: must finish writer before freeing the open set
                Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
            }
            finally
            {
                BurstCompiler.Options.EnableBurstSafetyChecks = safety;
                JobsUtility.JobDebuggerEnabled = debugger;
            }
        }

        [Test, Performance, Category("Performance")]
        public void Overhead()
        {
            channel.BeginTick(1);
            void Pair(out double writeUs, out double readUs)
            {
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(prodA.SystemHandle);
                long t0 = Stopwatch.GetTimestamp();
                channel.BeginWrite(ref state, ProducerKind.Test, 1);
                channel.EndWrite(default);
                long t1 = Stopwatch.GetTimestamp();
                channel.BeginRead(ref state);
                channel.EndRead(default);
                long t2 = Stopwatch.GetTimestamp();
                writeUs = (t1 - t0) * 1e6 / Stopwatch.Frequency;
                readUs = (t2 - t1) * 1e6 / Stopwatch.Frequency;
            }
            for (int i = 0; i < 200; i++) Pair(out _, out _);
            var w = new double[1001]; var r = new double[1001];
            for (int i = 0; i < w.Length; i++) Pair(out w[i], out r[i]);
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("BeginWrite+EndWrite", w, "us").AddSamples("BeginRead+EndRead", r, "us");
            run.Write("BeginWrite+EndWrite", budgetMedian: 5.0);
            run.AssertMedian("BeginWrite+EndWrite", 5.0);
            run.AssertMedian("BeginRead+EndRead", 5.0);
            UnityEngine.Debug.Log($"SimEventChannel overhead: write pair median {run.Get("BeginWrite+EndWrite").Median:F2} µs, read pair median {run.Get("BeginRead+EndRead").Median:F2} µs");
            Assert.That(() => { Pair(out _, out _); }, Is.Not.AllocatingGCMemory());
        }
    }
}
