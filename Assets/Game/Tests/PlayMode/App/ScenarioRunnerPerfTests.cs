using System;
using System.Diagnostics;
using System.IO;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Game.Tests.PlayMode.App
{
    public sealed class ScenarioRunnerPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void Step_Smoke()
        {
            var host = DevRuntimeHost.Create(null);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "playtest", "runner-perf", Guid.NewGuid().ToString("N")));
            try
            {
                var script = ScenarioParser.Parse("scenario perf\ntimeout 300\nwait frames=100000000", host.Commands, host.Probes);
                using var runner = new ScenarioRunner(script, "perf.scenario", 1, host.Context, output);
                for (int i = 0; i < 100; i++) runner.Step();
                var times = new double[31];
                for (int sample = 0; sample < times.Length; sample++)
                {
                    long start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < 1000; i++) runner.Step();
                    times[sample] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 1000;
                }
                Assert.IsFalse(runner.Finished);
                var stat = PerfRun.Gate("ScenarioStep", times, .1);
                TestContext.WriteLine($"Scenario wait step median {stat.Median:F6} ms");
            }
            finally { host.Dispose(); if (Directory.Exists(output)) Directory.Delete(output, true); }
        }
    }
}
