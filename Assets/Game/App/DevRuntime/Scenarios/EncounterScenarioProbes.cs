#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Services;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>ENC-012: real director/faction queries and run-relative, typed defeat events.</summary>
    public static class EncounterScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount => Name.StartsWith("encounters.", StringComparison.Ordinal) ? 0 : 1;
            internal Probe(string name) => Name = name;
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                if (ArgCount == 0)
                {
                    if (!ctx.TryGetService<EncounterDirectorService>(out var director)) throw new InvalidOperationException("encounter director not available");
                    return Number(Name == "encounters.active" ? director.ActiveInstanceCount : director.AliveShipCount);
                }
                string id = RequireString(args[0]);
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("probe '" + Name + "' expects a content id");
                if (!ctx.TryGetService<ContentService>(out var content)) throw new InvalidOperationException("content service not available");
                if (Name == "encounter_defeated")
                {
                    if (!content.Catalog.TryGet<EncounterDefinition>(new ContentId(id), out var encounter)) throw new ArgumentException("unknown encounter '" + id + "'");
                    return Number(ctx.EncounterDefeatedCount(encounter.Id.Hash));
                }
                if (!ctx.TryGetService<FactionService>(out var factions)) throw new InvalidOperationException("faction service not available");
                if (!content.Catalog.TryGet<FactionDefinition>(new ContentId(id), out var faction) || factions.Indices.IndexOf(faction) == 255)
                    throw new ArgumentException("unknown faction '" + id + "'");
                return Name == "stance" ? Text(factions.GetPlayerStance(faction).ToString()) : Number(factions.GetRelation(faction));
            }
        }
        internal static void Register(ScenarioProbeRegistry registry)
        {
            foreach (string name in new[] { "encounters.active", "encounters.ships", "relation", "stance", "encounter_defeated" }) registry.Register(new Probe(name));
        }
    }
}
#endif
