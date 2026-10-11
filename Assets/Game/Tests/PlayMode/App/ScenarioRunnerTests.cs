using System;
using System.Collections;
using System.IO;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    public sealed class ScenarioRunnerTests
    {
        DevRuntimeHost host;
        string output;
        int nextId;
        [SetUp] public void SetUp()
        {
            host = DevRuntimeHost.Create(null);
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "playtest", "runner-tests", Guid.NewGuid().ToString("N")));
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            host?.Dispose(); yield return null;
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
        ScenarioRunner Runner(string body)
        {
            var script = ScenarioParser.Parse("scenario test_run\nstatus active\ntimeout 5\n" + body, host.Commands, host.Probes);
            return new ScenarioRunner(script, "test.scenario", ++nextId, host.Context, output);
        }
        static IEnumerator Finish(ScenarioRunner runner)
        {
            double limit = Time.realtimeSinceStartupAsDouble + 6;
            while (!runner.Finished && Time.realtimeSinceStartupAsDouble < limit) { runner.Step(); yield return null; }
            Assert.IsTrue(runner.Finished); Assert.IsTrue(File.Exists(runner.Run.ResultJsonPath), runner.Run.Message);
        }
        [UnityTest] public IEnumerator Pass_Fail_Error_ExitCodes()
        {
            using var pass = Runner("wait frames=2\nwait ticks=2\nassert true");
            yield return Finish(pass); Assert.AreEqual(ScenarioOutcome.Passed, pass.Run.Outcome); Assert.AreEqual(0, pass.Run.ExitCode);
            using var fail = Runner("assert 1 == 2 \"different\"");
            yield return Finish(fail); Assert.AreEqual(ScenarioOutcome.Failed, fail.Run.Outcome); Assert.AreEqual(1, fail.Run.ExitCode);
            StringAssert.Contains("1 == 2", fail.Run.Message);
            using var error = Runner("assert 1 == \"1\"");
            yield return Finish(error); Assert.AreEqual(ScenarioOutcome.Error, error.Run.Outcome); Assert.AreEqual(2, error.Run.ExitCode);
            Assert.AreNotEqual(pass.Run.ResultJsonPath, fail.Run.ResultJsonPath, "same-second runs cannot overwrite each other");
        }
        [UnityTest] public IEnumerator WaitTimeout_Message()
        {
            using var runner = Runner("wait 1 == 2 timeout=0.02");
            yield return Finish(runner); Assert.AreEqual(1, runner.Run.ExitCode);
            StringAssert.Contains("wait timed out: 1 == 2", runner.Run.Message);
        }
        [UnityTest] public IEnumerator UnexpectedErrorLogs_Fail()
        {
            using var runner = Runner("wait frames=1\nassert true");
            LogAssert.Expect(LogType.Error, "scenario unexpected error"); Debug.LogError("scenario unexpected error");
            yield return Finish(runner); Assert.AreEqual(1, runner.Run.ExitCode);
            StringAssert.Contains("unexpected error logs", runner.Run.Message); StringAssert.Contains("scenario unexpected error", runner.Run.Message);
        }
        [Serializable] sealed class JsonResult
        {
            public string scenario = null, file = null, status = null, result = null, message = null, startedUtc = null;
            public int exitCode = 0, failedLine = 0;
            public double durationSeconds = 0;
            public ulong seed = 0;
            public JsonStep[] steps = null;
            public JsonLogs logs = null;
        }
        [Serializable] sealed class JsonStep
        {
            public int line = 0;
            public string text = null, outcome = null, output = null;
            public double seconds = 0;
        }
        [Serializable] sealed class JsonLogs { public long errors = 0, exceptions = 0, warnings = 0; }
        [UnityTest] public IEnumerator Json_HasAllKeys()
        {
            using var runner = Runner("let n = 2\nassert $n == 2 \"escaped \\\"quote\\\"\"");
            yield return Finish(runner); Assert.AreEqual(0, runner.Run.ExitCode, runner.Run.Message);
            string json = File.ReadAllText(runner.Run.ResultJsonPath);
            foreach (string key in new[] { "scenario", "file", "status", "result", "exitCode", "message", "startedUtc", "durationSeconds", "failedLine", "seed", "steps", "logs", "errors", "exceptions", "warnings" })
                StringAssert.Contains("\"" + key + "\":", json);
            var result = JsonUtility.FromJson<JsonResult>(json);
            Assert.AreEqual("test_run", result.scenario); Assert.AreEqual("pass", result.result); Assert.AreEqual(2, result.steps.Length);
            Assert.AreEqual("ok", result.steps[1].outcome); Assert.IsNotNull(result.logs);
            Assert.AreEqual((byte)'{', File.ReadAllBytes(runner.Run.ResultJsonPath)[0], "UTF-8 without BOM");
        }
    }
}
