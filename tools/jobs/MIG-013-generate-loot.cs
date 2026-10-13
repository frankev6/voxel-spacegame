// MIG-013 lead-only targeted generation recipe. Outside Assets: not compiled or executed by the worker.
// Integrate ECO-002 and MIG-013 first; copy to Assets/Game/Editor/LootMigrationJob.cs in the validation checkout.
// Run Game.Editor.Migration.LootMigrationJob.Run using the serialized Unity job workflow; remove the temporary file/meta afterward.
// Commit the two generated Loot assets/metas, ContentDatabase and owned manifest hashes with central closeout.
// A later full LegacyMigrationRunner pass supplies the complete Import/Migration/report.md; this job preserves that report.
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor.Migration
{
    public static class LootMigrationJob
    {
        public static void Run()
        {
            const string manifestPath = "Import/Migration/manifest.json";
            const string evidenceRoot = "Logs/playtest/mig013";
            var report = new ValidationReport("MIG-013 targeted generation");
            string legacyRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Import/Legacy"));
            var index = LegacyContentIndex.Load(legacyRoot, report);
            var manifest = MigrationManifest.Load(manifestPath);
            var database = ContentDatabaseBuilder.Rebuild();
            var catalog = ContentCatalog.Build(database.Definitions, report);
            if (report.HasErrors) throw new ContentValidationException(report);
            var converter = new Converters.LootTableConverter();
            var writer = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, report);
            converter.Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, legacyRoot) { Catalog = catalog });
            AssetDatabase.SaveAssets();
            // Retain ownership evidence for successful partial writes before surfacing any input/prerequisite failures.
            manifest.Save(manifestPath); database = ContentDatabaseBuilder.Rebuild();
            catalog = ContentCatalog.Build(database.Definitions, report);
            Directory.CreateDirectory(evidenceRoot); File.WriteAllText(evidenceRoot + "/targeted-report.txt", report.ToText());
            if (report.HasErrors) throw new ContentValidationException(report);

            string[] ids = { "loot_base", "loot_creative" }; int[] counts = { 15, 36 };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!catalog.TryGet<LootTableDefinition>(new ContentId(ids[i]), out var table))
                { report.Error("MIG-E004", "ContentDatabase", ids[i], "Generated loot table is unavailable."); continue; }
                string path = AssetDatabase.GetAssetPath(table);
                if (table.DefaultPicks != 0 || table.Entries.Count != counts[i])
                    report.Error("MIG-E072", path, "entries", "Expected ordered real-export rows: " + counts[i] + ", defaultPicks 0. Any authored table remains protected.");
                table.Validate(report.For(path), new ContentValidationContext(path, true, _ => true, catalog));
            }
            if (report.Entries.Count(e => e.Code == "MIG-W070") != 4 || report.Entries.Count(e => e.Code == "MIG-W071") != 1)
                report.Error("MIG-E072", "Import/Legacy", "lootItems", "Expected four unsupported ship rows and one null creative row; recheck source provenance.");
            File.WriteAllText(evidenceRoot + "/targeted-report.txt", report.ToText());
            if (report.HasErrors) throw new ContentValidationException(report);

            var paths = writer.Outcomes.Select(o => o.AssetPath).ToArray();
            var snapshots = paths.ToDictionary(p => p, p => (Bytes: File.ReadAllBytes(p), Guid: AssetDatabase.AssetPathToGUID(p)));
            byte[] ownedManifest = File.ReadAllBytes(manifestPath);
            var again = new ValidationReport("MIG-013 unchanged rerun");
            var againWriter = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, false, again);
            converter.Convert(new LegacyMigrationContext(index, LegacyIdMap.Default, againWriter, again, false, legacyRoot) { Catalog = catalog });
            AssetDatabase.SaveAssets(); manifest.Save(manifestPath);
            foreach (var pair in snapshots)
                if (!pair.Value.Bytes.SequenceEqual(File.ReadAllBytes(pair.Key)) || pair.Value.Guid != AssetDatabase.AssetPathToGUID(pair.Key))
                    again.Error("MIG-E072", pair.Key, "", "Targeted rerun changed asset bytes or GUID.");
            if (!ownedManifest.SequenceEqual(File.ReadAllBytes(manifestPath))) again.Error("MIG-E072", manifestPath, "", "Targeted rerun changed the owned manifest.");
            if (againWriter.Outcomes.Count != 2 || againWriter.Outcomes.Any(o => o.Outcome != WriteOutcome.Unchanged))
                again.Error("MIG-E072", "ContentDatabase", "Loot", "Both generated tables must report Unchanged; inspect protected authored content if retained.");
            File.WriteAllText(evidenceRoot + "/targeted-rerun.txt", again.ToText());
            if (again.HasErrors) throw new ContentValidationException(again);
            Game.Core.GameLog.Info("Migration", "MIG-013: 15 base / 36 creative rows; four ship drops / one null drop; unchanged bytes, GUIDs and manifest on rerun.");
            EditorApplication.Exit(0);
        }
    }
}
