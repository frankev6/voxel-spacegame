using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Game.App.Testing;
using Game.Core;
using Game.Core.Flow;
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
        sealed class CommandEntry
        {
            internal string Line;
            internal double Requested;
            internal bool RestoreIntro, PreviousIntro;
            internal readonly TaskCompletionSource<DevCommandResult> Completion = new TaskCompletionSource<DevCommandResult>();
        }
        static readonly List<CommandEntry> commands = new List<CommandEntry>();
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
                EnterPlay();
                return entry.Run;
            }
            catch (Exception e)
            {
                var run = ScenarioRunner.ErrorRun(name, path, id, e.Message);
                runs[id] = new Entry { Run = run }; return run;
            }
        }
        static void EnterPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Boot.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        /// <summary>DEV-008: asynchronously enter Play, await the real boot, then return its runtime operation id.</summary>
        internal static Task<DevCommandResult> StartCommand(string line, bool intro)
        {
            if (EditorApplication.isPlaying)
                return Task.FromResult(Services.TryGet<DevRuntimeHost>(out var host) && host != null
                    ? host.Commands.Execute(line, host.Context) : DevCommandResult.Fail("dev runtime not available"));
            var entry = new CommandEntry { Line = line, Requested = EditorApplication.timeSinceStartup,
                RestoreIntro = !EditorApplication.isPlayingOrWillChangePlaymode, PreviousIntro = GameRootTestHooks.SkipIntroScreens };
            if (entry.RestoreIntro) GameRootTestHooks.SkipIntroScreens = !intro;
            commands.Add(entry);
            try { EnterPlay(); }
            catch (Exception e) { FinishCommand(entry, DevCommandResult.Fail(e.Message)); }
            return entry.Completion.Task;
        }
        static void FinishCommand(CommandEntry entry, DevCommandResult result)
        {
            commands.Remove(entry);
            if (entry.RestoreIntro) GameRootTestHooks.SkipIntroScreens = entry.PreviousIntro;
            entry.Completion.TrySetResult(result);
        }
        static void FailCommands(string message)
        {
            while (commands.Count > 0) FinishCommand(commands[commands.Count - 1], DevCommandResult.Fail(message));
        }
        static void Update()
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                var entry = commands[i];
                if (EditorApplication.isPlaying && Services.TryGet<DevRuntimeHost>(out var host) && host != null
                    && Services.TryGet<IGameFlow>(out var flow) && !flow.IsTransitioning && flow.CurrentScene == GameScene.MainMenu)
                    FinishCommand(entry, host.Commands.Execute(entry.Line, host.Context));
                else if (EditorApplication.timeSinceStartup - entry.Requested > 30)
                    FinishCommand(entry, DevCommandResult.Fail("dev runtime not available after 30 s"));
            }
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
            FailCommands("play mode exited");
            foreach (var entry in runs.Values) if (entry.Run.Outcome == ScenarioOutcome.Running) Fail(entry, "play mode exited");
        }
        static void BeforeReload()
        {
            FailCommands("scripts reloaded");
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
