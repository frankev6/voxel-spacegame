using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Dto;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 MIG-014: identity stubs only; SPEC-30 authors the world and visual fields.</summary>
    public sealed class BodyTemplateConverter : ILegacyConverter
    {
        const string PlaceholderDescription = "This is a placeholder description blah blah blah";
        public string Name => "Bodies";
        public int Order => 420;

        public void Convert(LegacyMigrationContext context)
        {
            foreach (var file in context.Index.Files)
            {
                var dto = ReadBody(file, context.Report);
                if (dto == null) continue;
                // The map explicitly uses $name first: water duplicates ice's entityId and stars have blank entityIds.
                if (!context.IdMap.TryMap(LegacyIdKind.Body, dto.Name, out string id) || id == null)
                    context.IdMap.TryMap(LegacyIdKind.Body, dto.EntityId, out id);
                if (id == null || !Rows.TryGetValue(id, out var row))
                {
                    context.Report.Error("MIG-E003", file.RelativePath, "entityId", "No canonical body mapping/stub row for " + dto.Name + " / " + dto.EntityId);
                    continue;
                }
                try
                {
                    context.Writer.CreateOrUpdateFields<BodyTemplateDefinition>("Bodies", id, so =>
                    {
                        var writer = context.Writer;
                        writer.Set(so, "displayName", p => p.stringValue = row.Name);
                        writer.Set(so, "role", p => p.intValue = (int)row.Role);
                        writer.Set(so, "description", p => p.stringValue = dto.Description == PlaceholderDescription ? "" : dto.Description ?? "");
                        writer.Set(so, "storyTag", p => p.stringValue = row.Tag);
                    }, out _);
                    // BodyTemplate has no map-colour field; the visual's UI token is authored by SPEC-30, never a raw legacy colour.
                    context.Report.Info("MIG-I090", file.RelativePath, "color", "Legacy map colour has no BodyTemplateDefinition field; SPEC-30 owns BodyVisualDefinition.mapColorToken.");
                    context.Report.Info("MIG-I091", file.RelativePath, "", "Dropped legacy prefab, mapIcon, ownerFactionIdOverride, encounterPool, encounterIdOverride, autoInteractOnContact, orbitType, orbitSpeedMultiplier, showOrbitLine, spawnType, minOrbitRadius, maxOrbitRadius, canHaveSatellites, spawnRequirement, spawnBlocker" + DroppedTypeFields(file.Type) +
                        (dto.Description == PlaceholderDescription ? "; cleared placeholder description" : "") + ". SPEC-30 re-authors world fields; encounters use the typed hand-off.");
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Body stub conversion failed: " + e.Message); }
            }
        }

        static string DroppedTypeFields(string type) => type switch
        {
            "PlanetEntity" => ", planetType, preferredOrbitDistance, minRadius, maxRadius, hasAtmosphere, atmosphereColor, hasRings, planetTextures",
            "StarEntity" => ", minTemperature, maxTemperature, minScale, maxScale, minLuminosity, maxLuminosity, minPlanetOrbitRadius, maxSystemRadius, galaxyMapPrefab",
            "Assets.Game.Core.Galaxy_Map.HyperlaneEntity" => ", routeStrength, duration",
            _ => ""
        };

        static LegacySystemEntityDto ReadBody(LegacyFile file, ValidationReport report) => file.Type switch
        {
            "SolarSystemEntity" => LegacyJson.Read<LegacySystemEntityDto>(file, report),
            "PlanetEntity" => LegacyJson.Read<LegacyPlanetEntityDto>(file, report),
            "StarEntity" => LegacyJson.Read<LegacyStarEntityDto>(file, report),
            "Assets.Game.Core.Galaxy_Map.HyperlaneEntity" => LegacyJson.Read<LegacyHyperlaneEntityDto>(file, report),
            _ => null // Fleet entities belong to encounters, not the 20 body templates.
        };

        // SPEC-72 §4.6 names/roles and SPEC-42 §4.10 exact narrative tags.
        static readonly IReadOnlyDictionary<string, (string Name, BodyRole Role, string Tag)> Rows =
            new Dictionary<string, (string, BodyRole, string)>(StringComparer.Ordinal)
            {
                ["body_star"] = ("Star", BodyRole.Star, "star"),
                ["body_star_blackhole"] = ("Black Hole", BodyRole.Star, "black_hole"),
                ["body_planet_barren"] = ("Barren Planet", BodyRole.Planet, "barren_planet"),
                ["body_planet_gas_giant"] = ("Gas Giant", BodyRole.Planet, "gas_giant"),
                ["body_planet_ice"] = ("Ice Planet", BodyRole.Planet, "ice_planet"),
                ["body_planet_ice_giant"] = ("Ice Giant", BodyRole.Planet, "ice_giant"),
                ["body_planet_rocky"] = ("Rocky Planet", BodyRole.Planet, "rocky_planet"),
                ["body_planet_terrestrial"] = ("Terrestrial Planet", BodyRole.Planet, "terrestrial_planet"),
                ["body_planet_water"] = ("Ocean Planet", BodyRole.Planet, "ocean_planet"),
                ["body_moon"] = ("Moon", BodyRole.Moon, "moon"),
                ["body_station"] = ("Station", BodyRole.Station, "station"),
                ["body_station_ark"] = ("ARK Station", BodyRole.Station, "ark_station"),
                ["body_station_abandoned"] = ("Abandoned Station", BodyRole.Station, "abandoned_station"),
                ["body_asteroid_belt"] = ("Asteroid Belt", BodyRole.Belt, "asteroid_belt"),
                ["body_gate_active"] = ("Active Gate", BodyRole.LaneGate, "gate_active"),
                ["body_gate_dormant"] = ("Dormant Gate", BodyRole.LaneGate, "gate_dormant"),
                ["body_jump_point"] = ("Jump Point", BodyRole.LaneGate, "jump_point"),
                ["body_derelict_ship"] = ("Derelict Ship", BodyRole.PointOfInterest, "derelict"),
                ["body_debris_field"] = ("Debris Field", BodyRole.PointOfInterest, "debris_field"),
                ["body_resource_cache"] = ("Resource Cache", BodyRole.PointOfInterest, "resource_cache"),
            };
    }
}
