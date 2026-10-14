#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;
namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>DEV-014: the current owner-written interaction target; no geometric approximation.</summary>
    public static class InteractionScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount => 0;
            internal Probe(string name) => Name = name;
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args) => Name == "interaction.target"
                ? Text(DevBodyQuery.TargetName(ctx)) : Boolean(ctx.TryGetInteractionTarget(out var target) && target.Target != Unity.Entities.Entity.Null && target.InRange != 0);
        }
        internal static void Register(ScenarioProbeRegistry registry)
        { registry.Register(new Probe("interaction.target")); registry.Register(new Probe("interaction.inrange")); }
    }
}
#endif
