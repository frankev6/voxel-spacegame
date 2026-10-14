using System.Collections;
using System.Collections.Generic;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>HUD-015 advisory rendered-map measurements; opt-in only.</summary>
    [Category("Performance")]
    public sealed class MapCanvasPerformanceTests
    {
        [UnityTest] public IEnumerator Pan_1024Stars_Under1ms()
        {
            using var f = new MapCanvasTests.Fixture(1024); yield return f.Ready();
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var samples = new List<double>(); int count = f.Canvas.TessellationCount;
            for (int i = 0; i < 30; i++)
            {
                f.Canvas.Pan(new float2(i % 2 == 0 ? 1 : -1, 0)); yield return null;
                if (recorder.Count > 0) samples.Add(recorder.LastValue / 1000000.0);
            }
            Assert.AreEqual(count, f.Canvas.TessellationCount);
            // Conservative inclusive main-thread frame cost, not just the Pan submission call.
            PerfRun.Gate("MapCanvas.Pan1024MainThreadFrame", samples.ToArray(), 1);
        }
        [UnityTest] public IEnumerator Tessellate_Under6ms()
        {
            using var f = new MapCanvasTests.Fixture(1024); yield return f.Ready();
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "MapCanvas.Tessellate", 1);
            var samples = new List<double>();
            for (int i = 0; i < 30; i++)
            {
                int count = f.Canvas.TessellationCount; f.Canvas.SetData(f.Data, i + 2);
                yield return null; yield return null;
                Assert.Greater(f.Canvas.TessellationCount, count);
                if (recorder.Count > 0) samples.Add(recorder.LastValue / 1000000.0);
            }
            PerfRun.Gate("MapCanvas.Tessellate1024", samples.ToArray(), 6);
        }
    }
}
