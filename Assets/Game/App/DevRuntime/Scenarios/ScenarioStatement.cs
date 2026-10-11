#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>One parsed, location-preserving scenario instruction (SPEC-70 §6.2).</summary>
    public sealed class ScenarioStatement
    {
        public enum StatementKind : byte { Command, WaitCondition, WaitTicks, WaitFrames, Assert, Let, ExpectErrors }
        public StatementKind Kind { get; internal set; }
        public int Line { get; internal set; }
        public string Text { get; internal set; }
        public string ExpressionText { get; internal set; }
        public ScenarioExpression Expression { get; internal set; }
        public string Name { get; internal set; }
        public string Message { get; internal set; }
        public double TimeoutSeconds { get; internal set; } = 30;
        public long Count { get; internal set; }
    }
}
#endif
