using System;
using System.Diagnostics;
using Game.Economy;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Economy
{
    public sealed class InventoryModelPerfTests
    {
        [Test, Performance, Category("Performance")] public void Apply40()
        {
            using var f = new InventoryModelTests.Fixture(40); f.Inventory.SetCapacity(200);
            var forward = new ItemDelta[40]; var backward = new ItemDelta[40];
            for (int i = 0; i < 40; i++)
            {
                f.Add(i, 50); int delta = i < 20 ? -10 : 10;
                forward[i] = new ItemDelta(f.Items[i].Id, delta); backward[i] = new ItemDelta(f.Items[i].Id, -delta);
            }
            Assert.AreEqual(200, f.Inventory.UsedSlots);
            for (int i = 0; i < 64; i++) Assert.IsTrue(f.Inventory.TryApplyAtomic(i % 2 == 0 ? forward : backward));
            var times = new double[100]; bool succeeded = true; long allocated = 0;
            for (int i = 0; i < times.Length; i++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                succeeded &= f.Inventory.TryApplyAtomic(i % 2 == 0 ? forward : backward);
                long elapsed = Stopwatch.GetTimestamp() - start;
                allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
                times[i] = elapsed * 1000.0 / Stopwatch.Frequency;
            }
            Assert.IsTrue(succeeded); Assert.AreEqual(200, f.Inventory.UsedSlots); f.Invariants();
            Assert.AreEqual(0, allocated, "Atomic edit scratch must be reusable");
            PerfRun.Gate("InventoryApply40", times, .05);
            PerfRun.Gate("InventoryApply40GC", new[] { (double)allocated }, 0, "B");
        }
    }
}
