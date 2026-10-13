using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration;
using Game.Editor.Migration.Encounters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>ENC-010: real imported blueprints/profiles plus disposable test-only ownership data, outside production content.</summary>
    internal sealed class EncounterContentTestFixture : IDisposable
    {
        internal const string Root = "Assets/Game/Tests/_EncounterContentMigrationTest";
        internal readonly MigrationManifest Manifest = new MigrationManifest();
        internal readonly LegacyContentIndex Index;
        internal readonly LegacyHandOff HandOff;
        readonly List<ContentDefinition> dependencies;
        bool ownsRoot;
        internal EncounterContentTestFixture()
        {
            Assert.IsFalse(Directory.Exists(Root) || AssetDatabase.IsValidFolder(Root), "Preserve existing files."); ownsRoot = true;
            try
            {
                var report = new ValidationReport("ENC-010 fixture");
                Index = LegacyContentIndex.Load(Path.GetFullPath("Import/Legacy"), report);
                HandOff = LegacyHandOff.Build(Index, LegacyIdMap.Default, report); Assert.AreEqual(0, report.ErrorCount, report.ToText());
                dependencies = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>())
                    .Where(d => !(d is FactionDefinition) && !(d is EncounterDefinition) && !(d is AuthoredStarSystemDefinition)).ToList();
                Directory.CreateDirectory(Root + "/Factions"); Directory.CreateDirectory(Root + "/Galaxy"); AssetDatabase.Refresh();
                foreach (string id in new[] { "faction_ark", "faction_vestari", "faction_tlaxcala", "faction_khuchad_clan" })
                {
                    var original = AssetDatabase.LoadAssetAtPath<FactionDefinition>(ContentDatabaseBuilder.ContentRoot + "/Factions/" + id + ".asset");
                    Assert.IsNotNull(original, "Use the existing MIG-012/ENC-002 factions.");
                    var copy = Object.Instantiate(original); copy.name = original.name;
                    using (var so = new SerializedObject(copy))
                    { so.FindProperty("ownedStarSystems").arraySize = 0; so.FindProperty("stances").arraySize = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
                    string path = Root + "/Factions/" + id + ".asset"; AssetDatabase.CreateAsset(copy, path);
                    if (id != "faction_ark") Manifest.SetHash(path, StableHash.Hash64(File.ReadAllBytes(path)));
                }
                // Only this disposable test root gets minimal canonical-ID ownership fixtures.
                // The generation job requires GAL-012's real definitions and never creates replacements.
                foreach (string id in new[] { "starsys_ark_start", "starsys_whisper", "starsys_drift" })
                {
                    var system = ScriptableObject.CreateInstance<AuthoredStarSystemDefinition>(); system.name = id;
                    using (var so = new SerializedObject(system))
                    { so.FindProperty("id").stringValue = id; so.FindProperty("danger").intValue = id == "starsys_drift" ? 2 : 0; so.ApplyModifiedPropertiesWithoutUndo(); }
                    AssetDatabase.CreateAsset(system, Root + "/Galaxy/" + id + ".asset");
                }
            }
            catch { Dispose(); throw; }
        }
        internal ContentCatalog Catalog()
        {
            var definitions = dependencies.Concat(AssetDatabase.FindAssets("t:ContentDefinition", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>())).ToArray();
            var report = new ValidationReport("ENC-010 fixture catalog"); var catalog = ContentCatalog.Build(definitions, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText()); return catalog;
        }
        internal LegacyMigrationContext Context(bool force = false)
        {
            var report = new ValidationReport("ENC-010 fixture conversion");
            return new LegacyMigrationContext(Index, LegacyIdMap.Default, new LegacyAssetWriter(Root, Manifest, force, report), report, force, Path.GetFullPath("Import/Legacy"))
            { Catalog = Catalog(), HandOff = HandOff };
        }
        internal void Factions()
        {
            var context = Context(); Assert.IsTrue(EncounterContentAuthoring.PreflightGeneration(context), context.Report.ToText());
            EncounterContentAuthoring.AuthorFactions(context); Assert.AreEqual(0, context.Report.ErrorCount, context.Report.ToText());
        }
        internal LegacyMigrationContext Fleets(bool force = false)
        {
            var context = Context(force); new FleetEncounterConverter().Convert(context);
            Assert.AreEqual(0, context.Report.ErrorCount, context.Report.ToText()); return context;
        }
        internal static EncounterDefinition Encounter(string id) => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(Root + "/Encounters/" + id + ".asset");
        public void Dispose() { if (ownsRoot) AssetDatabase.DeleteAsset(Root); ownsRoot = false; }
    }
}
