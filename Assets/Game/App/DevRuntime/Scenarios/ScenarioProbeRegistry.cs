#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Explicit, extendable probe vocabulary (SPEC-70 §6.2); unknown names fail during parsing.</summary>
    public sealed class ScenarioProbeRegistry
    {
        readonly Dictionary<string, IScenarioProbe> probes = new Dictionary<string, IScenarioProbe>(StringComparer.Ordinal);
        public ScenarioProbeRegistry(params IScenarioProbe[] entries) { foreach (var probe in entries) Register(probe); }
        public void Register(IScenarioProbe probe)
        {
            if (probe == null || string.IsNullOrEmpty(probe.Name) || probe.ArgCount < 0) throw new ArgumentException("invalid probe");
            if (!probes.TryAdd(probe.Name, probe)) throw new ArgumentException("duplicate probe '" + probe.Name + "'");
        }
        public bool TryGet(string name, out IScenarioProbe probe) => probes.TryGetValue(name, out probe);
        public static ScenarioProbeRegistry CreateDefault()
        {
            var registry = new ScenarioProbeRegistry(); BuiltInScenarioProbes.Register(registry); PlayerScenarioProbes.Register(registry); GridScenarioProbes.Register(registry); StateScenarioProbes.Register(registry); return registry;
        }
    }
}
#endif
