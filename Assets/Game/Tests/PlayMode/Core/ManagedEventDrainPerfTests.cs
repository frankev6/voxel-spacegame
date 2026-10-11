using System;
using System.Diagnostics;
using Game.Core.Events;
using Game.Data.Components;
using Game.Presentation.Systems;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;

[assembly: RegisterGenericComponentType(typeof(ManagedEventQueue<Game.Tests.PlayMode.Core.BridgePerfEvent>))]

namespace Game.Tests.PlayMode.Core
{
    /// <summary>Test-only payload for the transfer gate.</summary>
    public readonly struct BridgePerfEvent { public readonly uint A; public readonly ulong B; public BridgePerfEvent(uint a, ulong b) { A = a; B = b; } }

    /// <summary>CORE-009 gate (SPEC-02 §8): transfer of 1,000 envelopes ≤ 0.5 ms on the main thread.</summary>
    public sealed class ManagedEventDrainPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void Transfer1000()
        {
            EventHub.Reset();
            using var world = new World("managed-event-drain-perf");
            var drain = world.CreateSystemManaged<ManagedEventDrainSystem>();
            drain.RegisterChannel<BridgePerfEvent>();
            ulong sink = 0;
            EventBus<BridgePerfEvent>.Subscribe(e => sink += e.B);
            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ManagedEventQueue<BridgePerfEvent>>());
            var queue = q.GetSingleton<ManagedEventQueue<BridgePerfEvent>>().Queue;
            var rng = new Unity.Mathematics.Random(12345);

            double Frame()
            {
                for (int i = 0; i < 1000; i++)
                    queue.Enqueue(new ManagedEventEnvelope<BridgePerfEvent> { Tick = rng.NextUInt(4), SortKey = ((ulong)rng.NextUInt() << 32) | rng.NextUInt(), Payload = new BridgePerfEvent((uint)i, (ulong)i) });
                long t0 = Stopwatch.GetTimestamp();
                drain.Update();
                return (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            }

            for (int i = 0; i < 20; i++) Frame();
            var times = new double[31];
            for (int i = 0; i < times.Length; i++) times[i] = Frame();
            var stat = PerfRun.Gate("Transfer1000", times, 0.5);
            UnityEngine.Debug.Log($"ManagedEventDrainSystem transfer of 1,000 envelopes (sort, publish, drain to one handler): median {stat.Median:F4} ms");
            Assert.AreNotEqual(0UL, sink);
            EventHub.Reset();
        }
    }
}
