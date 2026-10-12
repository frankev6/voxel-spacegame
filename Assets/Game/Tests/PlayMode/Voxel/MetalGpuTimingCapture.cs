using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>SPEC-11 Q28: complete native Metal render intervals; never substitutes missing/zero counters.</summary>
    internal static class MetalGpuTimingCapture
    {
        sealed class Frame
        {
            public long First = long.MaxValue, Last, Order;
            public bool Shadows, Surface;
            public readonly List<(long Start, long End)> Intervals = new List<(long, long)>();
        }
        public static IEnumerator Capture(List<double> samples, List<double> elapsedSpans)
        {
            Assert.AreEqual(RuntimePlatform.OSXEditor, Application.platform, "The reference Metal gate requires macOS Editor and Xcode Instruments.");
            int pid;
            using (var current = Process.GetCurrentProcess()) pid = current.Id;
            Directory.CreateDirectory("Logs/rendering");
            string prefix = Path.GetFullPath("Logs/rendering/ren003-native-" + Guid.NewGuid().ToString("N"));
            string trace = prefix + ".trace", xml = prefix + ".xml";
            try
            {
                yield return Run("xcrun", new[] { "xctrace", "record", "--template", "Metal System Trace", "--attach", pid.ToString(CultureInfo.InvariantCulture), "--time-limit", "10s", "--output", trace });
                yield return Run("xcrun", new[] { "xctrace", "export", "--input", trace, "--xpath", "/trace-toc/run[@number=\"1\"]/data/table[@schema=\"metal-gpu-intervals\"]", "--output", xml });
                var document = XDocument.Load(xml);
                var identifiers = document.Descendants().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id"));
                XElement Resolve(XElement element) { while (element.Attribute("ref") != null) element = identifiers[(string)element.Attribute("ref")]; return element; }
                string Value(XElement element) => Resolve(element).Value;
                string Label(XElement element) { var resolved = Resolve(element); return (string)resolved.Attribute("fmt") ?? resolved.Value; }
                long Number(XElement element) => long.Parse(Value(element), CultureInfo.InvariantCulture);
                var schema = document.Descendants("schema").Single(e => (string)e.Attribute("name") == "metal-gpu-intervals");
                var columns = schema.Elements("col").Select((e, i) => new { Name = e.Element("mnemonic").Value, Index = i }).ToDictionary(e => e.Name, e => e.Index);
                var frames = new Dictionary<string, Frame>();
                foreach (var row in document.Descendants("row"))
                {
                    var values = row.Elements().ToArray();
                    var process = Resolve(values[columns["process"]]);
                    var processId = process.Element("pid");
                    if (processId == null || Number(processId) != pid) continue;
                    var frameId = values[columns["frame-number"]];
                    if (frameId.Name.LocalName == "sentinel") continue;
                    // Upload-only command buffers are separate trace frames. A complete render submission
                    // contains both the shadow and surface encoders; include ALL its GPU intervals and gaps.
                    string key = Value(frameId) + ":" + Value(values[columns["cmdbuffer-id"]]);
                    if (!frames.TryGetValue(key, out var frame)) frames.Add(key, frame = new Frame { Order = Number(frameId) });
                    long first = Number(values[columns["start"]]), duration = Number(values[columns["duration"]]);
                    frame.First = Math.Min(frame.First, first); frame.Last = Math.Max(frame.Last, first + duration);
                    frame.Intervals.Add((first, first + duration));
                    string label = Label(values[columns["event-label"]]);
                    frame.Shadows |= label.Contains("Shadows.Draw"); frame.Surface |= label.Contains("RenderLoop.Draw");
                }
                var completeFrames = new List<Frame>();
                var pending = new Frame();
                foreach (var frame in frames.Values.OrderBy(f => f.Order))
                {
                    pending.First = Math.Min(pending.First, frame.First); pending.Last = Math.Max(pending.Last, frame.Last);
                    pending.Intervals.AddRange(frame.Intervals);
                    if (!frame.Shadows || !frame.Surface) continue;
                    // The isolated camera submits upload buffers before its render buffer. Include their
                    // execution and every gap; upload-only buffers must never count as separate fast frames.
                    completeFrames.Add(pending); pending = new Frame();
                }
                var complete = completeFrames.ToArray();
                Assert.GreaterOrEqual(complete.Length, 302, "At least 300 complete native render submissions plus capture boundaries required");
                samples.Clear(); elapsedSpans.Clear();
                using (var evidence = new StreamWriter("Logs/rendering/ren003-native-gpu.csv"))
                {
                    evidence.WriteLine("first_ns,last_ns,elapsed_ms,execution_ms");
                    // Drop the two capture boundaries, never select frames by measured duration.
                    for (int i = 1; i < complete.Length - 1; i++)
                    {
                        var frame = complete[i];
                        frame.Intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
                        long first = frame.Intervals[0].Start, last = frame.Intervals[0].End, active = 0;
                        for (int j = 1; j < frame.Intervals.Count; j++)
                        {
                            var interval = frame.Intervals[j];
                            if (interval.Start > last) { active += last - first; first = interval.Start; last = interval.End; }
                            else last = Math.Max(last, interval.End);
                        }
                        active += last - first;
                        double ms = active / 1e6, elapsed = (frame.Last - frame.First) / 1e6;
                        Assert.Greater(ms, 0); Assert.LessOrEqual(ms, elapsed);
                        samples.Add(ms); elapsedSpans.Add(elapsed);
                        evidence.WriteLine(frame.First.ToString(CultureInfo.InvariantCulture) + "," + frame.Last.ToString(CultureInfo.InvariantCulture) + "," + elapsed.ToString("R", CultureInfo.InvariantCulture) + "," + ms.ToString("R", CultureInfo.InvariantCulture));
                    }
                }
                TestContext.WriteLine("Native Metal GPU evidence: Logs/rendering/ren003-native-gpu.csv; samples=" + samples.Count);
            }
            finally
            {
                // Raw Instruments captures include unrelated process/environment metadata; retain only this test's filtered timings.
                if (File.Exists(xml)) File.Delete(xml);
                if (Directory.Exists(trace)) Directory.Delete(trace, true);
            }
        }
        static IEnumerator Run(string command, string[] arguments)
        {
            var info = new ProcessStartInfo(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (string argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            Assert.IsNotNull(process);
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            try
            {
                while (!process.HasExited || !output.IsCompleted || !error.IsCompleted)
                { Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "Instruments capture/export timed out"); yield return null; }
                Assert.AreEqual(0, process.ExitCode, output.Result + error.Result);
            }
            finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(1000); } }
        }
    }
}
