using System;
using System.Collections.Generic;
using Game.Data.Validation;
using Game.Editor.Migration.Dto;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.7. Typed legacy data that other specs convert. Built once per run, after this spec's own converters.</summary>
    public sealed class LegacyHandOff
    {
        public static LegacyHandOff Build(LegacyContentIndex index, LegacyIdMap map, ValidationReport report)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (report == null) throw new ArgumentNullException(nameof(report));
            var links = new List<LegacyBodyEncounterLink>();
            var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in index.Files)
            {
                var dto = ReadBody(file, report);
                if (dto == null) continue;
                // $name is a specified map key, not a new v2 identity; it disambiguates water and blank star entityIds.
                if (!map.TryMap(LegacyIdKind.Body, dto.Name, out var bodyId) || bodyId == null)
                    map.TryMap(LegacyIdKind.Body, dto.EntityId, out bodyId);
                if (bodyId == null) { Missing(report, file, "entityId", "No body mapping for " + dto.Name + " / " + dto.EntityId); continue; }
                bodies.Add(file.AssetPath, bodyId);
                AddBodyLinks(index, file, dto, bodyId, links, report);
            }

            var fleets = new List<LegacyFleetTemplate>();
            foreach (var file in index.OfType("NPCFleetEntity"))
            {
                var dto = LegacyJson.Read<LegacyNpcFleetEntityDto>(file, report);
                if (dto == null) continue;
                if (!TryMap(map, LegacyIdKind.Encounter, dto.EntityId, file, "entityId", report, out var suggestedId)) continue;
                bool numbersValid = TryFloat(dto.DetectionRadius, file, "detectionRadius", report, out var detection);
                numbersValid &= TryFloat(dto.PatrolRadius, file, "patrolRadius", report, out var patrol);
                numbersValid &= TryFloat(dto.MovementSpeed, file, "movementSpeed", report, out var speed);
                if (!numbersValid) continue;
                var fleet = new LegacyFleetTemplate
                {
                    LegacyEntityId = dto.EntityId, SuggestedId = suggestedId, Allegiance = dto.Allegiance,
                    DetectionRadius = detection, PatrolRadius = patrol, MovementSpeed = speed,
                    ChasesPlayer = dto.ChasesPlayer, UseFactionDoctrine = dto.UseFactionDoctrine,
                    AutoInteractOnContact = dto.AutoInteractOnContact,
                    DoctrineMinShips = dto.DoctrineMinShips, DoctrineMaxShips = dto.DoctrineMaxShips,
                    LegacyNarrativeEncounterId = dto.EntityId == "npc_traveling_merchant" ? "event_merchant_shady" : null
                };
                fleet.Formations = ReadFormations(index, map, file, dto, fleet.LegacyNarrativeEncounterId, report);
                fleets.Add(fleet);
            }

            var systems = new List<(LegacyFile File, LegacySolarSystemDataDto Dto)>();
            var systemsByPath = new Dictionary<string, LegacySolarSystemDataDto>(StringComparer.Ordinal);
            foreach (var file in index.OfType("SolarSystemData"))
            {
                var dto = LegacyJson.Read<LegacySolarSystemDataDto>(file, report);
                if (dto == null) continue;
                if (string.IsNullOrEmpty(dto.SystemId))
                {
                    if (dto.Name == "ExtractStablePoint") report.Info("MIG-I091", file.RelativePath, "systemId", "ExtractStablePoint is a roguelike leftover with no systemId; skipped.");
                    else Missing(report, file, "systemId", "Star-system template has no systemId.");
                    continue;
                }
                // These mappings deliberately have no v2 ID: the receiving galaxy spec authors the star systems.
                if (!map.TryMap(LegacyIdKind.StarSystem, dto.SystemId, out _))
                { Missing(report, file, "systemId", "Unknown legacy star system " + dto.SystemId); continue; }
                systems.Add((file, dto)); systemsByPath.Add(file.AssetPath, dto);
            }
            var templates = new List<LegacyStarSystemTemplate>();
            foreach (var source in systems)
            {
                var dto = source.Dto; var file = source.File;
                var stars = new List<string>();
                var pool = new List<(string BodyId, float Weight, int MaxCount)>();
                var connected = new List<string>();
                var starRefs = dto.StarEntities ?? Array.Empty<LegacyRef>();
                for (int i = 0; i < starRefs.Length; i++)
                    if (ResolveBody(index, bodies, starRefs[i], file, "starEntities[" + i + "]", report, out var starId)) stars.Add(starId);
                var entries = dto.EntityPool ?? Array.Empty<LegacySolarSystemDataDto.PoolEntry>();
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i]; string field = "entityPool[" + i + "]";
                    if (entry == null) { Invalid(report, file, field, "Null entity-pool entry."); continue; }
                    if (ResolveBody(index, bodies, entry.Entity, file, field + ".entity", report, out var poolBodyId) &&
                        TryFloat(entry.Weight, file, field + ".weight", report, out var weight)) pool.Add((poolBodyId, weight, entry.MaxCount));
                }
                var connections = dto.ConnectedSystems ?? Array.Empty<LegacyRef>();
                for (int i = 0; i < connections.Length; i++)
                {
                    string field = "connectedSystems[" + i + "]";
                    if (!Resolve(index, connections[i], file, field, report, out var target)) continue;
                    if (systemsByPath.TryGetValue(target.AssetPath, out var system)) connected.Add(system.SystemId);
                    else Missing(report, file, field, "Reference is not an included star-system template.");
                }
                templates.Add(new LegacyStarSystemTemplate
                {
                    LegacySystemId = dto.SystemId, Dto = dto, StarBodyIds = stars.AsReadOnly(),
                    EntityPool = pool.AsReadOnly(), ConnectedSystemIds = connected.AsReadOnly()
                });
            }
            return new LegacyHandOff
            {
                BodyEncounterLinks = links.AsReadOnly(), Fleets = fleets.AsReadOnly(), StarSystems = templates.AsReadOnly(),
                Galaxy = ReadSingle<LegacyGalaxyDataDto>(index, "GalaxyData", report),
                OpenWorld = ReadSingle<LegacyOpenWorldGalaxyConfigDto>(index, "OpenWorldGalaxyConfig", report)
            };
        }

        public IReadOnlyList<LegacyBodyEncounterLink> BodyEncounterLinks { get; private set; } = Array.Empty<LegacyBodyEncounterLink>();
        public IReadOnlyList<LegacyFleetTemplate> Fleets { get; private set; } = Array.Empty<LegacyFleetTemplate>();
        public IReadOnlyList<LegacyStarSystemTemplate> StarSystems { get; private set; } = Array.Empty<LegacyStarSystemTemplate>();
        public LegacyGalaxyDataDto Galaxy { get; private set; }
        public LegacyOpenWorldGalaxyConfigDto OpenWorld { get; private set; }
        public const string NarrativeFolder = "narrative";   // Import/Legacy/narrative/*.json (authoritative narrative source)

        static LegacySystemEntityDto ReadBody(LegacyFile file, ValidationReport report) => file.Type switch
        {
            "SolarSystemEntity" => LegacyJson.Read<LegacySystemEntityDto>(file, report),
            "PlanetEntity" => LegacyJson.Read<LegacyPlanetEntityDto>(file, report),
            "StarEntity" => LegacyJson.Read<LegacyStarEntityDto>(file, report),
            "Assets.Game.Core.Galaxy_Map.HyperlaneEntity" => LegacyJson.Read<LegacyHyperlaneEntityDto>(file, report),
            _ => null // Fleets are encounters, never body templates.
        };

        static void AddBodyLinks(LegacyContentIndex index, LegacyFile file, LegacySystemEntityDto dto, string bodyId,
            List<LegacyBodyEncounterLink> links, ValidationReport report)
        {
            string overrideId = dto.EncounterIdOverride ?? string.Empty;
            bool overrideInPool = false;
            var entries = dto.EncounterPool?.Encounters ?? Array.Empty<LegacySystemEntityDto.WeightedEncounter>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; string field = "encounterPool.encounters[" + i + "]";
                if (entry == null) { Invalid(report, file, field, "Null encounter-pool entry."); continue; }
                if (!Resolve(index, entry.Encounter, file, field + ".encounter", report, out var encounter)) continue;
                if (encounter.Type != "NarrativeEncounter" || string.IsNullOrEmpty(encounter.Name))
                { Invalid(report, file, field + ".encounter", "Body link must reference a named narrative encounter."); continue; }
                if (!TryFloat(entry.ProbabilityWeight, file, field + ".probabilityWeight", report, out var weight)) continue;
                // Association labels only: narrative content still comes from narrative/*.json, never these importer copies (Q18).
                links.Add(new LegacyBodyEncounterLink(bodyId, encounter.Name, weight, dto.AutoInteractOnContact, overrideId));
                overrideInPool |= string.Equals(encounter.Name, overrideId, StringComparison.Ordinal);
            }
            if (overrideId.Length > 0 && !overrideInPool)
                links.Add(new LegacyBodyEncounterLink(bodyId, overrideId, 1, dto.AutoInteractOnContact, overrideId));
        }

        static IReadOnlyList<IReadOnlyList<string>> ReadFormations(LegacyContentIndex index, LegacyIdMap map, LegacyFile file,
            LegacyNpcFleetEntityDto fleet, string narrativeId, ValidationReport report)
        {
            var result = new List<IReadOnlyList<string>>();
            var entries = fleet.EncounterPool?.Encounters ?? Array.Empty<LegacySystemEntityDto.WeightedEncounter>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; string field = "encounterPool.encounters[" + i + "]";
                if (entry == null) { Invalid(report, file, field, "Null encounter-pool entry."); continue; }
                if (!Resolve(index, entry.Encounter, file, field + ".encounter", report, out var encounter)) continue;
                if (encounter.Type == "NarrativeEncounter" && narrativeId != null && encounter.Name == narrativeId) continue;
                if (encounter.Type != "CombatEncounter") { Invalid(report, file, field, "Fleet pool entry is not a combat formation or the merchant narrative."); continue; }
                var combat = LegacyJson.Read<LegacyCombatEncounterDto>(encounter, report);
                if (combat == null) continue;
                var formations = combat.PossibleFormations ?? Array.Empty<LegacyCombatEncounterDto.Formation>();
                for (int f = 0; f < formations.Length; f++)
                {
                    var formation = formations[f]; string formationField = "possibleFormations[" + f + "]";
                    if (formation?.Ships == null) { Invalid(report, encounter, formationField, "Formation has no ship list."); continue; }
                    var ships = new List<string>(); bool valid = true;
                    for (int s = 0; s < formation.Ships.Length; s++)
                    {
                        string shipField = formationField + ".ships[" + s + "]";
                        if (!Resolve(index, formation.Ships[s], encounter, shipField, report, out var shipFile)) { valid = false; continue; }
                        if (shipFile.Type != "ShipItem") { Invalid(report, encounter, shipField, "Formation reference is not a ship."); valid = false; continue; }
                        var ship = LegacyJson.Read<LegacyShipItemDto>(shipFile, report);
                        if (ship == null || !TryMap(map, LegacyIdKind.Ship, ship.RegistryKey, encounter, shipField, report, out var shipId))
                        { valid = false; continue; }
                        ships.Add(shipId);
                    }
                    if (valid) result.Add(ships.AsReadOnly());
                }
            }
            return result.AsReadOnly();
        }

        static bool ResolveBody(LegacyContentIndex index, Dictionary<string, string> bodies, LegacyRef reference,
            LegacyFile source, string field, ValidationReport report, out string bodyId)
        {
            bodyId = null;
            if (!Resolve(index, reference, source, field, report, out var target)) return false;
            if (bodies.TryGetValue(target.AssetPath, out bodyId)) return true;
            Missing(report, source, field, "Reference has no mapped body template."); return false;
        }

        static bool Resolve(LegacyContentIndex index, LegacyRef reference, LegacyFile source, string field,
            ValidationReport report, out LegacyFile target)
        {
            if (index.TryResolve(reference, out target)) return true;
            Missing(report, source, field, "Unresolved legacy reference " + reference.Path); return false;
        }

        static bool TryMap(LegacyIdMap map, LegacyIdKind kind, string key, LegacyFile source, string field,
            ValidationReport report, out string id)
        {
            if (map.TryMap(kind, key, out id) && id != null) return true;
            Missing(report, source, field, "No v2 " + kind + " mapping for " + key); return false;
        }

        static bool TryFloat(double value, LegacyFile source, string field, ValidationReport report, out float result)
        {
            result = 0;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -float.MaxValue || value > float.MaxValue)
            { Invalid(report, source, field, "Value cannot be represented by the hand-off float field."); return false; }
            result = (float)value; return true;
        }

        static T ReadSingle<T>(LegacyContentIndex index, string type, ValidationReport report) where T : LegacyDto
        {
            var files = index.OfType(type);
            if (files.Count == 1) return LegacyJson.Read<T>(files[0], report);
            report.Error("MIG-E002", "content", type, "Expected exactly one " + type + " file, found " + files.Count); return null;
        }

        static void Missing(ValidationReport report, LegacyFile source, string field, string message)
            => report.Error("MIG-E003", source.RelativePath, field, message);
        static void Invalid(ValidationReport report, LegacyFile source, string field, string message)
            => report.Error("MIG-E002", source.RelativePath, field, message);
    }
}
