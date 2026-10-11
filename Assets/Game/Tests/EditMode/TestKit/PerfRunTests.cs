using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Tests.Common.Perf;
using NUnit.Framework;

namespace Game.Tests.EditMode.TestKit
{
    /// <summary>TST-001: PerfStat maths and the PerfRun JSON record (SPEC-71 §5.3).</summary>
    public sealed class PerfRunTests
    {
        [Test]
        public void Stats_MedianP95()
        {
            var values = Enumerable.Range(1, 300).Select(i => (double)i).Reverse().ToArray();
            var s = PerfStat.From("x", "ms", values);
            Assert.AreEqual(150.5, s.Median);
            Assert.AreEqual(285, s.P95);
            Assert.AreEqual(300, s.Max);
            Assert.AreEqual(150.5, s.Mean, 1e-9);
            Assert.AreEqual(300, s.Samples);
            Assert.AreEqual(3, PerfStat.From("odd", "ms", new double[] { 5, 1, 3 }).Median, "odd count: the middle value");
        }

        [Test]
        public void Write_Json()
        {
            const string name = "Game.Tests.EditMode.TestKit.PerfRunTests.Write_Json";
            var run = new PerfRun(name).AddSamples("Tick", Enumerable.Range(1, 300).Select(i => (double)i)).AddSamples("Other", new double[] { 1, 2 }, "count");
            var path = PerfRun.PathFor(name);
            if (File.Exists(path)) File.Delete(path);
            run.Write("Tick", budgetMedian: 200, budgetP95: 300);
            var json = File.ReadAllText(path);
            foreach (var key in new[] { "test", "gate", "utc", "machine", "unity", "stats", "budget", "pass" })
                StringAssert.Contains($"\"{key}\":", json, key);
            StringAssert.Contains("\"name\": \"Tick\", \"unit\": \"ms\", \"median\": 150.5, \"p95\": 285, \"max\": 300, \"mean\": 150.5, \"samples\": 300", json);
            StringAssert.Contains("\"budget\": { \"median\": 200, \"p95\": 300 }", json);
            StringAssert.Contains("\"pass\": true", json);

            run.Write("Tick", budgetMedian: 100);
            StringAssert.Contains("\"pass\": false", File.ReadAllText(path), "median 150.5 > 100");
            StringAssert.Contains("\"budget\": { \"median\": 100, \"p95\": null }", File.ReadAllText(path));
            File.Delete(path);
        }

        [Test]
        public void PerfSummary_PrintsPassFail_ExitsOneOnFailure()
        {
            var dir = Path.Combine(Path.GetTempPath(), "perf_summary_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var pass = new PerfRun("A.Pass").AddSamples("T", new double[] { 1, 2, 3 }).ToJson("T", 5, double.NaN, new List<PerfStat> { PerfStat.From("T", "ms", new double[] { 1, 2, 3 }) });
                var fail = new PerfRun("B.Fail").ToJson("T", 1, double.NaN, new List<PerfStat> { PerfStat.From("T", "ms", new double[] { 4, 5, 6 }) });
                File.WriteAllText(Path.Combine(dir, "A.Pass.json"), pass);
                var (code1, out1) = RunSummary(dir);
                Assert.AreEqual(0, code1, out1); StringAssert.Contains("PASS", out1);
                File.WriteAllText(Path.Combine(dir, "B.Fail.json"), fail);
                var (code2, out2) = RunSummary(dir);
                Assert.AreEqual(1, code2, out2); StringAssert.Contains("FAIL", out2);
                Assert.IsTrue(Regex.IsMatch(out2, @"B\.Fail\s.*FAIL"), out2);
            }
            finally { Directory.Delete(dir, true); }
        }

        static (int code, string output) RunSummary(string dir)
        {
            var root = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            var psi = new System.Diagnostics.ProcessStartInfo("/usr/bin/env", $"python3 \"{Path.Combine(root, "tools", "perf_summary.py")}\" --dir \"{dir}\"")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var p = System.Diagnostics.Process.Start(psi);
            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, output);
        }
    }
}
