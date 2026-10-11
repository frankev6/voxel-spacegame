#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Parsed headers and ordered statements (SPEC-70 §6.2). Defaults: active, 300-second timeout.</summary>
    public sealed class ScenarioScript
    {
        public string Name { get; internal set; }
        public string Description { get; internal set; } = "";
        public string Status { get; internal set; } = "active";
        public double TimeoutSeconds { get; internal set; } = 300;
        public IReadOnlyList<string> Tags { get; internal set; }
        public IReadOnlyList<ScenarioStatement> Statements { get; internal set; }
    }
}
#endif
