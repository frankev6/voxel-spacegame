using System;
using System.Diagnostics;
using Game.Narrative.Dialog;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    [Category("Performance")]
    public sealed class DialogServicePerfTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void Pump_Idle()
        {
            var service = new DialogService(new FakeDialogRunner(), DialogServiceTests.Table(),
                new DialogServiceTests.Context(FactStoreTests.Create().Catalog), new DialogMemory());
            service.Pump(); var samples = new double[5];
            for (int run = 0; run < samples.Length; run++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 100; i++) service.Pump();
                samples[run] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 100;
                Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - bytes);
            }
            PerfRun.Gate("DialogPumpIdle", samples, .01);
        }
    }
}
