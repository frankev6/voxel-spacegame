using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.Tests.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.DevTools
{
    /// <summary>SPEC-70 DEV-007: one discoverable coroutine case per smoke/regression file.</summary>
    public sealed class ScenarioFileTests : GameFlowFixture
    {
        static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "Scenarios"));
        public static IEnumerable<string> Files
        {
            get
            {
                var files = Directory.GetFiles(DirectoryPath, "*.scenario"); Array.Sort(files, StringComparer.Ordinal);
                foreach (string file in files)
                {
                    var header = ReadHeader(File.ReadAllText(file));
                    foreach (string tag in header.Tags)
                        if (tag == "regression" || tag == "smoke") { yield return Path.GetFileNameWithoutExtension(file); break; }
                }
            }
        }
        // Drafts intentionally contain future command/probe names. Validate their headers without resolving their steps.
        static ScenarioScript ReadHeader(string source)
        {
            var header = new StringBuilder();
            using var reader = new StringReader(source); string line;
            while ((line = reader.ReadLine()) != null)
            {
                string text = ScenarioTokenizer.WithoutComment(line).Trim();
                if (text.Length == 0) { header.AppendLine(line); continue; }
                int end = 0; while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
                string word = text.Substring(0, end);
                if (word != "scenario" && word != "desc" && word != "status" && word != "tags" && word != "timeout") break;
                header.AppendLine(line);
            }
            return ScenarioParser.Parse(header.ToString(), new DevCommandRegistry(), new ScenarioProbeRegistry());
        }
        [UnityTest, Category("Scenario")]
        public IEnumerator Run([ValueSource(nameof(Files))] string scenarioName)
        {
            string path = Path.Combine(DirectoryPath, scenarioName + ".scenario");
            string source = File.ReadAllText(path);
            if (ReadHeader(source).Status == "draft") Assert.Ignore("draft");
            var host = Service<DevRuntimeHost>();
            var script = ScenarioParser.Parse(source, host.Commands, host.Probes);
            using var runner = host.RunScenario(script, path, 1);
            double frameBudget = Math.Ceiling(script.TimeoutSeconds * 60);
            for (long frame = 0; frame < frameBudget && !runner.Finished; frame++) yield return null;
            if (!runner.Finished) runner.Abort("scenario frame timeout after " + frameBudget + " frames");
            TestContext.WriteLine("Scenario result: " + runner.Run.ResultJsonPath);
            Assert.AreEqual(ScenarioOutcome.Passed, runner.Run.Outcome, runner.Run.Message + "; " + runner.Run.ResultJsonPath);
            Assert.IsTrue(File.Exists(runner.Run.ResultJsonPath), "Scenario result JSON must exist");
        }
    }
}
