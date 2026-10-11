using System;
using System.Collections.Generic;
using System.IO;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.DevTools.Editor.Scenarios
{
    /// <summary>Nonblocking scenario dispatch across Boot and Play exit (SPEC-70 DEV-006).</summary>
    [InitializeOnLoad]
    public static class EditorScenarioDriver
    {
        sealed class Entry
        {
            internal ScenarioScript Script;
            internal ScenarioRun Run;
            internal ScenarioRunner Runner;
            internal double Requested;
        }
        static readonly Dictionary<int, Entry> runs = new Dictionary<int, Entry>();
        const string NextIdKey = "Game.DevTools.ScenarioNextId";
        static EditorScenarioDriver()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }
        public static ScenarioRun Start(string file)
        {
            int id = checked(SessionState.GetInt(NextIdKey, 0) + 1); SessionState.SetInt(NextIdKey, id);
            string path = file ?? "", name = "scenario";
            try
            {
                path = Path.GetFullPath(path); name = Path.GetFileNameWithoutExtension(path);
                var script = ScenarioParser.Parse(File.ReadAllText(path), DevCommandRegistry.CreateDefault(), ScenarioProbeRegistry.CreateDefault());
                var entry = new Entry { Script = script, Requested = EditorApplication.timeSinceStartup,
                    Run = new ScenarioRun { Name = script.Name, FilePath = path, RunId = id, Message = "waiting for dev runtime" } };
                runs.Add(id, entry);
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorSceneManager.OpenScene("Assets/Game/Scenes/Boot.unity", OpenSceneMode.Single);
                    EditorApplication.isPlaying = true;
                }
                return entry.Run;
            }
            catch (Exception e)
            {
                var run = ScenarioRunner.ErrorRun(name, path, id, e.Message);
                runs[id] = new Entry { Run = run }; return run;
            }
        }
        static void Update()
        {
            foreach (var entry in runs.Values)
            {
                if (entry.Run.Outcome != ScenarioOutcome.Running || entry.Runner != null) continue;
                if (EditorApplication.isPlaying && Game.Core.Services.TryGet<DevRuntimeHost>(out var host) && host != null)
                {
                    try { entry.Runner = host.RunScenario(entry.Script, entry.Run.FilePath, entry.Run.RunId); entry.Run = entry.Runner.Run; }
                    catch (Exception e) { Fail(entry, e.GetType().Name + ": " + e.Message); }
                }
                else if (EditorApplication.timeSinceStartup - entry.Requested > 30) Fail(entry, "dev runtime not available after 30 s");
            }
        }
        static void Fail(Entry entry, string message)
        {
            if (entry.Runner != null) entry.Runner.Abort(message);
            else entry.Run = ScenarioRunner.ErrorRun(entry.Run.Name, entry.Run.FilePath, entry.Run.RunId, message);
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
            foreach (var entry in runs.Values) if (entry.Run.Outcome == ScenarioOutcome.Running) Fail(entry, "play mode exited");
        }
        static void BeforeReload()
        {
            foreach (var entry in runs.Values) if (entry.Run.Outcome == ScenarioOutcome.Running) Fail(entry, "scripts reloaded");
        }
        public static ScenarioRun Get(int id) => runs.TryGetValue(id, out var entry) ? entry.Run : null;
        public static ScenarioRun Stop(int id)
        {
            if (!runs.TryGetValue(id, out var entry)) return null;
            if (entry.Run.Outcome == ScenarioOutcome.Running) Fail(entry, "stopped");
            return entry.Run;
        }
    }
}
