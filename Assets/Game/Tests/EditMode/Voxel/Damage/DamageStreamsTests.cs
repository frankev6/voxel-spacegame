using System;
using System.Linq;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public sealed unsafe class DamageStreamsTests
    {
        const int Count = 3000;
        World world;
        SystemHandle owner;
        TestDamageProducerSystem a, b;
        TestDamageConsumerSystem consumer;
        Entity clock;
        NativeArray<DamageEvent> eventsA, eventsB;
        NativeList<DamageEvent> output;
        NativeList<ExplosionRequest> explosions;
        NativeLeakDetectionMode previousLeak;
        bool previousSafety, previousDebugger;
        int previousWorkers;
        DamageStreams Streams
        {
            get { using var q = world.EntityManager.CreateEntityQuery(typeof(DamageStreams)); return q.GetSingleton<DamageStreams>(); }
        }
        [SetUp] public void SetUp()
        {
            previousLeak = NativeLeakDetection.Mode; previousSafety = BurstCompiler.Options.EnableBurstSafetyChecks;
            previousDebugger = JobsUtility.JobDebuggerEnabled; previousWorkers = JobsUtility.JobWorkerCount;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            BurstCompiler.Options.EnableBurstSafetyChecks = true; JobsUtility.JobDebuggerEnabled = true;
            Create();
        }
        void Create()
        {
            world = new World("DMG-002"); var em = world.EntityManager;
            owner = world.CreateSystem<DamageStreamsSystem>();
            clock = em.CreateEntity(typeof(SimClock), typeof(GameplayActive), typeof(BlockDefsRef), typeof(BlockModulesRef), typeof(OrientationTablesRef));
            eventsA = new NativeArray<DamageEvent>(Count, Allocator.Persistent); eventsB = new NativeArray<DamageEvent>(Count, Allocator.Persistent);
            a = world.CreateSystemManaged<TestDamageProducerSystem>(); b = world.CreateSystemManaged<TestDamageProducerSystem>();
            a.Tag = ComponentType.ReadOnly<TestDamageTagA>(); b.Tag = ComponentType.ReadOnly<TestDamageTagB>();
            a.Events = eventsA; b.Events = eventsB;
            foreach (bool first in new[] { true, false })
            {
                var archetype = em.CreateArchetype(typeof(TestDamageIndex), first ? typeof(TestDamageTagA) : typeof(TestDamageTagB));
                using var entities = new NativeArray<Entity>(Count, Allocator.Temp); em.CreateEntity(archetype, entities);
                for (int i = 0; i < Count; i++) em.SetComponentData(entities[i], new TestDamageIndex { Value = i });
            }
            for (int i = 0; i < Count; i++)
            {
                eventsA[i] = new DamageEvent { Producer = ProducerKind.Beam, Sequence = (uint)i, Amount = i + 1 };
                eventsB[i] = new DamageEvent { Producer = ProducerKind.Projectile, Sequence = (uint)i, Amount = i + Count + 1 };
            }
            output = new NativeList<DamageEvent>(Count * 2, Allocator.Persistent);
            explosions = new NativeList<ExplosionRequest>(Count * 2, Allocator.Persistent);
            consumer = world.CreateSystemManaged<TestDamageConsumerSystem>(); consumer.Damage = output; consumer.Explosions = explosions;
            Tick(1);
        }
        void Tick(uint tick)
        {
            world.EntityManager.SetComponentData(clock, new SimClock { Tick = tick });
            owner.Update(world.Unmanaged);
        }
        void DisposeWorld()
        {
            if (world != null && world.IsCreated) world.Dispose();
            world = null;
            if (eventsA.IsCreated) eventsA.Dispose(); if (eventsB.IsCreated) eventsB.Dispose();
            if (output.IsCreated) output.Dispose(); if (explosions.IsCreated) explosions.Dispose();
        }
        [TearDown] public void TearDown()
        {
            try { DisposeWorld(); }
            finally
            {
                NativeLeakDetection.Mode = previousLeak; BurstCompiler.Options.EnableBurstSafetyChecks = previousSafety;
                JobsUtility.JobDebuggerEnabled = previousDebugger; JobsUtility.JobWorkerCount = previousWorkers;
            }
            LogAssert.NoUnexpectedReceived();
        }
        void ProduceAndRead()
        {
            a.Update(); b.Update(); consumer.Update(); consumer.LastJob.Complete();
            Assert.AreEqual(Count * 2, output.Length);
        }
        [Test] public void TwoParallelProducers_NoSafetyErrors()
        {
            ProduceAndRead();
            Assert.AreEqual(0, Streams.Damage.PendingCount);
            Assert.AreEqual(ProducerKind.Beam, output[0].Producer);
            Assert.AreEqual(ProducerKind.Projectile, output[Count].Producer);
        }
        [Test] public void Consumer_ReadsInRegistrationAndIndexOrder()
        {
            ProduceAndRead();
            for (int i = 0; i < Count * 2; i++)
            {
                Assert.AreEqual((uint)(i % Count), output[i].Sequence, "stream index order");
                Assert.AreEqual(i < Count ? ProducerKind.Beam : ProducerKind.Projectile, output[i].Producer, "registration beats producer enum order");
            }
            var data = Streams.Data; data->AllJobs(default).Complete();
            data->Current.EventsIn = Count * 2;
            data->PendingDetachNext.Add(new PendingDetach { SlotIndex = 42 });
            Tick(2); data->AllJobs(default).Complete();
            Assert.AreEqual(Count * 2, Streams.LastTickStats.EventsIn);
            Assert.AreEqual(1, Streams.LastTickStats.Tick);
            Assert.AreEqual(42, data->PendingDetachCurrent[0].SlotIndex);
            Assert.AreEqual(0, data->PendingDetachNext.Length);
        }
        [Test] public void StaleStream_IsDisposedAndLoggedOnce()
        {
            UnsafeUtility.CheckForLeaks();
            a.Update(); Tick(2); Assert.AreEqual(1, Streams.Damage.PendingCount);
            LogAssert.Expect(LogType.Warning, "[Core] stale stream: damage");
            Tick(3); Assert.AreEqual(0, Streams.Damage.PendingCount);
            a.Update(); Tick(5); Assert.AreEqual(0, Streams.Damage.PendingCount);
            DisposeWorld(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
        }
        [BurstCompile] struct WriteRemovedJob : IJob
        {
            public NativeList<CellRemoved> Removed;
            public NativeList<GridRemovedRange> Ranges;
            public NativeList<CellRestored> Restored;
            public NativeList<GridRemovedRange> RestoreRanges;
            public NativeList<GridCoreLost> CoreLost;
            public void Execute()
            {
                Removed.Add(default); Ranges.Add(new GridRemovedRange { Count = 1 });
                Restored.Add(default); RestoreRanges.Add(new GridRemovedRange { Count = 1 }); CoreLost.Add(default);
            }
        }
        [BurstCompile] struct ReadCellsJob : IJob
        {
            public CellRemovedView Removed; public CellRestoredView Restored;
            public NativeReference<int> Count;
            public void Execute() => Count.Value = Removed.Cells.Length + Restored.Cells.Length + Removed.Grids.Length + Restored.Grids.Length;
        }
        [Test] public void Destroy_WithPendingHandles_NoLeak()
        {
            DisposeWorld(); UnsafeUtility.CheckForLeaks();
            JobsUtility.JobWorkerCount = 0; Create();
            a.Update(); b.Update(); consumer.Update();
            var streams = Streams; var data = streams.Data;
            var writer = new WriteRemovedJob { Removed = data->Removed, Ranges = data->RemovedGrids, Restored = data->Restored,
                RestoreRanges = data->RestoredGrids, CoreLost = data->CoreLost }.Schedule(data->AllJobs(default));
            data->RemovedWriteHandle = writer; data->CoreLostWriteHandle = writer;
            var count = new NativeReference<int>(Allocator.TempJob);
            ref var state = ref world.Unmanaged.ResolveSystemStateRef(consumer.SystemHandle);
            var removed = streams.BeginRemovedRead(ref state); var restored = streams.BeginRestoredRead(ref state);
            var reader = new ReadCellsJob { Removed = removed, Restored = restored, Count = count }.Schedule(state.Dependency);
            streams.EndRemovedRead(reader); streams.EndRestoredRead(reader);
            // The owner must finish the reader even though it is deliberately not assigned to a system dependency.
            DisposeWorld(); Assert.AreEqual(4, count.Value); count.Dispose();
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
        }
        [Test] public void DrainExplosions_IsThreadCountIndependent()
        {
            for (int i = 0; i < Count; i++)
            {
                var x = eventsA[i]; x.Source = new Entity { Index = i % 7, Version = 1 }; x.Sequence = (uint)(i % 3); eventsA[i] = x;
                var y = eventsB[i]; y.Source = x.Source; y.Sequence = x.Sequence; eventsB[i] = y;
            }
            var expected = eventsA.ToArray().Concat(eventsB.ToArray()).OrderBy(e => e.Source.Index).ThenBy(e => (byte)e.Producer).ThenBy(e => e.Sequence).Select(e => e.Amount).ToArray();
            float[] baseline = null;
            foreach (int workers in new[] { 1, 8 })
            {
                JobsUtility.JobWorkerCount = workers; a.Explosions = b.Explosions = consumer.DrainExplosions = true;
                a.Update(); b.Update(); consumer.Update(); consumer.LastJob.Complete();
                var actual = explosions.AsArray().ToArray().Select(e => e.Amount).ToArray();
                CollectionAssert.AreEqual(expected, actual, "stable sort retains gather order for equal keys");
                if (baseline != null) CollectionAssert.AreEqual(baseline, actual); baseline = actual;
            }
            using var lost = new NativeList<GridCoreLost>(Allocator.Temp);
            Streams.Data->CoreLost.Add(new GridCoreLost { Tick = 1 }); Streams.Data->CoreLost.Add(new GridCoreLost { Tick = 2 });
            Streams.DrainCoreLost(lost); Assert.AreEqual(2, lost.Length); Assert.AreEqual(0, Streams.Data->CoreLost.Length);
        }
    }
}
