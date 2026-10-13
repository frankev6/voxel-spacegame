// MIG-010 lead-only targeted generation recipe. This file is outside Assets and is not compiled into the project.
// Integrate ABL-002 and MIG-010 first, then copy this file to Assets/Game/Editor/AbilityMigrationJob.cs in the validation checkout.
// Invoke Game.Editor.Migration.AbilityMigrationJob.Run through the normal Unity job workflow; remove the temporary file/meta afterward.
// Commit the generated two ability assets, four block grant changes and migration manifest with the central closeout.
namespace Game.Editor.Migration
{
    public static class AbilityMigrationJob
    {
        public static void Run()
        {
            var report = new Game.Data.Validation.ValidationReport("MIG-010 targeted generation");
            string legacyRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Import/Legacy"));
            var index = LegacyContentIndex.Load(legacyRoot, report);
            if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
            const string manifestPath = "Import/Migration/manifest.json";
            var manifest = MigrationManifest.Load(manifestPath);
            var database = Game.Editor.Content.ContentDatabaseBuilder.Rebuild();
            var catalog = Game.Data.Services.ContentCatalog.Build(database.Definitions, report);
            if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
            var writer = new LegacyAssetWriter(Game.Editor.Content.ContentDatabaseBuilder.ContentRoot, manifest, false, report);
            var context = new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, legacyRoot) { Catalog = catalog };
            new Converters.AbilityConverter().Convert(context);
            UnityEditor.AssetDatabase.SaveAssets();
            // Preserve provenance of successful partial writes before surfacing any failed grant/source read.
            manifest.Save(manifestPath);
            database = Game.Editor.Content.ContentDatabaseBuilder.Rebuild();
            var finalCatalog = Game.Data.Services.ContentCatalog.Build(database.Definitions, report);
            foreach (string id in new[] { "ability_speed_boost", "ability_shield_toggle" })
            {
                if (!finalCatalog.TryGet<Game.Data.Definitions.AbilityDefinition>(new Game.Core.ContentId(id), out var definition))
                    report.Error("MIG-E004", "ContentDatabase", "Abilities", "Generated ability unavailable: " + id);
                else definition.Validate(report.For(UnityEditor.AssetDatabase.GetAssetPath(definition)), null);
            }
            foreach (string blockId in new[] { "block_thruster_small", "block_thruster_medium", "block_thruster_large", "block_shield_small" })
            {
                string abilityId = blockId == "block_shield_small" ? "ability_shield_toggle" : "ability_speed_boost";
                if (!finalCatalog.TryGet<Game.Data.Definitions.BlockDefinition>(new Game.Core.ContentId(blockId), out var block) ||
                    block.GrantedAbilities.Count != 1 || block.GrantedAbilities[0] == null || block.GrantedAbilities[0].IdString != abilityId)
                    report.Error("MIG-E004", "ContentDatabase", blockId + ".grantedAbilities",
                        "Expected grant was not applied; any retained authored array remains protected: " + abilityId);
            }
            if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
            Game.Core.GameLog.Info("Migration", report.ToText());
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
