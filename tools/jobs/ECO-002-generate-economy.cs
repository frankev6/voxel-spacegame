// ECO-002 lead-only generation job; run from the project root after source integration/compilation.
// Partial ECO-009 content: brings forward only canonical resource_scrap, retaining strict ECO-E005.
//
// Copy into the Editor assembly under an unused temporary filename (preserve existing source/metas):
//   test ! -e Assets/Game/Editor/ECO002GenerationJob.cs && test ! -e Assets/Game/Editor/ECO002GenerationJob.cs.meta && cp tools/jobs/ECO-002-generate-economy.cs Assets/Game/Editor/ECO002GenerationJob.cs
//
// With the Editor closed and the lead's Unity channel free, execute the one-off job (docs/08-tools.md §3):
//   mkdir -p Logs/tools
//   . tools/_env.sh
//   "$(unity_editor)" -batchmode -nographics -projectPath "$PWD" -executeMethod Game.Editor.CreateEconomyConfig.Run -logFile Logs/tools/eco002-content.log
//
// After the process exits, inspect its exit status/log and Logs/eco002/generation-readback.txt.
// Remove only the temporary source/meta after preserving the evidence:
//   rm Assets/Game/Editor/ECO002GenerationJob.cs Assets/Game/Editor/ECO002GenerationJob.cs.meta
// The job exits 0 on success, 1 on failure; persisted assets are retained for a safe idempotent retry.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Content.Validators;
using Game.Editor.Migration;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class CreateEconomyConfig
    {
        const string ScrapPath = "Assets/Game/Content/Items/resource_scrap.asset";
        const string ConfigPath = "Assets/Game/Content/Config/config_economy.asset";

        public static void Run()
        {
            ItemDefinition scrap = null;
            EconomyConfigDefinition config = null;
            bool createdScrap = false, createdConfig = false;
            try
            {
                // Read the actual content assets so a stale database can be registered after successful preflight.
                var definitions = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                    .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<ContentDefinition>()).ToList();
                var report = new ValidationReport("ECO-002 generation");
                var catalog = ContentCatalog.Build(definitions, report);
                if (report.HasErrors) throw new InvalidOperationException(report.ToText());
                var fuel = catalog.Get<ItemDefinition>(new ContentId("resource_fuel"));
                var hull = catalog.Get<BlockDefinition>(new ContentId("block_hull"));
                var angled = catalog.Get<BlockDefinition>(new ContentId("block_hull_angled"));
                var cue = catalog.Get<AudioCueDefinition>(new ContentId("cue_ui_success"));
                var sources = AssetDatabase.LoadAssetAtPath<LegacyMigrationSources>(LegacyMigrationSources.AssetPath);
                if (sources == null) throw new InvalidOperationException("Configured migration icon source is missing.");
                var icons = sources.LoadIcons(report);
                if (report.HasErrors || !icons.TryGetValue("spritetest2_transparent_0", out var icon))
                    throw new InvalidOperationException("ECO-009's canonical scrap icon is unavailable. " + report.ToText());
                scrap = Existing<ItemDefinition>(ScrapPath, "resource_scrap");
                config = Existing<EconomyConfigDefinition>(ConfigPath, "config_economy");
                bool needScrap = scrap == null, needConfig = config == null;
                if (needScrap)
                {
                    scrap = ScriptableObject.CreateInstance<ItemDefinition>(); scrap.name = "resource_scrap";
                    using var so = new SerializedObject(scrap);
                    so.FindProperty("id").stringValue = "resource_scrap";
                    so.FindProperty("displayName").stringValue = "Scrap";
                    so.FindProperty("description").stringValue = "";
                    so.FindProperty("category").intValue = (int)ItemCategory.Resource;
                    so.FindProperty("rarity").intValue = (int)ItemRarity.Common;
                    so.FindProperty("basePrice").intValue = 2;
                    so.FindProperty("maxStack").intValue = 999;
                    so.FindProperty("icon").objectReferenceValue = icon;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (scrap.DisplayName != "Scrap" || scrap.Category != ItemCategory.Resource || scrap.Rarity != ItemRarity.Common ||
                    scrap.BasePrice != 2 || scrap.MaxStack != 999 || scrap.Icon != icon)
                    throw new InvalidOperationException("Existing resource_scrap differs from ECO-009's table; preserved for lead review.");
                if (needConfig)
                {
                    config = ScriptableObject.CreateInstance<EconomyConfigDefinition>(); config.name = "config_economy";
                    using var so = new SerializedObject(config);
                    so.FindProperty("id").stringValue = "config_economy";
                    so.FindProperty("scrapItem").objectReferenceValue = scrap;
                    so.FindProperty("salvageCue").objectReferenceValue = cue;
                    var rows = so.FindProperty("startItems"); rows.arraySize = 3;
                    StartRow(rows.GetArrayElementAtIndex(0), fuel, 50);
                    StartRow(rows.GetArrayElementAtIndex(1), hull, 40);
                    StartRow(rows.GetArrayElementAtIndex(2), angled, 20);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (config.ScrapItem != scrap || config.SalvageCue != cue)
                    throw new InvalidOperationException("Existing economy config has other scrap/cue references; preserved for lead review.");

                // Preflight proposed definitions before any asset write, without weakening global cardinality/ownership checks.
                var proposed = new List<ContentDefinition>(definitions);
                if (needScrap) proposed.Add(scrap);
                if (needConfig) proposed.Add(config);
                catalog = ContentCatalog.Build(proposed, report);
                scrap.Validate(report.For(ScrapPath), null);
                config.Validate(report.For(ConfigPath), null);
                new EconomyContentValidator().Validate(catalog, report);
                if (report.HasErrors) throw new InvalidOperationException(report.ToText());
                if (needScrap) { AssetDatabase.CreateAsset(scrap, ScrapPath); createdScrap = true; }
                if (needConfig) { AssetDatabase.CreateAsset(config, ConfigPath); createdConfig = true; }
                if (createdScrap) AssetDatabase.SaveAssetIfDirty(scrap);
                if (createdConfig) AssetDatabase.SaveAssetIfDirty(config);
                var database = ContentDatabaseBuilder.Rebuild();
                if (!database.Definitions.Contains(scrap) || !database.Definitions.Contains(config) ||
                    !File.Exists(ScrapPath) || !File.Exists(ConfigPath) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScrapPath)) ||
                    string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ConfigPath)))
                    throw new InvalidOperationException("Economy assets/catalog readback failed.");
                Directory.CreateDirectory("Logs/eco002");
                File.WriteAllText("Logs/eco002/generation-readback.txt",
                    ScrapPath + ": " + (createdScrap ? "Created" : "Kept") + ", GUID " + AssetDatabase.AssetPathToGUID(ScrapPath) +
                    "\nScrap: Resource/Common, price 2, stack 999, icon " + scrap.Icon.name + " (partial ECO-009 content)\n" +
                    ConfigPath + ": " + (createdConfig ? "Created" : "Kept") + ", GUID " + AssetDatabase.AssetPathToGUID(ConfigPath) +
                    "\n" + report.ToText());
                Debug.Log(report.ToText());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                if (scrap != null && !AssetDatabase.Contains(scrap)) UnityEngine.Object.DestroyImmediate(scrap);
                if (config != null && !AssetDatabase.Contains(config)) UnityEngine.Object.DestroyImmediate(config);
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        static T Existing<T>(string path, string id) where T : ContentDefinition
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null && File.Exists(path)) throw new InvalidOperationException("Existing asset could not be loaded: " + path);
            if (asset == null) return null;
            if (asset.GetType() != typeof(T) || ((T)asset).IdString != id)
                throw new InvalidOperationException("Existing asset type/ID collision, preserved: " + path);
            return (T)asset;
        }
        static void StartRow(SerializedProperty row, ItemDefinition item, int count)
        {
            row.FindPropertyRelative("item").objectReferenceValue = item;
            row.FindPropertyRelative("min").intValue = count;
            row.FindPropertyRelative("max").intValue = count;
            row.FindPropertyRelative("chancePercent").floatValue = 100;
        }
    }
}
