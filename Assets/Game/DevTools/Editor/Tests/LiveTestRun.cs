using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.DevTools.Editor.Tests
{
    /// <summary>DEV-026: runs tests in the open Editor through Unity's own TestRunnerApi (tools/live-tests.py calls it by eval). The
    /// Pipeline package's run_tests entered Play before the runner's first task (CreateBootstrapSceneTask), which then failed with
    /// "This cannot be used during play mode" and "Too many instant steps"; TestRunnerApi enters and leaves Play itself. It runs exactly
    /// the named tests, writes the runner's NUnit XML to the given path, and keeps a status file the caller polls:
    /// Logs/tools/live-test-status.json = {"state": "running"|"completed"|"error", "total", "passed", "failed", "skipped", "message"}.
    /// Domain reload is off on entering Play (project setting), so the callbacks survive the run.</summary>
    public static class LiveTestRun
    {
        public const string StatusPath = "Logs/tools/live-test-status.json";
        static Callbacks active;

        /// <summary>Starts a run of the tests named in <paramref name="namesFile"/> (one full test name a line) in <paramref name="mode"/>
        /// (EditMode or PlayMode). Returns "started" or why not.</summary>
        public static string Start(string mode, string namesFile, string xmlPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "refused: the Editor is in Play";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return "refused: the Editor is compiling";
            if (active != null) return "refused: a live run is already going";
            if (!Enum.TryParse(mode, true, out TestMode testMode) || (testMode != TestMode.EditMode && testMode != TestMode.PlayMode)) return "refused: mode is EditMode or PlayMode";
            var names = File.Exists(namesFile) ? File.ReadAllLines(namesFile) : Array.Empty<string>();
            names = Array.FindAll(names, n => n.Length > 0);
            if (names.Length == 0) return "refused: no tests named";
            Directory.CreateDirectory(Path.GetDirectoryName(StatusPath));
            if (File.Exists(xmlPath)) File.Delete(xmlPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            active = new Callbacks(api, xmlPath);
            api.RegisterCallbacks(active);
            Write("running", 0, 0, 0, 0, "");
            api.Execute(new ExecutionSettings(new Filter { testMode = testMode, testNames = names }));
            return "started";
        }

        static void Write(string state, int total, int passed, int failed, int skipped, string message) =>
            File.WriteAllText(StatusPath, JsonUtility.ToJson(new Status { state = state, total = total, passed = passed, failed = failed, skipped = skipped, message = message }));

        [Serializable] sealed class Status { public string state, message; public int total, passed, failed, skipped; }

        sealed class Callbacks : ICallbacks, IErrorCallbacks
        {
            readonly TestRunnerApi api; readonly string xml;
            internal Callbacks(TestRunnerApi api, string xml) { this.api = api; this.xml = xml; }
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, xml);
                    Write("completed", result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount, result.PassCount, result.FailCount, result.SkipCount, "");
                }
                catch (Exception e) { Write("error", 0, 0, 0, 0, e.Message); }
                finally { Done(); }
            }
            public void OnError(string message) { Write("error", 0, 0, 0, 0, message); Done(); }
            void Done() { api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(api); active = null; }
        }
    }
}
