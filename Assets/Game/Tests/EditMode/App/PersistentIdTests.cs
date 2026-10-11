using System.Collections.Generic;
using Game.App.Session;
using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-008: mixed managed/Burst grants, persisted high-water mark and empty inactive-session pool.</summary>
    public sealed partial class PersistentIdTests : GameTestBase
    {
        GameConfigDefinition config;
        GameSession session;
        [SetUp] public void SetUp() => config = ScriptableObject.CreateInstance<GameConfigDefinition>();
        [TearDown] public void TearDown()
        {
            if (session != null) Services.Unregister(session);
            Object.DestroyImmediate(config);
        }

        [DisableAutoCreation, UpdateInGroup(typeof(InitializationSystemGroup)), UpdateAfter(typeof(PersistentIdRefillSystem))]
        public partial class BurstConsumer : SystemBase
        {
            public NativeList<ulong> Issued;
            ComponentLookup<PersistentIdAllocator> allocators;
            protected override void OnCreate()
            {
                RequireForUpdate<PersistentIdAllocator>();
                allocators = GetComponentLookup<PersistentIdAllocator>();
                Issued = new NativeList<ulong>(10000, Allocator.Persistent);
            }
            protected override void OnDestroy() { Dependency.Complete(); Issued.Dispose(); }
            protected override void OnUpdate()
            {
                allocators.Update(this);
                Dependency = new TakeJob { Allocators = allocators, Entity = SystemAPI.GetSingletonEntity<PersistentIdAllocator>(), Issued = Issued }.Schedule(Dependency);
            }
            [BurstCompile]
            struct TakeJob : IJob
            {
                public ComponentLookup<PersistentIdAllocator> Allocators;
                public Entity Entity;
                public NativeList<ulong> Issued;
                public void Execute()
                {
                    var a = Allocators[Entity];
                    for (int i = 0; i < 100 && a.Next < a.End; i++) Issued.Add(a.Next++);
                    Allocators[Entity] = a;
                }
            }
        }

        EcsTestWorld Setup(out PersistentIdRefillSystem refill, out BurstConsumer consumer)
        {
            var w = CreateWorld(typeof(PersistentIdRefillSystem), typeof(BurstConsumer));
            w.AddSingleton(default(PersistentIdAllocator));
            refill = w.World.GetExistingSystemManaged<PersistentIdRefillSystem>();
            consumer = w.World.GetExistingSystemManaged<BurstConsumer>();
            session = Services.Register(new GameSession(w.World, config));
            session.ResetPersistentIds(1, true);
            return w;
        }

        [Test]
        public void Unique_MixedSources()
        {
            var w = Setup(out var refill, out var consumer); var seen = new HashSet<ulong>();
            for (int frame = 0; frame < 100; frame++)
            {
                // Do not use StepTicks: its end-of-frame completion would conceal a missing refill dependency wait.
                refill.Update();
                for (int i = 0; i < 100; i++) Assert.IsTrue(seen.Add(session.Allocate()));
                consumer.Update();
            }
            w.EntityManager.CompleteAllTrackedJobs();
            Assert.AreEqual(10000, consumer.Issued.Length);
            foreach (ulong id in consumer.Issued) Assert.IsTrue(seen.Add(id), "duplicate " + id);
            Assert.AreEqual(20000, seen.Count); Assert.IsFalse(seen.Contains(0));
            foreach (ulong id in seen) Assert.Less(id, session.NextPersistentId);
            Assert.Greater(session.NextPersistentId, 20001ul, "Unused remainder of replaced blocks is never reused");
        }

        [Test]
        public void AfterLoad_Monotonic()
        {
            var w = Setup(out var refill, out var consumer);
            refill.Update(); consumer.Update();
            ulong lastManaged = session.Allocate(); w.EntityManager.CompleteAllTrackedJobs();
            ulong reservedEnd = w.GetSingleton<PersistentIdAllocator>().End;
            using var writer = new SaveWriter(); writer.WriteUInt64(session.NextPersistentId);
            // FND-009 owns the complete SESS payload; this is its exact NextPersistentId UInt64 field.
            ulong restored = new SaveReader(writer.ToArray()).ReadUInt64();
            session.ResetPersistentIds(restored, true);
            Assert.AreEqual(0ul, w.GetSingleton<PersistentIdAllocator>().End, "Never reuse a pre-load grant");
            ulong first = session.Allocate(); Assert.Greater(first, lastManaged); Assert.GreaterOrEqual(first, reservedEnd);
            foreach (ulong id in consumer.Issued) Assert.Less(id, first);
            consumer.Issued.Clear(); refill.Update(); consumer.Update(); w.EntityManager.CompleteAllTrackedJobs();
            foreach (ulong id in consumer.Issued) Assert.Greater(id, first);
        }

        [Test, Performance, Category("Performance")]
        public void NoSession_Empty()
        {
            // Zero is valid configuration: refill when empty, with no early top-up.
            var serialized = new UnityEditor.SerializedObject(config);
            serialized.FindProperty("persistentIdRefillThreshold").intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
            var w = CreateWorld(typeof(PersistentIdRefillSystem));
            var entity = w.AddSingleton(new PersistentIdAllocator { Next = 10, End = 100 });
            var refill = w.World.GetExistingSystemManaged<PersistentIdRefillSystem>();
            refill.Update(); AssertEmpty();
            session = Services.Register(new GameSession(w.World, config));
            w.EntityManager.SetComponentData(entity, new PersistentIdAllocator { Next = 20, End = 200 });
            refill.Update(); AssertEmpty();
            session.ResetPersistentIds(1, true); refill.Update(); Assert.AreEqual(1ul, w.GetSingleton<PersistentIdAllocator>().Next);
            MeasureNoRefill(refill);
            session.ResetPersistentIds(1, false); AssertEmpty(); refill.Update(); AssertEmpty();
            void AssertEmpty() { var a = w.GetSingleton<PersistentIdAllocator>(); Assert.AreEqual(0ul, a.Next); Assert.AreEqual(0ul, a.End); }
        }
        static void MeasureNoRefill(PersistentIdRefillSystem refill)
        {
            bool burst = BurstCompiler.Options.EnableBurstCompilation, safety = BurstCompiler.Options.EnableBurstSafetyChecks;
            bool debugger = JobsUtility.JobDebuggerEnabled; var leaks = NativeLeakDetection.Mode;
            try
            {
                BurstCompiler.Options.EnableBurstCompilation = true; BurstCompiler.Options.EnableBurstSafetyChecks = false;
                JobsUtility.JobDebuggerEnabled = false; NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
                PerfSettings.AssertReference();
                using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PersistentIdRefillSystem.Check");
                Assert.IsTrue(recorder.Valid && recorder.IsRunning);
                for (int i = 0; i < 60; i++) refill.Update();
                var samples = new double[300];
                for (int i = 0; i < samples.Length; i++)
                {
                    long before = recorder.CurrentValue; refill.Update(); samples[i] = (recorder.CurrentValue - before) * 1e-6;
                }
                var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("PersistentIdNoRefill", samples);
                Assert.Greater(run.Get("PersistentIdNoRefill").Median, 0);
                run.Write("PersistentIdNoRefill", .01); run.AssertMedian("PersistentIdNoRefill", .01);
                TestContext.WriteLine($"No-refill check median/p95 {run.Get("PersistentIdNoRefill").Median:F6}/{run.Get("PersistentIdNoRefill").P95:F6} ms; 60 warmup + 300 initialization updates");
            }
            finally
            {
                BurstCompiler.Options.EnableBurstCompilation = burst; BurstCompiler.Options.EnableBurstSafetyChecks = safety;
                JobsUtility.JobDebuggerEnabled = debugger; NativeLeakDetection.Mode = leaks;
            }
        }
    }
}
