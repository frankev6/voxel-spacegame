using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;

namespace Game.Editor.Content.Validators
{
    /// <summary>SPEC-33 ENC-002: faction/config cardinality, symmetric stance consistency, territory ownership and hostility notices.</summary>
    public sealed class EncounterContentValidator : IContentDatabaseValidator
    {
        public int Order => 330;
        public void Validate(ContentCatalog catalog, ValidationReport report)
        {
            var factions = catalog.All<FactionDefinition>();
            int players = 0;
            foreach (var faction in factions) if (faction.IsPlayerFaction) players++;
            if (players != 1) report.Error("ENC-E001", "Assets/Game/Content/Factions", "isPlayerFaction", "Exactly one player faction is required; found " + players + ".");
            if (factions.Count > 64) report.Error("ENC-E002", "Assets/Game/Content/Factions", "", "At most 64 factions are supported.");
            var pairs = new Dictionary<(FactionDefinition, FactionDefinition), (FactionStanceValue Stance, FactionDefinition Owner)>();
            var owners = new Dictionary<AuthoredStarSystemDefinition, FactionDefinition>();
            foreach (var faction in factions)
            {
                if (!faction.IsPlayerFaction && faction.Stances != null)
                    for (int i = 0; i < faction.Stances.Count; i++)
                    {
                        var entry = faction.Stances[i];
                        if (entry.other == null || entry.other == faction || entry.other.IsPlayerFaction || !Enum.IsDefined(typeof(FactionStanceValue), entry.stance)) continue;
                        var key = string.CompareOrdinal(faction.IdString, entry.other.IdString) < 0 ? (faction, entry.other) : (entry.other, faction);
                        if (pairs.TryGetValue(key, out var previous))
                        {
                            if (previous.Owner != faction && previous.Stance != entry.stance)
                                report.Error("ENC-E003", Path(faction), "stances[" + i + "]", "The reverse faction-pair declaration has a different stance.");
                        }
                        else pairs.Add(key, (entry.stance, faction));
                    }
                if (faction.OwnedStarSystems == null) continue;
                for (int i = 0; i < faction.OwnedStarSystems.Count; i++)
                {
                    var system = faction.OwnedStarSystems[i];
                    if (system == null) continue;
                    if (owners.TryGetValue(system, out var previous) && previous != faction)
                        report.Error("ENC-E005", Path(faction), "ownedStarSystems[" + i + "]", "Authored star system is already owned by " + previous.IdString + ".");
                    else owners[system] = faction;
                }
            }
            var configs = catalog.All<EncounterConfigDefinition>();
            if (configs.Count != 1)
                report.Error("ENC-E020", "Assets/Game/Content/Config", "", "Exactly one EncounterConfigDefinition (config_encounters) is required; found " + configs.Count + ".");
            else if (configs[0].IdString != "config_encounters")
                report.Error("ENC-E020", Path(configs[0]), "id", "Encounter config must have ID config_encounters.");
            if (configs.Count != 1) return;
            foreach (var encounter in catalog.All<EncounterDefinition>())
            {
                var faction = encounter.Faction;
                if (faction == null) continue; // ENC-E010 owns this diagnostic.
                bool hostile = !faction.IsPlayerFaction && faction.BasePlayerRelation <= configs[0].HostileAtOrBelow;
                if (!hostile && encounter.DirectorWeight > 0 && (encounter.Kind == EncounterKind.Ambush || encounter.Kind == EncounterKind.Patrol))
                    report.Warning("ENC-W001", Path(encounter), "faction", "Director combat encounter faction is not Hostile at its base relation.");
                if (!hostile && encounter.Kind == EncounterKind.Story)
                    report.Warning("ENC-W002", Path(encounter), "faction", "Story/StartCombat encounter faction is not Hostile at its base relation.");
            }
        }
        static string Path(ContentDefinition definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(path) ? "content:" + definition.IdString : path;
        }
    }
}
