#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Implements SPEC-70 §4. One scenario run (DEV-006 fills it).</summary>
    public sealed class ScenarioRun
    {
        public string Name { get; }  public string FilePath { get; }  public int RunId { get; }
        public ScenarioOutcome Outcome { get; }  public int ExitCode { get; }      // 0/1/2 per §3.8; Ignored → 0
        public string Message { get; }  public int CurrentLine { get; }  public double ElapsedSeconds { get; }
        public string ResultJsonPath { get; }                                     // Logs/playtest/scenarios/<name>-<UTC yyyyMMdd-HHmmss>.json
    }
}
#endif
