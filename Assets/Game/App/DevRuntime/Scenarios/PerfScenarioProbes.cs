#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.App.DevRuntime.Perf;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    public static class PerfScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount => 0;
            internal Probe(string name) { Name = name; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
                => Number(Name == "fx.drained" ? DevPerfCounters.Fx(ctx)?.EventsDrained ?? 0 : DevPerfCounters.Audio(ctx)?.ActiveSfxVoices ?? 0);
        }
        internal static void Register(ScenarioProbeRegistry registry)
        { registry.Register(new Probe("fx.drained")); registry.Register(new Probe("sfx.voices")); }
    }
}
#endif
