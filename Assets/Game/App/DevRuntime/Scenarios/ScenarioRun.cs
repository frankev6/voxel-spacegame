#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Stable run result retained across Play exit by the Editor driver (SPEC-70 §4).</summary>
    public sealed class ScenarioRun
    {
        public string Name { get; internal set; }
        public string FilePath { get; internal set; }
        public int RunId { get; internal set; }
        public ScenarioOutcome Outcome { get; internal set; }
        public int ExitCode { get; internal set; }
        public string Message { get; internal set; } = "";
        public int CurrentLine { get; internal set; }
        public double ElapsedSeconds { get; internal set; }
        public string ResultJsonPath { get; internal set; } = "";
    }
}
#endif
