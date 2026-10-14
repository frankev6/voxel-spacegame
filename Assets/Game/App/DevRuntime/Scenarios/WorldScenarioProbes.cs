#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Galaxy.Services;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>SPEC-70 DEV-018: read current identity from canonical galaxy state; absent means -1.</summary>
    public static class WorldScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name => "star_system";
            public int ArgCount => 0;
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
                => Number(ctx.TryGetService<GalaxyService>(out var galaxy) && galaxy.State.CurrentStarSystem.IsValid
                    ? galaxy.State.CurrentStarSystem.Value : -1d);
        }
        internal static void Register(ScenarioProbeRegistry registry) => registry.Register(new Probe());
    }
}
#endif
