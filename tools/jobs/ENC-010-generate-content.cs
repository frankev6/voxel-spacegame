// ENC-010 lead-only recipe; unexecuted and outside Assets.
// Integrate ENC-010 and GAL-012's REAL starsys_ark_start, starsys_whisper and starsys_drift (danger 2).
// Copy to Assets/Game/Editor/EncounterContentJob.cs; invoke Game.Editor.Migration.EncounterContentJob.Run
// in a stopped Editor (RunCli for batch mode), then remove the temporary script/meta.
// This creates no ships, art, AI profiles or star-system substitutes. Force=false.
// Commit the two new factions, ten encounters, ARK/Vestari ownership updates, catalog and owned manifest
// hashes; evidence is Logs/playtest/enc010/. Validate only ENC-owned definitions with strict ENC validators.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration.Encounters;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Game.Editor.Migration
{
    public static class EncounterContentJob
    {
        const string ManifestPath = "Import/Migration/manifest.json";
        const string Evidence = "Logs/playtest/enc010";
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Encounter content generation requires a stopped Editor.");
            Directory.CreateDirectory(Evidence);
            string legacyRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Import/Legacy"));
            var preflight = new ValidationReport("ENC-010 preflight");
            var index = LegacyContentIndex.Load(legacyRoot, preflight);
            var handoff = LegacyHandOff.Build(index, LegacyIdMap.Default, preflight);
            var manifest = MigrationManifest.Load(ManifestPath);
            ContentCatalog Live(ValidationReport report) => ContentCatalog.Build(AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>()).ToArray(), report);
            var catalog = Live(preflight);
            LegacyMigrationContext Context(LegacyAssetWriter writer, ValidationReport report, ContentCatalog content) =>
                new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, legacyRoot) { Catalog = content, HandOff = handoff };
            EncounterContentAuthoring.PreflightGeneration(Context(new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, preflight), preflight, catalog));
            var targetIds = EncounterContentTables.Rows.Select(r => r.Id).Concat(new[] { "faction_raiders", "faction_xeno" }).ToArray();
            foreach (string id in targetIds)
            {
                string path = PathOf(id); var asset = AssetDatabase.LoadMainAssetAtPath(path);
                Type type = id.StartsWith("faction_", StringComparison.Ordinal) ? typeof(FactionDefinition) : typeof(EncounterDefinition);
                if (catalog.TryGet<ContentDefinition>(new ContentId(id), out var identified) && AssetDatabase.GetAssetPath(identified) != path)
                    preflight.Error("MIG-E010", path, "id", "Canonical identity exists elsewhere; reconcile before creating a duplicate.");
                if (asset != null && asset.GetType() != type || asset == null && (File.Exists(path) || File.Exists(path + ".meta")))
                    preflight.Error("MIG-E010", path, "", "Wrong target type or orphaned asset/metadata.");
                if (asset is ContentDefinition definition && definition.IdString != id)
                    preflight.Error("MIG-E010", path, "id", "Target ID does not match the canonical path.");
            }
            File.WriteAllText(Evidence + "/preflight.txt", preflight.ToText());
            if (preflight.HasErrors) throw new ContentValidationException(preflight); // No content write before all real prerequisites exist.
            var touched = new HashSet<string>(targetIds.Select(PathOf).Concat(new[] { PathOf("faction_ark"), PathOf("faction_vestari") }), StringComparer.Ordinal);
            var allBytes = Directory.GetFiles(ContentDatabaseBuilder.ContentRoot, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                .Where(p => p != ContentDatabaseBuilder.DatabasePath).ToDictionary(p => p, File.ReadAllBytes);
            var sourceBytes = Directory.GetFiles(legacyRoot, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            var before = touched.Where(File.Exists).ToDictionary(p => p, p =>
            {
                bool owned = manifest.TryGetHash(p, out ulong hash);
                return (Bytes: File.ReadAllBytes(p), Guid: AssetDatabase.AssetPathToGUID(p), HasHash: owned, Hash: hash);
            });
            var ownershipBefore = new[] { "faction_ark", "faction_vestari" }.ToDictionary(id => id, id => OwnershipOtherFields(catalog.Get<FactionDefinition>(new ContentId(id))));
            var report = new ValidationReport("ENC-010 generation");
            var writer = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, report);
            EncounterContentAuthoring.AuthorFactions(Context(writer, report, catalog));
            catalog = Live(report);
            if (!report.HasErrors)
            {
                new FleetEncounterConverter().Convert(Context(writer, report, catalog));
                EncounterContentAuthoring.AuthorRemainingEncounters(Context(writer, report, catalog));
            }
            manifest.Save(ManifestPath); ContentDatabaseBuilder.Rebuild(); catalog = Live(report);
            EncounterContentAuthoring.ValidateScoped(catalog, report);
            VerifyCanonical(catalog, report);
            if (writer.Outcomes.Any(o => o.Outcome == WriteOutcome.Failed || !touched.Contains(o.AssetPath)))
                report.Error("MIG-E099", "ENC-010", "outputs", "Failed or unexpected content output.");
            if (writer.Outcomes.Count(o => o.AssetPath.Contains("/Encounters/")) != 10)
                report.Error("MIG-E099", "ENC-010", "outputs", "Expected exactly ten encounter outcomes.");
            foreach (var outcome in writer.Outcomes)
                if (before.TryGetValue(outcome.AssetPath, out var prior))
                {
                    if (prior.Guid != AssetDatabase.AssetPathToGUID(outcome.AssetPath)) report.Error("MIG-E099", outcome.AssetPath, "", "Existing GUID changed.");
                    if (outcome.Outcome == WriteOutcome.SkippedHandEdited)
                    {
                        bool owned = manifest.TryGetHash(outcome.AssetPath, out ulong hash);
                        if (!prior.Bytes.SequenceEqual(File.ReadAllBytes(outcome.AssetPath)) || owned != prior.HasHash || owned && hash != prior.Hash)
                            report.Error("MIG-E099", outcome.AssetPath, "", "Protected bytes or ownership changed.");
                    }
                }
            foreach (var prior in ownershipBefore)
                if (!JToken.DeepEquals(prior.Value, OwnershipOtherFields(catalog.Get<FactionDefinition>(new ContentId(prior.Key)))))
                    report.Error("MIG-E099", prior.Key, "", "Ownership update changed unrelated faction fields.");
            string arkPath = PathOf("faction_ark"); var priorArk = before[arkPath];
            if (manifest.TryGetHash(arkPath, out _) != priorArk.HasHash)
                report.Error("MIG-E099", arkPath, "", "Unexpected player ownership adoption.");
            VerifyUntouched(sourceBytes, allBytes, touched, report);
            File.WriteAllText(Evidence + "/generation.txt", report.ToText());
            File.WriteAllLines(Evidence + "/outputs.tsv", writer.Outcomes.Select(o => o.AssetPath + "\t" + o.Outcome + "\t" + AssetDatabase.AssetPathToGUID(o.AssetPath)));
            if (report.HasErrors) throw new ContentValidationException(report);
            byte[] firstManifest = File.ReadAllBytes(ManifestPath);
            var firstBytes = touched.ToDictionary(p => p, File.ReadAllBytes);
            var rerun = new ValidationReport("ENC-010 rerun"); var again = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, rerun);
            catalog = Live(rerun); EncounterContentAuthoring.AuthorFactions(Context(again, rerun, catalog)); catalog = Live(rerun);
            new FleetEncounterConverter().Convert(Context(again, rerun, catalog)); EncounterContentAuthoring.AuthorRemainingEncounters(Context(again, rerun, catalog));
            manifest.Save(ManifestPath); catalog = Live(rerun); EncounterContentAuthoring.ValidateScoped(catalog, rerun); VerifyCanonical(catalog, rerun);
            if (again.Outcomes.Any(o => o.Outcome != WriteOutcome.Unchanged && o.Outcome != WriteOutcome.SkippedHandEdited))
                rerun.Error("MIG-E099", "ENC-010", "rerun", "Rerun unexpectedly modified content.");
            foreach (var pair in firstBytes) if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) rerun.Error("MIG-E099", pair.Key, "", "Rerun changed output bytes.");
            if (!firstManifest.SequenceEqual(File.ReadAllBytes(ManifestPath))) rerun.Error("MIG-E099", ManifestPath, "", "Rerun changed ownership manifest.");
            VerifyUntouched(sourceBytes, allBytes, touched, rerun);
            File.WriteAllText(Evidence + "/rerun.txt", rerun.ToText());
            if (rerun.HasErrors) throw new ContentValidationException(rerun);
            GameLog.Info("Encounters", "ENC-010: six canonical factions and ten encounters verified with zero scoped ENC errors; ownership references real GAL-012 systems.");
        }
        public static void RunCli()
        { try { Run(); EditorApplication.Exit(0); } catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); } }
        static string PathOf(string id) => ContentDatabaseBuilder.ContentRoot + (id.StartsWith("faction_", StringComparison.Ordinal) ? "/Factions/" : "/Encounters/") + id + ".asset";
        static JObject OwnershipOtherFields(FactionDefinition definition)
        { var value = JObject.Parse(EditorJsonUtility.ToJson(definition)); value.Remove("ownedStarSystems"); return value; }
        static void VerifyUntouched(Dictionary<string, byte[]> sources, Dictionary<string, byte[]> content, HashSet<string> touched, ValidationReport report)
        {
            foreach (var pair in sources) if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) report.Error("MIG-E099", pair.Key, "", "Read-only export changed.");
            foreach (var pair in content)
                if (!touched.Contains(pair.Key) && !pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) report.Error("MIG-E099", pair.Key, "", "Unrelated content or metadata changed.");
        }
        static void VerifyCanonical(ContentCatalog catalog, ValidationReport report)
        {
            foreach (var row in EncounterContentTables.Rows)
            {
                if (!catalog.TryGet<EncounterDefinition>(new ContentId(row.Id), out var encounter) || encounter.Faction == null || encounter.Faction.IdString != row.Faction ||
                    encounter.Kind != row.Kind || encounter.UseFactionDoctrine || encounter.Ships.Count != row.Ships.Count || encounter.DirectorWeight != row.Weight ||
                    encounter.MinDanger != row.DangerMin || encounter.MaxDanger != row.DangerMax || encounter.SpawnDistanceMinM != row.DistanceMin || encounter.SpawnDistanceMaxM != row.DistanceMax ||
                    encounter.DisplayName != row.Name || encounter.PersistUntilDefeated != (row.Kind == EncounterKind.Story) ||
                    encounter.Hailable != (row.Kind == EncounterKind.Merchant) || encounter.UniquePerVisit != (row.Kind == EncounterKind.Merchant) || encounter.Condition.IsStale || encounter.Condition.HasErrors)
                { report.Error("ENC-E011", row.Id, "", "Canonical authored encounter differs from ENC-010 (including protected authored targets requiring reconciliation)."); continue; }
                for (int i = 0; i < row.Ships.Count; i++)
                {
                    var actual = encounter.Ships[i]; var desired = row.Ships[i];
                    if (actual.blueprint == null || actual.blueprint.IdString != desired.Id || actual.countMin != desired.Min || actual.countMax != desired.Max ||
                        desired.Ai != null && (actual.aiProfile == null || actual.aiProfile.IdString != desired.Ai))
                        report.Error("ENC-E012", row.Id, "ships[" + i + "]", "Canonical ship/reference/count/profile differs.");
                }
            }
            foreach (string id in new[] { "faction_raiders", "faction_xeno" })
                if (!catalog.TryGet<FactionDefinition>(new ContentId(id), out var faction) || faction.IsPlayerFaction || !faction.RelationLocked || faction.BasePlayerRelation != -100 || id == "faction_xeno" && faction.MapColorToken != "--color-faction-xeno")
                    report.Error("ENC-E001", id, "", "Expected locked hostile non-player faction.");
            if (!catalog.TryGet<FactionDefinition>(new ContentId("faction_raiders"), out var raiders)) return;
            if (raiders.DefaultAiProfile == null || raiders.DefaultAiProfile.IdString != "ai_default" || raiders.Doctrine.Count != 2 ||
                raiders.Doctrine[0].blueprint == null || raiders.Doctrine[0].blueprint.IdString != "ship_fighter" || raiders.Doctrine[0].weight != 1 ||
                raiders.Doctrine[1].blueprint == null || raiders.Doctrine[1].blueprint.IdString != "ship_striker" || raiders.Doctrine[1].weight != .6f ||
                !catalog.All<FactionDefinition>().Where(f => !f.IsPlayerFaction && f != raiders).All(f => raiders.Stances.Any(s => s.other == f && s.stance == FactionStanceValue.Hostile)))
                report.Error("ENC-E004", "faction_raiders", "", "Canonical doctrine/AI/hostility differs from ENC-010.");
            foreach (var pair in new[] { (Faction: "faction_ark", System: "starsys_ark_start"), (Faction: "faction_vestari", System: "starsys_whisper") })
                if (!catalog.Get<FactionDefinition>(new ContentId(pair.Faction)).OwnedStarSystems.Any(s => s != null && s.IdString == pair.System))
                    report.Error("ENC-E005", pair.Faction, "ownedStarSystems", "Required real system ownership missing.");
        }
    }
}
