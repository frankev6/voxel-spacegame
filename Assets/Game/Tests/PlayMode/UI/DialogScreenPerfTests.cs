using System.Collections;
using Game.Core;
using Game.Core.Events;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.UI
{
    [Category("Performance")]
    public sealed class DialogScreenPerfTests
    {
        [SetUp] public void Setup() { EventHub.Reset(); Services.Reset(); }
        [TearDown] public void Cleanup() { EventHub.Reset(); Services.Reset(); }
        [UnityTest] public IEnumerator Idle()
        {
            using var f = new DialogViewFixture(); f.Populate(); f.Show();
            f.UI.RootElement.AddToClassList(Game.UI.Data.UIStyleClasses.ReduceMotion);
            yield return null; yield return null;
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            yield return null;
            if (!gc.Valid || gc.Count == 0) Assert.Ignore("The whole idle panel frame GC recorder is unavailable.");
            PerfRun.Gate("DialogScreenIdleFrameGc", new[] { (double)gc.LastValue }, 0, "B");
            if (main.Valid && main.Count > 0) PerfRun.Gate("DialogScreenIdleFrameMs", new[] { main.LastValue * 1e-6 }, 4);
            else TestContext.Out.WriteLine("The whole idle panel frame main-thread recorder is unavailable.");
        }
    }
}
