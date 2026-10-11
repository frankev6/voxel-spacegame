using System;
using System.IO;
using System.Linq;
using Game.Core.Events;
using Game.Core.Save;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>SPEC-05 PERS-012: capture, typical restore and slot-listing micro-gates.</summary>
    public sealed class SavePerfTests
    {
        string root;
        static SlotRef Slot(int i = 1) => new SlotRef(new ProfileId("p001"), new SlotId("manual_" + i.ToString("D3")));
        [SetUp] public void SetUp() { EventHub.Reset(); root = Path.Combine(Path.GetTempPath(), "SavePerf_" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { EventHub.Reset(); TestContentFactory.DestroyAll(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        internal static void Gate(string name, Action action, double budget, int calls = 1)
        {
            var group = new SampleGroup(calls == 1 ? name : name + "Batch", SampleUnit.Millisecond);
            Measure.Method(action).SampleGroup(group).WarmupCount(2).MeasurementCount(10).IterationsPerMeasurement(calls).Run();
            var stat = PerfRun.Gate(name, group.Samples.Select(ms => ms / calls).ToArray(), budget);
            TestContext.WriteLine($"{name}: median {stat.Median:F4} ms; p95 {stat.P95:F4} ms");
        }
        [Test, Performance, Category("Performance")]
        public void Capture_MainThread()
        {
            using var suite = new TypicalSaveFakes(true); using var service = suite.CreateService(root);
            Gate("SaveCapture", () => {
                var captured = service.CaptureNow(Slot(), SaveReason.Manual, null);
                try { Assert.IsFalse(captured.Failure.HasValue); }
                finally { captured.Release(); }
            }, 5);
        }
        [Test, Performance, Category("Performance")]
        public void ReadAndRestore_Typical()
        {
            using var suite = new TypicalSaveFakes(false); using var service = suite.CreateService(root);
            var saved = service.WriteCaptured(service.CaptureNow(Slot(), SaveReason.Manual, null)); Assert.IsTrue(saved.Success, saved.Message);
            Gate("ReadRestoreTypical", () => {
                var read = service.ReadNow(Slot()); Assert.IsTrue(read.Success, read.Message);
                var report = service.Restore(read.Save); Assert.AreEqual(0, report.ErrorCount, report.ToText()); EventHub.DrainAll();
            }, 500);
            foreach (var p in suite.Participants)
            {
                CollectionAssert.AreEqual(p.Payload, p.RestoredPayload);
                Assert.AreEqual(p.Sources.Count, p.Restored.Count);
            }
        }
        [Test, Performance, Category("Performance")]
        public void ListSlots_50()
        {
            using var service = SaveFixtureHarness.CreateService(root, null, ContainerFixtureTests.Registry());
            for (int i = 1; i <= 50; i++) Assert.IsTrue(service.WriteCaptured(service.CaptureNow(Slot(i), SaveReason.Manual, null)).Success);
            Gate("ListSlots50", () => Assert.AreEqual(50, service.ListSlots(new ProfileId("p001")).Count), 50);
        }
        [Test, Performance, Category("Performance")]
        public void AutosaveUpdate_Idle()
        {
            using var service = SaveFixtureHarness.CreateService(root, null, ContainerFixtureTests.Registry());
            Gate("AutosaveUpdate", () => service.Autosave.Update(0), .01, 1000);
        }
    }
}
