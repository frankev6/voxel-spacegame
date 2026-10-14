using System;
using System.Diagnostics;
using Game.Narrative.Dialog;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    [Category("Performance")]
    public sealed class DialogPresenterPerfTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void PresentOptions()
        {
            using var f = new DialogPresenterFixture();
            var choices = new DialogOption[16];
            for (int i = 0; i < choices.Length; i++) choices[i] = new DialogOption { Id = i, Text = "Option" };
            f.Start(host => host.OnOptions(choices, choices.Length));
            f.Service.OnOptions(choices, choices.Length); // One initial warm-up; includes the fallback row.
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            f.Service.OnOptions(choices, choices.Length);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            Assert.AreEqual(17, f.Model.Options.Count);
            PerfRun.Gate("DialogPresentOptionsMs", new[] { ms }, .5);
            PerfRun.Gate("DialogPresentOptionsBytes", new[] { (double)allocated }, 1024, "B");
        }
    }
}
