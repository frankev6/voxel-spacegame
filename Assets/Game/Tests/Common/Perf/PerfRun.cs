using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core.Diagnostics;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Game.Tests.Common.Perf
{
    /// <summary>One measured run: warm-up, then one sample per frame (PlayMode) or per tick (EditMode) for every configured source.
    /// Implements SPEC-71 §4.2, §5.3.</summary>
    public sealed class PerfRun
    {
        enum Kind { Marker, MarkerPrefix, FrameTime, Custom }

        sealed class Source
        {
            public string Name, Unit, Key; public Kind Kind; public Func<double> Sample; public List<double> Values = new List<double>();
        }

        readonly string testName;
        readonly int warmup, measured;
        readonly List<Source> sources = new List<Source>();
        bool checkSettings = true;

        public PerfRun(string testName, int warmupFrames = 60, int measuredFrames = 300)
        {
            this.testName = testName;
            warmup = warmupFrames;
            measured = measuredFrames;
        }

        public PerfRun Marker(string markerName) { sources.Add(new Source { Name = markerName, Key = markerName, Kind = Kind.Marker, Unit = "ms" }); return this; }
        /// <summary>Sum of every marker starting with <paramref name="prefix"/> (e.g. "Brick").</summary>
        public PerfRun MarkerPrefix(string prefix) { sources.Add(new Source { Name = prefix + "*", Key = prefix, Kind = Kind.MarkerPrefix, Unit = "ms" }); return this; }
        public PerfRun FrameTime() { sources.Add(new Source { Name = "FrameTime", Kind = Kind.FrameTime, Unit = "ms" }); return this; }
        public PerfRun Custom(string name, Func<double> sample, string unit = "ms") { sources.Add(new Source { Name = name, Kind = Kind.Custom, Sample = sample, Unit = unit }); return this; }

        /// <summary>Tests of the kit itself skip the reference-settings check (SPEC-71 §5.3 step 1 applies to perf tests).</summary>
        internal PerfRun SkipSettingsCheck() { checkSettings = false; return this; }

        /// <summary>PlayMode: yields one frame per warm-up and measured sample.</summary>
        public IEnumerator RunFrames(Action perFrame = null)
        {
            Begin();
            for (int i = 0; i < warmup; i++) { perFrame?.Invoke(); yield return null; }
            for (int i = 0; i < measured; i++)
            {
                perFrame?.Invoke();
                yield return null;
                foreach (var s in sources) s.Values.Add(SampleLastFrame(s));
            }
        }

        /// <summary>EditMode: one StepTicks(1) per sample. Markers read the time recorded during that tick, because an EditMode loop
        /// never ends a profiler frame (SPEC-71 open question 5).</summary>
        public void RunEcs(EcsTestWorld world, Action perTick = null)
        {
            Begin();
            var before = new double[sources.Count];
            for (int i = 0; i < warmup + measured; i++)
            {
                for (int k = 0; k < sources.Count; k++) before[k] = SampleCurrent(sources[k]);
                perTick?.Invoke();
                world.StepTicks(1);
                if (i < warmup) continue;
                for (int k = 0; k < sources.Count; k++)
                {
                    var s = sources[k];
                    s.Values.Add(s.Kind == Kind.Custom ? s.Sample() : SampleCurrent(s) - before[k]);
                }
            }
        }

        void Begin()
        {
            if (checkSettings) PerfSettings.AssertReference();
            PerfMarkers.StartRecording();
            foreach (var s in sources) s.Values.Clear();
        }

        static double SampleLastFrame(Source s)
        {
            switch (s.Kind)
            {
                case Kind.Marker: return PerfMarkers.TryGetLastFrameMs(s.Key, out var ms) ? ms : 0;
                case Kind.MarkerPrefix:
                    double sum = 0;
                    foreach (var name in PerfMarkers.Names)
                        if (name.StartsWith(s.Key, StringComparison.Ordinal) && PerfMarkers.TryGetLastFrameMs(name, out var m)) sum += m;
                    return sum;
                case Kind.FrameTime: return Time.unscaledDeltaTime * 1000.0;
                default: return s.Sample();
            }
        }

        static double SampleCurrent(Source s)
        {
            switch (s.Kind)
            {
                case Kind.Marker: return PerfMarkers.TryGetCurrentFrameMs(s.Key, out var ms) ? ms : 0;
                case Kind.MarkerPrefix:
                    double sum = 0;
                    foreach (var name in PerfMarkers.Names)
                        if (name.StartsWith(s.Key, StringComparison.Ordinal) && PerfMarkers.TryGetCurrentFrameMs(name, out var m)) sum += m;
                    return sum;
                case Kind.FrameTime: return Time.unscaledDeltaTime * 1000.0;
                default: return 0;
            }
        }

        /// <summary>Adds samples directly (tests and callers that time their own loop).</summary>
        public PerfRun AddSamples(string name, IEnumerable<double> values, string unit = "ms")
        {
            var s = sources.Find(x => x.Name == name);
            if (s == null) { s = new Source { Name = name, Kind = Kind.Custom, Unit = unit, Sample = () => double.NaN }; sources.Add(s); }
            s.Values.AddRange(values);
            return this;
        }

        /// <summary>For gates that time their own loop (main-thread micro-benchmarks): records the samples as the current test's
        /// Logs/perf record and asserts the median budget. No reference-settings check (SPEC-71 open question 6).</summary>
        public static PerfStat Gate(string name, double[] samples, double budgetMedian, string unit = "ms")
        {
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples(name, samples, unit);
            run.Write(name, budgetMedian);
            run.AssertMedian(name, budgetMedian);
            return run.Get(name);
        }

        public PerfStat Get(string name)
        {
            var s = sources.Find(x => x.Name == name) ?? throw new ArgumentException($"PerfRun {testName}: no source named {name}");
            return PerfStat.From(s.Name, s.Unit, s.Values.ToArray());
        }

        public void AssertMedian(string name, double maxMedian) { var st = Get(name); CheckBudget(st, st.Median, maxMedian, "median"); }
        public void AssertP95(string name, double maxP95) { var st = Get(name); CheckBudget(st, st.P95, maxP95, "p95"); }
        public void AssertMax(string name, double maxValue) { var st = Get(name); CheckBudget(st, st.Max, maxValue, "max"); }

        public static void WarnBudget(string message)
        {
            // Warn is absent in this NUnit version. Consume the expected warning for fixtures
            // that also guard unexpected logs, while retaining it in the Editor/test output.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, message);
            Debug.LogWarning(message);
        }

        void CheckBudget(PerfStat stat, double measured, double target, string metric)
        {
            string message = $"{testName} {stat.Name}: {metric} {measured:F4} {stat.Unit}; target <= {target}";
            Assert.That(double.IsNaN(measured) || double.IsInfinity(measured), Is.False, message);
            // Zero managed allocation is a hot-path correctness contract; other PerfRun
            // thresholds are advisory budgets. Functional/capacity assertions live in callers.
            if (target == 0 && (stat.Unit == "B" || stat.Unit == "bytes")) Assert.LessOrEqual(measured, target, message);
            else if (measured > target) WarnBudget(message);
        }

        /// <summary>Logs/perf/&lt;testName&gt;.json plus one Performance Testing sample group per stat. The budget applies to the source named
        /// <paramref name="gate"/> when there is one, else to the first source.</summary>
        public void Write(string gate = null, double budgetMedian = double.NaN, double budgetP95 = double.NaN)
        {
            var stats = new List<PerfStat>();
            foreach (var s in sources) stats.Add(Get(s.Name));
            foreach (var st in stats)
            {
                if (PerformanceTest.Active == null) break;           // outside a [Performance] test only the JSON record is written
                var unit = st.Unit == "ms" ? SampleUnit.Millisecond : st.Unit == "us" || st.Unit == "µs" ? SampleUnit.Microsecond : SampleUnit.Undefined;
                Measure.Custom(new SampleGroup(st.Name + ".median", unit), st.Median);
                Measure.Custom(new SampleGroup(st.Name + ".p95", unit), st.P95);
            }
            File.WriteAllText(PathFor(testName), ToJson(gate, budgetMedian, budgetP95, stats));
        }

        internal string ToJson(string gate, double budgetMedian, double budgetP95, List<PerfStat> stats)
        {
            var primary = stats.Count == 0 ? default : stats.Find(s => s.Name == gate);
            if (primary.Name == null && stats.Count > 0) primary = stats[0];
            bool pass = (double.IsNaN(budgetMedian) || primary.Median <= budgetMedian) && (double.IsNaN(budgetP95) || primary.P95 <= budgetP95);
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"test\": ").Append(Str(testName)).Append(",\n");
            sb.Append("  \"gate\": ").Append(gate == null ? "null" : Str(gate)).Append(",\n");
            sb.Append("  \"utc\": ").Append(Str(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))).Append(",\n");
            sb.Append("  \"machine\": ").Append(Str(SystemInfo.deviceModel + " / " + SystemInfo.processorType)).Append(",\n");
            sb.Append("  \"unity\": ").Append(Str(Application.unityVersion)).Append(",\n");
            sb.Append("  \"stats\": [");
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    { \"name\": ").Append(Str(s.Name)).Append(", \"unit\": ").Append(Str(s.Unit))
                  .Append(", \"median\": ").Append(Num(s.Median)).Append(", \"p95\": ").Append(Num(s.P95))
                  .Append(", \"max\": ").Append(Num(s.Max)).Append(", \"mean\": ").Append(Num(s.Mean))
                  .Append(", \"samples\": ").Append(s.Samples.ToString(CultureInfo.InvariantCulture)).Append(" }");
            }
            sb.Append(stats.Count > 0 ? "\n  ],\n" : "],\n");
            sb.Append("  \"budget\": { \"median\": ").Append(Num(budgetMedian)).Append(", \"p95\": ").Append(Num(budgetP95)).Append(" },\n");
            sb.Append("  \"pass\": ").Append(pass ? "true" : "false").Append("\n}\n");
            return sb.ToString();
        }

        /// <summary>&lt;project&gt;/Logs/perf/&lt;testName with path characters replaced&gt;.json; creates the folder.</summary>
        public static string PathFor(string testName)
        {
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "perf");
            Directory.CreateDirectory(dir);
            var safe = new StringBuilder(testName.Length);
            foreach (var c in testName) safe.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_');
            return Path.Combine(dir, safe + ".json");
        }

        static string Num(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("R", CultureInfo.InvariantCulture);

        static string Str(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
