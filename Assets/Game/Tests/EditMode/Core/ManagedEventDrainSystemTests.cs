using System.Collections.Generic;
using System.Diagnostics;
using Game.Core.Events;
using Game.Data.Components;
using Game.Presentation.Systems;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

[assembly: RegisterGenericComponentType(typeof(ManagedEventQueue<Game.Tests.EditMode.Core.BridgeTestEvent>))]

namespace Game.Tests.EditMode.Core
{
    /// <summary>Test-only managed event payload (CORE-009).</summary>
    public readonly struct BridgeTestEvent
    {
        public readonly uint A; public readonly ulong B;
        public BridgeTestEvent(uint a, ulong b) { A = a; B = b; }
    }

    /// <summary>CORE-009: ManagedEventDrainSystem transfer order, parallel producers, lifetime and cost (SPEC-02 §5.6).</summary>
    public sealed partial class ManagedEventDrainSystemTests
    {
        public struct Source : IComponentData { public uint Tick; public ulong Key; public uint A; }

        [BurstCompile]
        struct EmitJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Source> SourceHandle;
            [NativeDisableContainerSafetyRestriction] public NativeQueue<ManagedEventEnvelope<BridgeTestEvent>>.ParallelWriter Writer;
            public ulong Salt;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var sources = chunk.GetNativeArray(ref SourceHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var s = sources[i];
                    Writer.Enqueue(new ManagedEventEnvelope<BridgeTestEvent> { Tick = s.Tick, SortKey = s.Key, Payload = new BridgeTestEvent(s.A, s.Key ^ Salt) });
                }
            }
        }

        /// <summary>The SPEC-02 §5.6 producer pattern: read-only singleton, parallel writer without container safety.</summary>
        [DisableAutoCreation]
        public partial class Producer : SystemBase
        {
            public ulong Salt;
            EntityQuery sources, queue;
            protected override void OnCreate()
            {
                sources = GetEntityQuery(ComponentType.ReadOnly<Source>());
                queue = GetEntityQuery(ComponentType.ReadOnly<ManagedEventQueue<BridgeTestEvent>>());
            }
            protected override void OnUpdate()
            {
                var writer = queue.GetSingleton<ManagedEventQueue<BridgeTestEvent>>().Queue.AsParallelWriter();
                Dependency = new EmitJob { SourceHandle = GetComponentTypeHandle<Source>(true), Writer = writer, Salt = Salt }.ScheduleParallel(sources, Dependency);
            }
        }

        World world;
        ManagedEventDrainSystem drain;
        readonly List<BridgeTestEvent> received = new List<BridgeTestEvent>();
        int savedWorkers;
        NativeLeakDetectionMode savedLeakMode;

        [SetUp]
        public void SetUp()
        {
            EventHub.Reset();
            savedWorkers = JobsUtility.JobWorkerCount;
            JobsUtility.JobWorkerCount = Mathf.Min(8, JobsUtility.JobWorkerMaximumCount);
            savedLeakMode = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            world = new World("managed-event-bridge");
            drain = world.CreateSystemManaged<ManagedEventDrainSystem>();
            drain.RegisterChannel<BridgeTestEvent>();
            received.Clear();
            EventBus<BridgeTestEvent>.Subscribe(e => received.Add(e));
        }

        [TearDown]
        public void TearDown()
        {
            if (world != null && world.IsCreated) world.Dispose();
            EventHub.Reset();
            NativeLeakDetection.Mode = savedLeakMode;
            JobsUtility.JobWorkerCount = savedWorkers;
        }

        void CreateSources(int count)
        {
            var em = world.EntityManager;
            using var entities = em.CreateEntity(em.CreateArchetype(typeof(Source)), count, Allocator.Temp);
            for (int i = 0; i < count; i++)
            {
                // Ticks 0..2, keys with duplicates so the payload hash breaks ties.
                em.SetComponentData(entities[i], new Source { Tick = (uint)(i % 3), Key = (ulong)((i * 7919) % 97), A = (uint)i });
            }
        }

        static NativeArray<Unity.Mathematics.int2> spans;
        [OneTimeSetUp] public void OneTimeSetUp() => spans = PayloadLayout.Spans(typeof(BridgeTestEvent), Allocator.Persistent);
        [OneTimeTearDown] public void OneTimeTearDown() => spans.Dispose();
        static ulong Hash(in BridgeTestEvent e) => PayloadLayout.Hash(e, spans);

        static void AssertSorted(List<BridgeTestEvent> events, Dictionary<uint, (uint tick, ulong key)> meta)
        {
            for (int i = 1; i < events.Count; i++)
            {
                var a = meta[events[i - 1].A]; var b = meta[events[i].A];
                int c = a.tick.CompareTo(b.tick);
                if (c == 0) c = a.key.CompareTo(b.key);
                if (c == 0) c = Hash(events[i - 1]).CompareTo(Hash(events[i]));
                Assert.LessOrEqual(c, 0, $"events {i - 1} and {i} out of order");
            }
        }

        [Test]
        public void ParallelProducer_DeliveredSorted_Deterministic()
        {
            const int N = 1000;
            CreateSources(N);
            var meta = new Dictionary<uint, (uint, ulong)>();
            for (int i = 0; i < N; i++) meta[(uint)i] = ((uint)(i % 3), (ulong)((i * 7919) % 97));
            var producer = world.CreateSystemManaged<Producer>();
            List<BridgeTestEvent> first = null;
            for (int run = 0; run < 20; run++)
            {
                producer.Update();
                drain.Update();
                Assert.AreEqual(N, received.Count, $"run {run}");
                AssertSorted(received, meta);
                if (first == null) first = new List<BridgeTestEvent>(received);
                else CollectionAssert.AreEqual(first, received, $"run {run} differs from run 0");
                received.Clear();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public unsafe void PayloadHash_IgnoresPadding()
        {
            // BridgeTestEvent is uint + 4 padding bytes + ulong: only the 12 field bytes count.
            CollectionAssert.AreEqual(new[] { new Unity.Mathematics.int2(0, 4), new Unity.Mathematics.int2(8, 8) }, spans.ToArray());
            var a = new BridgeTestEvent(7, 9); var b = a;
            ((byte*)&b)[5] = 0xAB;                                          // scribble into the padding
            Assert.AreEqual(Hash(a), Hash(b));
            Assert.AreNotEqual(Hash(a), Hash(new BridgeTestEvent(7, 10)));
        }

        [Test]
        public void TwoProducers_NoSafetyErrors()
        {
            CreateSources(1000);
            var p1 = world.CreateSystemManaged<Producer>(); p1.Salt = 1;
            var p2 = world.CreateSystemManaged<Producer>(); p2.Salt = 2;
            for (int frame = 0; frame < 5; frame++)
            {
                p1.Update(); p2.Update(); drain.Update();
                Assert.AreEqual(2000, received.Count);
                received.Clear();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RegisterTwice_OneSingleton()
        {
            int before = drain.ChannelCount;
            drain.RegisterChannel<BridgeTestEvent>();
            Assert.AreEqual(before, drain.ChannelCount);
            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ManagedEventQueue<BridgeTestEvent>>());
            Assert.AreEqual(1, q.CalculateEntityCount());
        }

        [Test]
        public void WorldDispose_NoLeaks()
        {
            // Warm up the global caches a world touches, then count only what a fresh world leaves behind. CheckForLeaks reports and
            // resets the tracked set (docs/08-tools.md §3).
            world.Dispose();
            UnsafeUtility.CheckForLeaks();
            world = new World("managed-event-bridge-leaks");
            drain = world.CreateSystemManaged<ManagedEventDrainSystem>();
            drain.RegisterChannel<BridgeTestEvent>();
            CreateSources(100);
            world.CreateSystemManaged<Producer>().Update();         // events left in the queue: disposal must free them too
            world.Dispose();
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "the world's queues are disposed with it");
        }

        [Test]
        public void NoEvents_NoAllocation()
        {
            for (int i = 0; i < 50; i++) drain.Update();
            Assert.That(() => { drain.Update(); }, Is.Not.AllocatingGCMemory());
            var times = new double[201];
            for (int i = 0; i < times.Length; i++) { long t0 = Stopwatch.GetTimestamp(); drain.Update(); times[i] = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency; }
            System.Array.Sort(times);
            UnityEngine.Debug.Log($"ManagedEventDrainSystem idle: median {times[100]:F4} ms");
            if (times[100] > 0.02) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Idle drain median {times[100]:F4} ms; target <= 0.02 ms");
        }

        [Test]
        public void DrainAll_RunsAfterTransfer()
        {
            var order = new List<string>();
            EventBus<PauseStateChanged>.Subscribe(_ => order.Add("managed"));
            EventBus<BridgeTestEvent>.Subscribe(_ => order.Add("ecs"));
            EventBus<PauseStateChanged>.Publish(default);
            world.EntityManager.CompleteDependencyBeforeRW<ManagedEventQueue<BridgeTestEvent>>();
            using (var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ManagedEventQueue<BridgeTestEvent>>()))
                q.GetSingleton<ManagedEventQueue<BridgeTestEvent>>().Queue.Enqueue(new ManagedEventEnvelope<BridgeTestEvent> { Payload = new BridgeTestEvent(1, 2) });
            drain.Update();
            CollectionAssert.AreEqual(new[] { "managed", "ecs" }, order, "both delivered in the same update; the transfer publishes after earlier managed events");
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(0, EventHub.PendingCount);
        }
    }
}
