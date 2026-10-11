#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Implements SPEC-70 §4.</summary>
    public enum ScenarioValueKind : byte { Number = 0, Bool = 1, String = 2 }
}
#endif
