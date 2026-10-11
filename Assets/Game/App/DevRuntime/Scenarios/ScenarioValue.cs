#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Implements SPEC-70 §4. The constructor is not in §4; readonly fields need one.</summary>
    public readonly struct ScenarioValue
    {
        public readonly ScenarioValueKind Kind; public readonly double Number; public readonly bool Bool; public readonly string String;
        public ScenarioValue(ScenarioValueKind kind, double number, bool @bool, string @string) { Kind = kind; Number = number; Bool = @bool; String = @string; }
    }
}
#endif
