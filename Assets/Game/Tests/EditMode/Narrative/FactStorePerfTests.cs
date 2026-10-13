using System;
using System.Diagnostics;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    [Category("Performance")]
    public sealed class FactStorePerfTests
    {
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        [Test] public void GetBySlot()
        {
            var store = FactStoreTests.Create(new FactDeclaration { Name = "perf_value", Kind = FactKind.Int });
            int slot = store.Catalog.SlotOf("perf_value"); store.SetBySlot(slot, 1, FactWriteSource.Debugger);
            EventBus<FactChanged>.Drain();
            const int reads = 100000; var samples = new double[5]; long sum = 0;
            for (int warmup = 0; warmup < reads; warmup++) sum += store.GetBySlot(slot);
            for (int run = 0; run < samples.Length; run++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                for (int i = 0; i < reads; i++) sum += store.GetBySlot(slot);
                long elapsed = Stopwatch.GetTimestamp() - start;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Assert.AreEqual(0, allocated); samples[run] = elapsed * 1000.0 / Stopwatch.Frequency / reads;
            }
            Assert.AreEqual(reads * 6L, sum); PerfRun.Gate("GetBySlot", samples, .000020);
        }
        [Test] public void Capture5k()
        {
            var rows = new FactDeclaration[5000];
            for (int i = 0; i < rows.Length; i++) rows[i] = new FactDeclaration { Name = "perf_fact_" + i.ToString("D4"), Kind = FactKind.Int };
            var store = FactStoreTests.Create(rows);
            for (int i = 0; i < rows.Length; i++) store.SetBySlot(store.Catalog.SlotOf(rows[i].Name), 1, FactWriteSource.Debugger);
            EventBus<FactChanged>.Drain(); store.Capture(); var samples = new double[5];
            for (int run = 0; run < samples.Length; run++)
            {
                long start = Stopwatch.GetTimestamp(); var saved = store.Capture();
                samples[run] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                Assert.AreEqual(5000, saved.Entries.Count);
            }
            PerfRun.Gate("Capture5k", samples, .5);
        }
    }
}
