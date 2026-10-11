#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Scenario probe: a named value source for expressions (player.pos.x, fact("x"), ...). Implements SPEC-70 §4.</summary>
    public interface IScenarioProbe
    {
        string Name { get; }                             // dotted path or function name: "player.pos.x", "fact"
        int ArgCount { get; }                            // 0 for paths
        ScenarioValue Read(Game.App.DevRuntime.DevContext ctx, ReadOnlySpan<ScenarioValue> args);
    }
}
#endif
