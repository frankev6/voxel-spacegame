#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Implements SPEC-70 §4.</summary>
    public enum ScenarioOutcome : byte { Running = 0, Passed = 1, Failed = 2, Error = 3, Ignored = 4 }
}
#endif
