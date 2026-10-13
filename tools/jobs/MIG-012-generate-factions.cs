// MIG-012 lead-only recipe; outside Assets, not compiled or executed by the worker.
// Integrate ENC-002 (strict validators + existing faction_ark) and MIG-012 first.
// Copy to Assets/Game/Editor/FactionMigrationJob.cs in the validation checkout, run
// Game.Editor.Migration.FactionMigrationJob.Run through the serialized Unity workflow,
// then remove the temporary script/meta. No Force and no new faction_ark asset.
// Commit only three generated factions/metas, ContentDatabase and owned manifest hashes.
// Logs/playtest/mig012 contains preflight, output/GUID readback and rerun evidence.
// Full-run report refresh and migration gate count integration remain lead closeout.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration.Dto;
using UnityEditor;

namespace Game.Editor.Migration
{
    public static class FactionMigrationJob
    {
        const string ManifestPath = "Import/Migration/manifest.json";
        const string EvidenceRoot = "Logs/playtest/mig012";
        static string[] Ids => new[] { "faction_khuchad_clan", "faction_tlaxcala", "faction_vestari" };

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Faction generation requires a stopped Editor.");
            Directory.CreateDirectory(EvidenceRoot);
            string legacyRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Import/Legacy"));
            var preflight = new ValidationReport("MIG-012 preflight");
            var index = LegacyContentIndex.Load(legacyRoot, preflight);
            var manifest = MigrationManifest.Load(ManifestPath);
            // Read live assets before any catalog rebuild/write, catching duplicate identities and absent prerequisites.
            var definitions = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>()).ToArray();
            var catalog = ContentCatalog.Build(definitions, preflight);
            var players = catalog.All<FactionDefinition>().Where(f => f.IsPlayerFaction).ToArray();
            if (players.Length != 1 || players[0].IdString != "faction_ark" || !players[0].RelationLocked)
                preflight.Error("ENC-E001", "ContentDatabase", "Factions", "Integrate the existing locked faction_ark first; this job never creates or changes a player faction.");
            foreach (string id in new[] { "ship_fighter", "ship_shuttle" })
                if (!catalog.TryGet<ShipBlueprintDefinition>(new ContentId(id), out _))
                    preflight.Error("MIG-E004", "ContentDatabase", "doctrine", "Missing required blueprint: " + id);
            var files = index.OfType("FactionData");
            if (files.Count != 4 || !Ids.Concat(new[] { "faction_4" }).OrderBy(s => s, StringComparer.Ordinal)
                .SequenceEqual(files.Select(f => f.Name).OrderBy(s => s, StringComparer.Ordinal)))
                preflight.Error("MIG-E002", "Import/Legacy/content/Factions", "", "Expected the exact three factions plus the unconverted duplicate.");
            foreach (var file in files)
            {
                var dto = LegacyJson.Read<LegacyFactionDto>(file, preflight);
                if (dto != null && dto.Id != file.Name)
                    preflight.Error("MIG-E003", file.RelativePath, "id", "Source identity has drifted from the documented real export; reconcile before generating.");
            }
            var inputBytes = files.ToDictionary(f => Path.Combine(legacyRoot, f.RelativePath), f => File.ReadAllBytes(Path.Combine(legacyRoot, f.RelativePath)));
            var before = new Dictionary<string, (byte[] Bytes, string Guid, bool HasHash, ulong Hash)>();
            foreach (string id in Ids)
            {
                string path = PathOf(id); var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (catalog.TryGet<FactionDefinition>(new ContentId(id), out var identified) && AssetDatabase.GetAssetPath(identified) != path)
                    preflight.Error("MIG-E010", path, "id", "This faction identity already exists at another path; reconcile it instead of creating a duplicate.");
                if (asset != null && !(asset is FactionDefinition) || asset == null && File.Exists(path))
                    preflight.Error("MIG-E010", path, "", "Target must be a loadable FactionDefinition.");
                if (asset == null && File.Exists(path + ".meta"))
                    preflight.Error("MIG-E010", path, "", "Orphaned target metadata requires reconciliation before generation.");
                if (asset != null)
                {
                    byte[] bytes = File.ReadAllBytes(path); bool hasHash = manifest.TryGetHash(path, out ulong hash);
                    before[path] = (bytes, AssetDatabase.AssetPathToGUID(path), hasHash, hash);
                }
            }
            var converter = new Converters.FactionConverter();
            var preWriter = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, preflight);
            converter.Preflight(new LegacyMigrationContext(index, LegacyIdMap.Default, preWriter, preflight, false, legacyRoot) { Catalog = catalog });
            File.WriteAllText(EvidenceRoot + "/preflight.txt", preflight.ToText());
            if (preflight.HasErrors) throw new ContentValidationException(preflight);
            string playerPath = AssetDatabase.GetAssetPath(players[0]); byte[] playerBytes = File.ReadAllBytes(playerPath);
            string playerGuid = AssetDatabase.AssetPathToGUID(playerPath);

            var report = new ValidationReport("MIG-012 targeted generation");
            var writer = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, report);
            converter.Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, legacyRoot) { Catalog = catalog });
            // Save only ownership actually acquired by the protected writer, including a successful partial run.
            manifest.Save(ManifestPath); var database = ContentDatabaseBuilder.Rebuild();
            catalog = ContentCatalog.Build(database.Definitions, report);
            if (writer.Outcomes.Count != 3 || writer.Outcomes.Select(o => o.AssetPath).Distinct().Count() != 3 ||
                writer.Outcomes.Any(o => !Ids.Any(id => o.AssetPath == PathOf(id))))
                report.Error("MIG-E073", "ContentDatabase", "Factions", "Expected exactly three targeted outcomes; no other faction may be written.");
            if (report.Entries.Count(e => e.Code == "MIG-I091" && e.AssetPath == "content/Factions/faction_4.json") != 1)
                report.Error("MIG-E073", "Import/Legacy", "faction_4", "Expected one explicit duplicate-skip diagnostic.");
            foreach (var outcome in writer.Outcomes)
            {
                if (outcome.Outcome == WriteOutcome.Failed) continue;
                if (before.TryGetValue(outcome.AssetPath, out var prior) && prior.Guid != AssetDatabase.AssetPathToGUID(outcome.AssetPath))
                    report.Error("MIG-E073", outcome.AssetPath, "", "Generation changed an existing GUID.");
                if (outcome.Outcome == WriteOutcome.SkippedHandEdited)
                {
                    if (!before.TryGetValue(outcome.AssetPath, out prior) || !prior.Bytes.SequenceEqual(File.ReadAllBytes(outcome.AssetPath)))
                        report.Error("MIG-E073", outcome.AssetPath, "", "Protected bytes changed.");
                    bool present = manifest.TryGetHash(outcome.AssetPath, out ulong retained);
                    // Hash zero is allowed: existence must also remain unchanged.
                    if (present != prior.HasHash || present && retained != prior.Hash)
                        report.Error("MIG-E073", outcome.AssetPath, "", "Protected manifest ownership changed.");
                    continue; // Authored values remain intact; never claim they match the legacy export.
                }
                var faction = AssetDatabase.LoadAssetAtPath<FactionDefinition>(outcome.AssetPath);
                if (faction == null) { report.Error("MIG-E010", outcome.AssetPath, "", "Written faction could not be loaded for readback."); continue; }
                var file = files.Single(f => f.Name == faction.IdString); var dto = LegacyJson.Read<LegacyFactionDto>(file, report);
                CheckValues(faction, dto, catalog, report);
                if (!manifest.TryGetHash(outcome.AssetPath, out ulong hash) || hash != StableHash.Hash64(File.ReadAllBytes(outcome.AssetPath)))
                    report.Error("MIG-E073", outcome.AssetPath, "", "Generated bytes lack a matching ownership hash.");
            }
            report.Merge(ContentValidatorRunner.Validate(database));
            VerifyUntouched(inputBytes, playerPath, playerBytes, playerGuid, report);
            File.WriteAllText(EvidenceRoot + "/targeted-report.txt", report.ToText());
            File.WriteAllLines(EvidenceRoot + "/outputs.tsv", writer.Outcomes.Select(o => o.AssetPath + "\t" + o.Outcome + "\t" + AssetDatabase.AssetPathToGUID(o.AssetPath)));
            if (report.HasErrors) throw new ContentValidationException(report);

            var snapshots = writer.Outcomes.ToDictionary(o => o.AssetPath, o => (Bytes: File.ReadAllBytes(o.AssetPath), Guid: AssetDatabase.AssetPathToGUID(o.AssetPath), Outcome: o.Outcome));
            byte[] manifestBytes = File.ReadAllBytes(ManifestPath);
            var again = new ValidationReport("MIG-012 rerun"); var againWriter = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, again);
            converter.Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, againWriter, again, false, legacyRoot) { Catalog = catalog });
            manifest.Save(ManifestPath);
            foreach (var outcome in againWriter.Outcomes)
            {
                var saved = snapshots[outcome.AssetPath];
                var expected = saved.Outcome == WriteOutcome.SkippedHandEdited ? WriteOutcome.SkippedHandEdited : WriteOutcome.Unchanged;
                if (outcome.Outcome != expected || !saved.Bytes.SequenceEqual(File.ReadAllBytes(outcome.AssetPath)) || saved.Guid != AssetDatabase.AssetPathToGUID(outcome.AssetPath))
                    again.Error("MIG-E073", outcome.AssetPath, "", "Rerun changed bytes, GUID or protected/unchanged outcome.");
            }
            if (againWriter.Outcomes.Count != 3 || !manifestBytes.SequenceEqual(File.ReadAllBytes(ManifestPath)))
                again.Error("MIG-E073", ManifestPath, "", "Rerun changed the manifest or omitted an outcome.");
            VerifyUntouched(inputBytes, playerPath, playerBytes, playerGuid, again);
            File.WriteAllText(EvidenceRoot + "/targeted-rerun.txt", again.ToText());
            if (again.HasErrors) throw new ContentValidationException(again);
            Game.Core.GameLog.Info("Migration", "MIG-012: three faction outcomes; duplicate skipped; protected bytes, GUIDs, player faction and manifest verified.");
            EditorApplication.Exit(0);
        }
        static string PathOf(string id) => ContentDatabaseBuilder.ContentRoot + "/Factions/" + id + ".asset";
        static void CheckValues(FactionDefinition faction, LegacyFactionDto dto, ContentCatalog catalog, ValidationReport report)
        {
            string path = AssetDatabase.GetAssetPath(faction);
            var color = new UnityEngine.Color((float)dto.Color[0], (float)dto.Color[1], (float)dto.Color[2], (float)dto.Color[3]);
            if (faction.IsPlayerFaction || faction.DisplayName != dto.DisplayName || faction.Description != dto.Description ||
                faction.ShipNamePrefix != dto.ShipNamePrefix || faction.BasePlayerRelation != dto.BasePlayerRelation || faction.LiveryColor != color ||
                faction.MapColorToken != "--color-faction-" + faction.IdString.Substring(8).Replace('_', '-') || faction.Doctrine.Count != dto.Doctrine.Length)
                report.Error("MIG-E073", path, "", "Generated faction does not match the mapped legacy fields.");
            for (int i = 0; i < Math.Min(faction.Doctrine.Count, dto.Doctrine.Length); i++)
                if (!LegacyIdMap.Default.TryMap(LegacyIdKind.Ship, dto.Doctrine[i].ShipKey, out string ship) ||
                    faction.Doctrine[i].blueprint != catalog.Get<ShipBlueprintDefinition>(new ContentId(ship)) || faction.Doctrine[i].weight != (float)dto.Doctrine[i].Weight)
                    report.Error("MIG-E073", path, "doctrine[" + i + "]", "Doctrine reference/order/weight differs from the mapped export.");
        }
        static void VerifyUntouched(Dictionary<string, byte[]> inputs, string playerPath, byte[] player, string guid, ValidationReport report)
        {
            foreach (var pair in inputs) if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) report.Error("MIG-E073", pair.Key, "", "Read-only export changed.");
            if (!player.SequenceEqual(File.ReadAllBytes(playerPath)) || guid != AssetDatabase.AssetPathToGUID(playerPath))
                report.Error("MIG-E073", playerPath, "", "Existing player faction bytes/GUID changed.");
        }
    }
}
