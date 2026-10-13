// Lead-only ENC-002 job; partial ENC-010 prerequisite: the required locked ARK player faction, no encounter fleets/art.
// From project root, copy into an unused temporary Editor source (preserve pre-existing source/metas):
//   test ! -e Assets/Game/Editor/ENC002GenerationJob.cs && test ! -e Assets/Game/Editor/ENC002GenerationJob.cs.meta && cp tools/jobs/ENC-002-generate-content.cs Assets/Game/Editor/ENC002GenerationJob.cs
// With Editor closed and the lead's Unity channel free (docs/08-tools.md §3):
//   mkdir -p Logs/tools
//   . tools/_env.sh
//   "$(unity_editor)" -batchmode -nographics -projectPath "$PWD" -executeMethod Game.Editor.GenerateEncounterContent.Run -logFile Logs/tools/enc002-content.log
// After process exit, retain Logs/enc002/generation-readback.txt and remove only the temporary source/meta:
//   rm Assets/Game/Editor/ENC002GenerationJob.cs Assets/Game/Editor/ENC002GenerationJob.cs.meta
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
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Lead-run SPEC-33 ENC-002 generation with preflight validation and preserved existing assets.</summary>
    public static class GenerateEncounterContent
    {
        const string FactionPath = "Assets/Game/Content/Factions/faction_ark.asset";
        const string ConfigPath = "Assets/Game/Content/Config/config_encounters.asset";
        public static void Run()
        {
            FactionDefinition faction = null; EncounterConfigDefinition config = null;
            try
            {
                var definitions = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>()).ToList();
                faction = Existing<FactionDefinition>(FactionPath, "faction_ark");
                config = Existing<EncounterConfigDefinition>(ConfigPath, "config_encounters");
                bool newFaction = faction == null, newConfig = config == null;
                var start = definitions.OfType<AuthoredStarSystemDefinition>().SingleOrDefault(d => d.IdString == "starsys_ark_start");
                if (newFaction)
                {
                    faction = ScriptableObject.CreateInstance<FactionDefinition>(); faction.name = "faction_ark";
                    using var so = new SerializedObject(faction);
                    so.FindProperty("id").stringValue = "faction_ark";
                    so.FindProperty("displayName").stringValue = "ARK Expedition";
                    so.FindProperty("isPlayerFaction").boolValue = true;
                    so.FindProperty("relationLocked").boolValue = true;
                    if (start != null)
                    { var rows = so.FindProperty("ownedStarSystems"); rows.arraySize = 1; rows.GetArrayElementAtIndex(0).objectReferenceValue = start; }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (!faction.IsPlayerFaction || !faction.RelationLocked)
                    throw new InvalidOperationException("Existing faction_ark has incompatible player/lock fields; preserved for lead review.");
                if (newConfig)
                {
                    config = ScriptableObject.CreateInstance<EncounterConfigDefinition>(); config.name = "config_encounters";
                    using var so = new SerializedObject(config);
                    so.FindProperty("id").stringValue = "config_encounters"; so.ApplyModifiedPropertiesWithoutUndo();
                }
                var proposed = new List<ContentDefinition>(definitions);
                if (newFaction) proposed.Add(faction); if (newConfig) proposed.Add(config);
                var report = new ValidationReport("ENC-002 generation");
                var catalog = ContentCatalog.Build(proposed, report);
                faction.Validate(report.For(FactionPath), null); config.Validate(report.For(ConfigPath), null);
                new EncounterContentValidator().Validate(catalog, report);
                if (report.HasErrors) throw new InvalidOperationException(report.ToText());
                if (newFaction) { AssetDatabase.CreateAsset(faction, FactionPath); AssetDatabase.SaveAssetIfDirty(faction); }
                if (newConfig) { AssetDatabase.CreateAsset(config, ConfigPath); AssetDatabase.SaveAssetIfDirty(config); }
                var database = ContentDatabaseBuilder.Rebuild();
                if (!database.Definitions.Contains(faction) || !database.Definitions.Contains(config) ||
                    !File.Exists(FactionPath) || !File.Exists(ConfigPath) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(FactionPath)) ||
                    string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ConfigPath))) throw new InvalidOperationException("Encounter assets/catalog readback failed.");
                string ownership = faction.OwnedStarSystems != null && faction.OwnedStarSystems.Any(s => s != null && s.IdString == "starsys_ark_start")
                    ? "Canonical ARK ownership present." : "ENC-010 follow-up: assign canonical starsys_ark_start when available; no substitute world created.";
                Directory.CreateDirectory("Logs/enc002");
                File.WriteAllText("Logs/enc002/generation-readback.txt", FactionPath + ": " + (newFaction ? "Created" : "Kept") + ", GUID " +
                    AssetDatabase.AssetPathToGUID(FactionPath) + " (partial ENC-010)\n" + ConfigPath + ": " + (newConfig ? "Created" : "Kept") + ", GUID " +
                    AssetDatabase.AssetPathToGUID(ConfigPath) + "\n" + ownership + "\n" + report.ToText());
                GameLog.Info(LogCategories.Content, ownership + "\n" + report.ToText()); EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                if (faction != null && !AssetDatabase.Contains(faction)) UnityEngine.Object.DestroyImmediate(faction);
                if (config != null && !AssetDatabase.Contains(config)) UnityEngine.Object.DestroyImmediate(config);
                GameLog.Error(LogCategories.Content, e.ToString()); EditorApplication.Exit(1);
            }
        }
        static T Existing<T>(string path, string id) where T : ContentDefinition
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null && File.Exists(path)) throw new InvalidOperationException("Existing asset could not be loaded: " + path);
            if (asset == null) return null;
            if (asset.GetType() != typeof(T) || ((T)asset).IdString != id) throw new InvalidOperationException("Asset type/ID collision, preserved: " + path);
            return (T)asset;
        }
    }
}
