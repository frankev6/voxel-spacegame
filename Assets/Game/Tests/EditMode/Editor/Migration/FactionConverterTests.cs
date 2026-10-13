using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using Game.Editor.Migration.Dto;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-012: real-export values and migration safety without writing canonical content.</summary>
    public sealed class FactionConverterTests
    {
        const string Root = "Assets/Game/Content/_FactionMigrationTest";
        MigrationManifest manifest;
        ContentCatalog catalog;
        readonly List<ContentDefinition> prerequisites = new List<ContentDefinition>();
        Dictionary<string, byte[]> inputs;
        bool ownsRoot;

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root) || Directory.Exists(Root) || File.Exists(Root + ".meta"), "Never remove existing user content.");
            ownsRoot = true; manifest = new MigrationManifest(); prerequisites.Clear();
            inputs = Directory.GetFiles("Import/Legacy/content/Factions", "*.json").ToDictionary(p => p, File.ReadAllBytes);
            foreach (string id in new[] { "ship_fighter", "ship_shuttle" })
            {
                var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>("Assets/Game/Content/Ships/" + id + ".asset");
                Assert.NotNull(blueprint); prerequisites.Add(blueprint);
            }
            prerequisites.Add(AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"));
            prerequisites.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"));
            var context = Context();
            var player = context.Writer.CreateOrUpdate<FactionDefinition>("Factions", "faction_ark", so =>
            {
                so.FindProperty("displayName").stringValue = "ARK Expedition";
                so.FindProperty("isPlayerFaction").boolValue = true; so.FindProperty("relationLocked").boolValue = true;
            }, out _);
            prerequisites.Add(player); RefreshCatalog();
        }
        [TearDown] public void Clean()
        { if (ownsRoot) AssetDatabase.DeleteAsset(Root); ownsRoot = false; prerequisites.Clear(); }

        [Test]
        public void RealExport_ThreeFactions()
        {
            var converter = new FactionConverter(); Assert.AreEqual("Factions", converter.Name); Assert.AreEqual(400, converter.Order);
            byte[] player = File.ReadAllBytes(PathOf("faction_ark")); string playerGuid = AssetDatabase.AssetPathToGUID(PathOf("faction_ark"));
            var context = Context(); Assert.IsTrue(converter.Preflight(context), context.Report.ToText());
            context = Context(); converter.Convert(context);
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText()); Assert.AreEqual(3, context.Writer.Outcomes.Count);
            Assert.IsTrue(context.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Created));
            Assert.AreEqual(4, AssetDatabase.FindAssets("t:FactionDefinition", new[] { Root }).Length, "Three imports plus the untouched player fixture.");
            string[] ids = { "faction_khuchad_clan", "faction_tlaxcala", "faction_vestari" };
            string[] names = { "Khuchad Clan", "Tlaxcala", "Vestari" }, prefixes = { "KC", "TXS", "VSS" };
            int[] relations = { 0, -20, 0 };
            for (int i = 0; i < ids.Length; i++)
            {
                var faction = Faction(ids[i]); Assert.NotNull(faction); Assert.AreEqual(ids[i], faction.IdString);
                Assert.AreEqual(names[i], faction.DisplayName); Assert.AreEqual(prefixes[i], faction.ShipNamePrefix);
                Assert.AreEqual(relations[i], faction.BasePlayerRelation); Assert.IsFalse(faction.IsPlayerFaction); Assert.IsFalse(faction.RelationLocked);
                Assert.AreEqual("--color-faction-" + ids[i].Substring(8).Replace('_', '-'), faction.MapColorToken);
                var file = context.Index.OfType("FactionData").Single(f => f.Name == ids[i]);
                var dto = LegacyJson.Read<LegacyFactionDto>(file, context.Report);
                Assert.AreEqual(dto.Description, faction.Description);
                Assert.AreEqual((float)dto.Color[0], faction.LiveryColor.r); Assert.AreEqual((float)dto.Color[1], faction.LiveryColor.g);
                Assert.AreEqual((float)dto.Color[2], faction.LiveryColor.b); Assert.AreEqual((float)dto.Color[3], faction.LiveryColor.a);
                Assert.AreEqual(i == 0 ? 2 : 0, faction.Doctrine.Count);
                foreach (var entry in faction.Doctrine) Assert.IsNull(entry.aiProfile);
            }
            var doctrine = Faction(ids[0]).Doctrine;
            Assert.AreSame(catalog.Get<ShipBlueprintDefinition>(new ContentId("ship_fighter")), doctrine[0].blueprint); Assert.AreEqual(.5f, doctrine[0].weight);
            Assert.AreSame(catalog.Get<ShipBlueprintDefinition>(new ContentId("ship_shuttle")), doctrine[1].blueprint); Assert.AreEqual(1f, doctrine[1].weight);
            var duplicate = context.Report.Entries.Single(); Assert.AreEqual("MIG-I091", duplicate.Code);
            Assert.AreEqual("content/Factions/faction_4.json", duplicate.AssetPath); Assert.AreEqual("id", duplicate.Field);
            Assert.IsTrue(duplicate.Message.Contains("faction_4")); Assert.IsNull(Faction("faction_4"));
            var snapshots = context.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => (Bytes: File.ReadAllBytes(o.AssetPath), Guid: AssetDatabase.AssetPathToGUID(o.AssetPath)));
            foreach (var pair in snapshots)
            {
                Assert.IsTrue(manifest.TryGetHash(pair.Key, out ulong hash)); Assert.AreEqual(StableHash.Hash64(pair.Value.Bytes), hash);
                AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate);
            }
            RefreshCatalog(); var again = Context(); converter.Convert(again);
            Assert.IsFalse(again.Report.HasErrors, again.Report.ToText()); Assert.AreEqual(3, again.Writer.Outcomes.Count);
            Assert.IsTrue(again.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged)); Assert.AreEqual(context.Report.ToText(), again.Report.ToText());
            foreach (var pair in snapshots)
            { CollectionAssert.AreEqual(pair.Value.Bytes, File.ReadAllBytes(pair.Key)); Assert.AreEqual(pair.Value.Guid, AssetDatabase.AssetPathToGUID(pair.Key)); }

            string path = PathOf(ids[1]); Assert.IsTrue(manifest.TryGetHash(path, out ulong ownedHash));
            using (var so = new SerializedObject(Faction(ids[1])))
            { so.FindProperty("shipNamePrefix").stringValue = "TUNED"; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(so.targetObject); }
            byte[] edited = File.ReadAllBytes(path); var kept = Context(); converter.Convert(kept);
            Assert.IsFalse(kept.Report.HasErrors, kept.Report.ToText()); Assert.AreEqual(1, kept.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.SkippedHandEdited));
            CollectionAssert.AreEqual(edited, File.ReadAllBytes(path)); Assert.IsTrue(manifest.TryGetHash(path, out ulong retained)); Assert.AreEqual(ownedHash, retained);
            var forced = Context(force: true); converter.Convert(forced); Assert.IsFalse(forced.Report.HasErrors, forced.Report.ToText());
            CollectionAssert.AreEqual(snapshots[path].Bytes, File.ReadAllBytes(path)); Assert.AreEqual(snapshots[path].Guid, AssetDatabase.AssetPathToGUID(path));
            CollectionAssert.AreEqual(player, File.ReadAllBytes(PathOf("faction_ark"))); Assert.AreEqual(playerGuid, AssetDatabase.AssetPathToGUID(PathOf("faction_ark")));
            AssertInputsUnchanged();
        }

        [Test]
        public void InvalidInput_PreservesFactionAndOwnership()
        {
            var converter = new FactionConverter(); var first = Context(); converter.Convert(first);
            Assert.IsFalse(first.Report.HasErrors, first.Report.ToText()); RefreshCatalog();
            string path = PathOf("faction_khuchad_clan"); byte[] original = File.ReadAllBytes(path); Assert.IsTrue(manifest.TryGetHash(path, out ulong hash));
            string temporary = Path.Combine(Path.GetTempPath(), "FactionMigration_" + Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(temporary, "content/Factions/faction_khuchad_clan.json");
            try
            {
                var changes = new (string Code, string Before, string After)[]
                {
                    ("MIG-E003", "\"id\":\"faction_khuchad_clan\"", "\"id\":\"faction_unknown\""),
                    ("MIG-E003", "\"shipKey\":\"ship_fighter\"", "\"shipKey\":\"ship_unknown\""),
                    ("MIG-E073", "\"weight\":0.5", "\"weight\":0"),
                    ("MIG-E073", "\"weight\":0.5", "\"weight\":1e300"),
                    ("MIG-E073", "\"basePlayerRelation\":0", "\"basePlayerRelation\":101"),
                    ("MIG-E002", "\"basePlayerRelation\":0", "\"unexpected\":1,\"basePlayerRelation\":0"),
                    ("MIG-E002", "\"color\":[", "\"color\":[null,")
                };
                foreach (var change in changes)
                {
                    CopyInputs(temporary); ReplaceFirst(sourcePath, change.Before, change.After);
                    var context = Context(legacyRoot: temporary); converter.Convert(context); AssertSkipped(context, change.Code, original, hash);
                }
                CopyInputs(temporary); var missing = Context(legacyRoot: temporary);
                missing.Catalog = BuildCatalog(prerequisites.Where(d => d.IdString != "ship_fighter"));
                Assert.IsFalse(converter.Preflight(missing)); Assert.AreEqual(0, missing.Writer.Outcomes.Count, "Preflight never writes.");
                missing = Context(legacyRoot: temporary); missing.Catalog = BuildCatalog(prerequisites.Where(d => d.IdString != "ship_fighter"));
                converter.Convert(missing); AssertSkipped(missing, "MIG-E004", original, hash);
                CopyInputs(temporary); ReplaceFirst(sourcePath, "\"id\":\"faction_khuchad_clan\"", "\"id\":\"faction_tlaxcala\"");
                byte[] alliance = File.ReadAllBytes(PathOf("faction_tlaxcala"));
                var duplicate = Context(legacyRoot: temporary); converter.Convert(duplicate);
                Assert.IsTrue(duplicate.Report.Entries.Any(e => e.Code == "MIG-E003")); Assert.AreEqual(1, duplicate.Writer.Outcomes.Count);
                Assert.AreEqual(PathOf("faction_vestari"), duplicate.Writer.Outcomes[0].AssetPath);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path)); CollectionAssert.AreEqual(alliance, File.ReadAllBytes(PathOf("faction_tlaxcala")));
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }

            // A later ENC-owned link fill updates its migration hash; reruns preserve that unrelated content.
            var faction = Faction("faction_khuchad_clan");
            var ai = AssetDatabase.LoadAssetAtPath<AiProfileDefinition>("Assets/Game/Content/Ai/ai_default.asset"); Assert.NotNull(ai);
            Assert.IsTrue(Context().Writer.SetLinkIfEmpty(faction, "defaultAiProfile", ai));
            var later = Context(); converter.Convert(later); Assert.IsFalse(later.Report.HasErrors, later.Report.ToText());
            Assert.AreSame(ai, faction.DefaultAiProfile); Assert.IsTrue(later.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged));
            using (var so = new SerializedObject(faction))
            { so.FindProperty("isPlayerFaction").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(faction); }
            byte[] playerBytes = File.ReadAllBytes(path); var protectedPlayer = Context(force: true); converter.Convert(protectedPlayer);
            Assert.IsTrue(protectedPlayer.Report.Entries.Any(e => e.Code == "MIG-E073")); CollectionAssert.AreEqual(playerBytes, File.ReadAllBytes(path));
            AssertInputsUnchanged();
        }

        void AssertSkipped(LegacyMigrationContext context, string code, byte[] bytes, ulong hash)
        {
            Assert.IsTrue(context.Report.HasErrors, code); Assert.IsTrue(context.Report.Entries.Any(e => e.Code == code), context.Report.ToText());
            Assert.AreEqual(2, context.Writer.Outcomes.Count, "Other factions continue.");
            Assert.IsFalse(context.Writer.Outcomes.Any(o => o.AssetPath == PathOf("faction_khuchad_clan")));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(PathOf("faction_khuchad_clan")));
            Assert.IsTrue(manifest.TryGetHash(PathOf("faction_khuchad_clan"), out ulong retained)); Assert.AreEqual(hash, retained);
        }
        void CopyInputs(string root)
        {
            foreach (var pair in inputs)
            {
                string path = Path.Combine(root, Path.GetRelativePath(Path.GetFullPath("Import/Legacy"), Path.GetFullPath(pair.Key)));
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pair.Value);
            }
        }
        static void ReplaceFirst(string path, string before, string after)
        {
            string text = File.ReadAllText(path); int at = text.IndexOf(before, StringComparison.Ordinal); Assert.GreaterOrEqual(at, 0, before);
            File.WriteAllText(path, text.Substring(0, at) + after + text.Substring(at + before.Length));
        }
        void AssertInputsUnchanged() { foreach (var pair in inputs) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key)); }
        void RefreshCatalog()
        {
            var converted = AssetDatabase.FindAssets("t:FactionDefinition", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FactionDefinition>).Cast<ContentDefinition>();
            catalog = BuildCatalog(prerequisites.Where(d => !(d is FactionDefinition)).Concat(converted));
        }
        static ContentCatalog BuildCatalog(IEnumerable<ContentDefinition> definitions)
        { var report = new ValidationReport("Faction fixtures"); var result = ContentCatalog.Build(definitions.ToArray(), report); Assert.IsFalse(report.HasErrors, report.ToText()); return result; }
        LegacyMigrationContext Context(bool force = false, string legacyRoot = null)
        {
            var report = new ValidationReport("MIG-012"); legacyRoot ??= Path.GetFullPath("Import/Legacy");
            var index = LegacyContentIndex.Load(legacyRoot, report); Assert.IsFalse(report.HasErrors, report.ToText());
            return new LegacyMigrationContext(index, LegacyIdMap.Default, new LegacyAssetWriter(Root, manifest, force, report), report, force, legacyRoot) { Catalog = catalog };
        }
        static string PathOf(string id) => Root + "/Factions/" + id + ".asset";
        static FactionDefinition Faction(string id) => AssetDatabase.LoadAssetAtPath<FactionDefinition>(PathOf(id));
    }
}
