using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Migration
{
    /// <summary>SPEC-72 §5.1: deterministic converter discovery; independent failures do not stop later converters.</summary>
    public static class LegacyMigrationRunner
    {
        const string Menu = "Game/Legacy Migration/Run All";
        [MenuItem(Menu)] public static void RunAllMenu() => RunAll(false);
        [MenuItem(Menu + " (Force)")] public static void RunAllForceMenu()
        {
            if (EditorUtility.DisplayDialog("Force legacy migration?", "Overwrite hand-edited target assets with the legacy export?", "Overwrite", "Cancel")) RunAll(true);
        }
        [MenuItem(Menu, true)] static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem(Menu + " (Force)", true)] static bool CanForce() => CanRun();
        public static ValidationReport RunAll(bool force)
        {
            var report = new ValidationReport("MIG");
            string legacyRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Import/Legacy"));
            if (!Directory.Exists(legacyRoot)) { report.Error("MIG-E001", legacyRoot, "", "Legacy folder not found."); report.LogTo("Migration"); return report; }
            var index = LegacyContentIndex.Load(legacyRoot, report);
            const string manifestPath = "Import/Migration/manifest.json";
            MigrationManifest manifest;
            try { manifest = MigrationManifest.Load(manifestPath); }
            catch (Exception e) { report.Error("MIG-E002", manifestPath, "", "Invalid manifest: " + e.Message); report.LogTo("Migration"); return report; }
            var writer = new LegacyAssetWriter(ContentDatabaseBuilder.ContentRoot, manifest, force, report);
            var context = new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, force, legacyRoot);
            var converters = new List<ILegacyConverter>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ILegacyConverter>().Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) != null).OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                try { converters.Add((ILegacyConverter)Activator.CreateInstance(type)); }
                catch (Exception e) { report.Error("MIG-E099", type.FullName, "", "Converter construction failed: " + e.GetBaseException().Message); }
            }
            converters = converters.OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
            var database = ContentDatabaseBuilder.Rebuild();
            foreach (var converter in converters)
            {
                context.Catalog = ContentCatalog.Build(database.Definitions, new ValidationReport("Migration scratch"));
                try
                {
                    if (converter.Order >= 500 && context.HandOff == null) context.HandOff = LegacyHandOff.Build(index, context.IdMap, report);
                    converter.Convert(context);
                }
                catch (Exception e) { report.Error("MIG-E099", converter.Name, "", "Converter threw " + e.GetType().Name + ": " + e.Message); }
                AssetDatabase.SaveAssets();
                database = ContentDatabaseBuilder.Rebuild();
            }
            // The writer updates hashes only for owned/forced writes, never for a protected asset's filled link.
            manifest.Save(manifestPath);
            ContentValidatorRunner.Run(out var contentReport); report.Merge(contentReport);
            MigrationReportWriter.Write("Import/Migration/report.md", converters, writer, report, context.Ships);
            Debug.Log($"Migration: {converters.Count} converters, {writer.Outcomes.Count} outcomes.");
            report.LogTo("Migration"); return report;
        }
        public static void RunCli()
        {
            try { var report = RunAll(false); Debug.Log(report.ToText()); EditorApplication.Exit(report.HasErrors ? 1 : 0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
