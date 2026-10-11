using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.DevTools.Editor.Scenarios;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>Scenario lifecycle as pollable CLI JSON (SPEC-70 §6.3).</summary>
    public static class ScenarioCliCommands
    {
        [CliCommand("gh_scenario", "Start a scenario, entering Play from Boot when needed.")]
        public static string Start([CliArg("file", "Scenario file path")] string file) => Status(EditorScenarioDriver.Start(file));
        [CliCommand("gh_scenario_status", "Read a scenario outcome and result path.")]
        public static string Poll([CliArg("run", "Run id")] int run) => Status(EditorScenarioDriver.Get(run));
        [CliCommand("gh_scenario_stop", "Stop a running scenario and write its result.")]
        public static string Stop([CliArg("run", "Run id")] int run) => Status(EditorScenarioDriver.Stop(run));
        static string Status(ScenarioRun run)
        {
            if (run == null) return "{\"outcome\":\"error\",\"exitCode\":2,\"message\":\"unknown run\",\"jsonPath\":\"\"}";
            return "{\"runId\":" + run.RunId + ",\"name\":" + DevJsonWriter.Quote(run.Name) + ",\"outcome\":" + DevJsonWriter.Quote(run.Outcome.ToString().ToLowerInvariant()) +
                ",\"exitCode\":" + run.ExitCode + ",\"line\":" + run.CurrentLine + ",\"seconds\":" + DevJsonWriter.Number(run.ElapsedSeconds) +
                ",\"message\":" + DevJsonWriter.Quote(run.Message) + ",\"jsonPath\":" + DevJsonWriter.Quote(run.ResultJsonPath) + "}";
        }
    }
}
