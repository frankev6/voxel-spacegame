using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using UnityEditor;

namespace Game.Editor.Content.Validators
{
    /// <summary>Database-wide SPEC-30 §5.9 checks; definitions own their local field checks.</summary>
    public sealed class GalaxyContentValidator : IContentDatabaseValidator
    {
        public int Order => 200;

        public void Validate(ContentCatalog catalog, ValidationReport report)
        {
            var definitions = catalog.All<GalaxyGenerationDefinition>();
            if (definitions.Count != 1)
                report.Error("GAL-E001", definitions.Count > 0 ? Path(definitions[0]) : "ContentDatabase", "definitions", "There must be exactly one GalaxyGenerationDefinition.");
            foreach (var gen in definitions)
            {
                var scope = report.For(Path(gen));
                if (gen.GeneratorVersion != GalaxyConstants.CurrentGeneratorVersion)
                    scope.Error("GAL-E002", "generatorVersion", "Generator version must equal the current supported version.");
                if (gen.ProceduralStarSystemCount < 0 || gen.ProceduralStarSystemCount > GalaxyConstants.MaxProceduralStarSystems)
                    scope.Error("GAL-E017", "proceduralStarSystemCount", "Procedural star system count is outside the supported bound.");
                var systems = gen.AuthoredStarSystems;
                if (systems == null || systems.Count > GalaxyConstants.MaxAuthoredStarSystems)
                    scope.Error("GAL-E004", "authoredStarSystems", "Authored star system count must be at most 256.");
                var distinct = new HashSet<AuthoredStarSystemDefinition>();
                var ids = new Dictionary<uint, AuthoredStarSystemDefinition>();
                var valid = new List<AuthoredStarSystemDefinition>();
                if (systems != null)
                    for (int i = 0; i < systems.Count; i++)
                    {
                        var system = systems[i];
                        if (system == null || !distinct.Add(system))
                        { scope.Error("GAL-E004", $"authoredStarSystems[{i}]", "Authored list must have no nulls or duplicates."); continue; }
                        valid.Add(system);
                        uint id = StarSystemId.FromAuthored(system.Id.Hash).Value;
                        if (ids.TryGetValue(id, out var first))
                            scope.Error("GAL-E021", $"authoredStarSystems[{i}]", $"Authored star systems '{first.IdString}' and '{system.IdString}' map to the same StarSystemId.");
                        else ids.Add(id, system);
                    }
                int authoredLaneCount = gen.AuthoredLanes?.Count ?? 0;
                // Pools may reuse a template; the authored list may also use a template absent from the pool.
                var templates = new HashSet<StarSystemTemplateDefinition>(catalog.All<StarSystemTemplateDefinition>());
                if (gen.TemplatePool != null)
                    foreach (var entry in gen.TemplatePool) if (entry.template != null) templates.Add(entry.template);
                foreach (var system in valid) if (system.Template != null) templates.Add(system.Template);
                foreach (var template in templates)
                {
                    long bound = 1L + (long)template.MaxPlanets * (1L + template.MaxMoonsPerPlanet) + template.MaxBelts + template.MaxStations + template.MaxPointsOfInterest + gen.MaxLaneDegree + authoredLaneCount;
                    if (bound > GalaxyConstants.MaxBodiesPerStarSystem)
                        report.Error("GAL-E014", Path(template), "maxPlanets", "Template's worst-case body count exceeds 512, including lane gates.");
                }
                foreach (var system in valid)
                {
                    int incidentLanes = 0;
                    if (gen.AuthoredLanes != null)
                        foreach (var lane in gen.AuthoredLanes)
                            if (lane != null && (lane.a == system || lane.b == system)) incidentLanes++;
                    if (1L + (system.Bodies?.Count ?? 0) + incidentLanes > GalaxyConstants.MaxBodiesPerStarSystem)
                        report.Error("GAL-E014", Path(system), "bodies", "Authored star system's body and gate count exceeds 512.");
                }
                for (int i = 0; i < valid.Count; i++)
                {
                    var system = valid[i];
                    double x = system.PositionLy.x, y = system.PositionLy.y;
                    if (Math.Sqrt(x * x + y * y) > gen.GalaxyRadiusLy)
                        report.Warning("GAL-W003", Path(system), "positionLy", "Authored star system lies outside the galaxy radius.");
                    for (int j = 0; j < i; j++)
                        if (Distance(system, valid[j]) < gen.MinStarSystemSpacingLy)
                            report.Warning("GAL-W001", Path(system), "positionLy", "Authored star systems are closer than the minimum spacing.");
                }
                if (gen.AuthoredLanes != null)
                    for (int i = 0; i < gen.AuthoredLanes.Count; i++)
                    {
                        var lane = gen.AuthoredLanes[i];
                        if (lane != null && lane.a != null && lane.b != null && lane.a != lane.b && Distance(lane.a, lane.b) > gen.MaxLaneLengthLy)
                            scope.Warning("GAL-W002", $"authoredLanes[{i}]", "Authored lane exceeds the maximum procedural lane length.");
                    }
            }
        }

        static double Distance(AuthoredStarSystemDefinition a, AuthoredStarSystemDefinition b)
        {
            double dx = (double)a.PositionLy.x - b.PositionLy.x, dy = (double)a.PositionLy.y - b.PositionLy.y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        static string Path(ContentDefinition definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(path) ? "content:" + definition.IdString : path;
        }
    }
}
