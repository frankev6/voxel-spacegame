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
    /// <summary>MIG-013: real export values, reference provenance, byte-stable reruns and per-table failure isolation.</summary>
    public sealed class LootTableConverterTests
    {
        const string Root = "Assets/Game/Content/_LootMigrationTest";
        MigrationManifest manifest;
        ContentCatalog catalog;
        Dictionary<string, byte[]> inputs;
        bool ownsRoot;

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root) || Directory.Exists(Root) || File.Exists(Root + ".meta"), "Never remove existing user content.");
            ownsRoot = true; manifest = new MigrationManifest();
            inputs = Directory.GetFiles("Import/Legacy/content", "*.json", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            var context = Context(); new ItemConverter().Convert(context); new BlockConverter().Convert(context);
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            var definitions = context.Writer.Outcomes.Select(o => AssetDatabase.LoadAssetAtPath<ItemDefinition>(o.AssetPath)).Cast<ContentDefinition>().ToList();
            definitions.Add(AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"));
            definitions.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"));
            catalog = ContentCatalog.Build(definitions, context.Report);
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
        }

        [TearDown] public void Clean()
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(Root);
            ownsRoot = false;
        }

        [Test]
        public void RealExport_EntriesInOrder()
        {
            var converter = new LootTableConverter(); Assert.AreEqual("Loot", converter.Name); Assert.AreEqual(410, converter.Order);
            var context = Context(); converter.Convert(context);
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText()); Assert.AreEqual(2, context.Writer.Outcomes.Count);
            Assert.IsTrue(context.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Created));
            Assert.AreEqual(2, AssetDatabase.FindAssets("t:LootTableDefinition", new[] { Root }).Length);
            AssertEntries("loot_base", @"block_core_small,1,1,100
module_scrap_collector,1,1,100
module_coolant_injector,1,1,100
block_thruster_medium,5,10,100
block_hull,5,10,100
weapon_plasma_turret_small,5,10,100
weapon_small_beam_turret,5,10,100
module_armor_plating,5,5,100
item_research_blueprint,2,4,100
resource_fuel,27,30,100
module_trained_crew,5,5,100
block_shield_small,5,5,100
block_capacitor_small,5,5,100
officer_gunner_vax,1,1,100
block_core_cross,1,1,100");
            AssertEntries("loot_creative", @"block_hull,1,1,100
block_hull_angled,1,1,100
block_core_small,1,1,100
module_scrap_collector,1,1,100
module_coolant_injector,1,1,100
block_thruster_medium,1,1,100
weapon_plasma_turret_small,1,1,100
weapon_small_beam_turret,1,1,100
module_armor_plating,1,1,100
module_trained_crew,1,1,100
block_shield_small,1,1,100
block_capacitor_small,1,1,100
officer_gunner_vax,1,1,100
block_prismatic_mirror_small,1,1,100
weapon_fixed_beam_turret_small,1,1,100
weapon_plasma_turret_medium,1,1,100
block_core_cross,1,1,100
block_prismatic_mirror_fixed_small,1,1,100
weapon_plasma_turret_small_fixed,1,1,100
block_thruster_small,1,1,100
weapon_railgun_medium,1,1,100
resource_fuel,50,50,100
weapon_flak_cannon_medium,1,1,100
weapon_missile_launcher_small,1,1,100
item_strange_matter_container_full,1,1,100
paint_04_blue,1,1,100
paint_02_red,1,1,100
paint_14_tan,1,1,100
paint_15_dark_red,1,1,100
paint_01_dark_gray,1,1,100
item_slot_expansion,1,1,100
paint_05_yellow,1,1,100
block_thruster_medium,1,1,100
item_slot_expansion,1,1,100
paint_13_steel_blue,1,1,100
paint_08_cyan,1,1,100");
            var ships = context.Report.Entries.Where(e => e.Code == "MIG-W070").ToArray(); Assert.AreEqual(4, ships.Length);
            CollectionAssert.AreEqual(new[] { "lootItems[14].item", "lootItems[15].item", "lootItems[16].item", "lootItems[18].item" }, ships.Select(e => e.Field));
            string[] ids = { "ship_beetle", "ship_shuttle", "ship_crescent", "ship_fighter" };
            for (int i = 0; i < ids.Length; i++) { Assert.IsTrue(ships[i].Message.Contains(ids[i])); Assert.IsTrue(ships[i].AssetPath.EndsWith("BaseLootTable.json", StringComparison.Ordinal)); }
            var empty = context.Report.Entries.Single(e => e.Code == "MIG-W071");
            Assert.AreEqual("content/Loot Tables/CreativeModeLootTable.json", empty.AssetPath); Assert.AreEqual("lootItems[11].item", empty.Field);
            var dead = context.Report.Entries.Single(e => e.Code == "MIG-I091");
            Assert.IsTrue(dead.Message.Contains("StartShipsTable (5 legacy ship references)"));
            Assert.AreEqual(5, context.Report.WarningCount); Assert.AreEqual(1, context.Report.InfoCount);

            var snapshots = context.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => (Bytes: File.ReadAllBytes(o.AssetPath), Guid: AssetDatabase.AssetPathToGUID(o.AssetPath)));
            foreach (var pair in snapshots)
            {
                Assert.IsTrue(manifest.TryGetHash(pair.Key, out ulong hash)); Assert.AreEqual(StableHash.Hash64(pair.Value.Bytes), hash);
                AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate);
            }
            var again = Context(); converter.Convert(again);
            Assert.IsFalse(again.Report.HasErrors, again.Report.ToText()); Assert.AreEqual(2, again.Writer.Outcomes.Count);
            Assert.IsTrue(again.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged)); Assert.AreEqual(context.Report.ToText(), again.Report.ToText());
            foreach (var pair in snapshots)
            { CollectionAssert.AreEqual(pair.Value.Bytes, File.ReadAllBytes(pair.Key), pair.Key); Assert.AreEqual(pair.Value.Guid, AssetDatabase.AssetPathToGUID(pair.Key)); }

            string path = PathOf("loot_base"); var table = Table("loot_base");
            Assert.IsTrue(manifest.TryGetHash(path, out ulong ownedHash));
            using (var so = new SerializedObject(table))
            { so.FindProperty("entries").GetArrayElementAtIndex(0).FindPropertyRelative("max").intValue = 9; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(table); }
            byte[] edited = File.ReadAllBytes(path); var kept = Context(); converter.Convert(kept);
            Assert.IsFalse(kept.Report.HasErrors, kept.Report.ToText()); Assert.AreEqual(1, kept.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.SkippedHandEdited));
            CollectionAssert.AreEqual(edited, File.ReadAllBytes(path)); Assert.AreEqual(9, table.Entries[0].max);
            Assert.IsTrue(manifest.TryGetHash(path, out ulong retainedHash)); Assert.AreEqual(ownedHash, retainedHash);
            Assert.AreNotEqual(retainedHash, StableHash.Hash64(edited), "A protected rerun must not adopt hand edits into the manifest.");
            var forced = Context(true); converter.Convert(forced);
            Assert.IsFalse(forced.Report.HasErrors, forced.Report.ToText()); Assert.AreEqual(1, table.Entries[0].max);
            CollectionAssert.AreEqual(snapshots[path].Bytes, File.ReadAllBytes(path)); Assert.AreEqual(snapshots[path].Guid, AssetDatabase.AssetPathToGUID(path));
            foreach (var pair in inputs) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), "Legacy input remains read-only: " + pair.Key);
        }

        [Test]
        public void InvalidInput_SkipsBrokenTable()
        {
            var converter = new LootTableConverter(); var initial = Context(); converter.Convert(initial);
            Assert.IsFalse(initial.Report.HasErrors, initial.Report.ToText());
            byte[] original = File.ReadAllBytes(PathOf("loot_base")); Assert.IsTrue(manifest.TryGetHash(PathOf("loot_base"), out ulong hash));
            var source = initial.Index.OfType("LootTable").Single(f => f.Name == "BaseLootTable");
            var dto = LegacyJson.Read<LegacyLootTableDto>(source, initial.Report); Assert.NotNull(dto);
            Assert.IsTrue(initial.Index.TryResolve(dto.LootItems[8].Item, out var research));
            string temporary = Path.Combine(Path.GetTempPath(), "LootMigration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string lootPath = Path.Combine(temporary, source.RelativePath), researchPath = Path.Combine(temporary, research.RelativePath);
                // Like LegacyJsonTests, mutate disposable text copies: the test assembly has no Newtonsoft reference.
                var changes = new (string Code, Action Mutate)[]
                {
                    ("MIG-E004", () => ReplaceFirst(lootPath, "\"$ref\":\"" + dto.LootItems[0].Item.Path + "\"", "\"$ref\":\"Assets/unknown.asset\"")),
                    ("MIG-E072", () => ReplaceFirst(lootPath, "\"minAmount\":1", "\"minAmount\":0")),
                    ("MIG-E072", () => ReplaceFirst(lootPath, "\"maxAmount\":1", "\"maxAmount\":10001")),
                    ("MIG-E072", () => ReplaceFirst(lootPath, "\"dropChance\":100", "\"dropChance\":101")),
                    ("MIG-E002", () => ReplaceFirst(lootPath, "\"minAmount\":1", "\"unrecognized\":1,\"minAmount\":1")),
                    ("MIG-E003", () => ReplaceFirst(lootPath, "\"$name\":\"BaseLootTable\"", "\"$name\":\"UnmappedLootTable\"")),
                    ("MIG-E003", () => ReplaceFirst(researchPath, "\"RegistryKey\":\"item_research_blueprint\"", "\"RegistryKey\":\"unknown_research_item\""))
                };
                foreach (var change in changes)
                {
                    CopyInputs(temporary); change.Mutate();
                    var context = Context(legacyRoot: temporary); converter.Convert(context);
                    AssertSkipped(context, change.Code, original, hash);
                }
                CopyInputs(temporary);
                var incomplete = Context(legacyRoot: temporary); var report = new ValidationReport("Incomplete prerequisite catalog");
                incomplete.Catalog = ContentCatalog.Build(catalog.All<ItemDefinition>().Where(d => d.IdString != "item_research_blueprint").Cast<ContentDefinition>().ToArray(), report);
                Assert.IsFalse(report.HasErrors, report.ToText()); converter.Convert(incomplete); AssertSkipped(incomplete, "MIG-E004", original, hash);

                // References use the indexed $path and the target DTO's RegistryKey, never ref name/type hints.
                ReplaceFirst(lootPath, "\"name\":\"" + dto.LootItems[0].Item.Name + "\"", "\"name\":\"ship_fighter\"");
                ReplaceFirst(lootPath, "\"type\":\"" + dto.LootItems[0].Item.Type + "\"", "\"type\":\"ShipItem\"");
                var hints = Context(legacyRoot: temporary); converter.Convert(hints);
                Assert.IsFalse(hints.Report.HasErrors, hints.Report.ToText()); Assert.AreEqual(2, hints.Writer.Outcomes.Count);
                Assert.IsTrue(hints.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged));
            }
            finally { Directory.Delete(temporary, true); }
            foreach (var pair in inputs) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
        }

        void AssertSkipped(LegacyMigrationContext context, string code, byte[] original, ulong hash)
        {
            Assert.IsTrue(context.Report.HasErrors, code); Assert.IsTrue(context.Report.Entries.Any(e => e.Code == code), context.Report.ToText());
            Assert.AreEqual(1, context.Writer.Outcomes.Count); Assert.AreEqual(PathOf("loot_creative"), context.Writer.Outcomes[0].AssetPath, "The other table still runs.");
            CollectionAssert.AreEqual(original, File.ReadAllBytes(PathOf("loot_base"))); Assert.IsTrue(manifest.TryGetHash(PathOf("loot_base"), out ulong retained)); Assert.AreEqual(hash, retained);
        }
        void CopyInputs(string root)
        {
            string sourceRoot = Path.GetFullPath("Import/Legacy");
            foreach (var pair in inputs)
            {
                string path = Path.Combine(root, Path.GetRelativePath(sourceRoot, Path.GetFullPath(pair.Key)));
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pair.Value);
            }
        }
        static void ReplaceFirst(string path, string before, string after)
        {
            string text = File.ReadAllText(path); int at = text.IndexOf(before, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "Expected mutation token in " + path + ": " + before);
            File.WriteAllText(path, text.Substring(0, at) + after + text.Substring(at + before.Length));
        }
        LegacyMigrationContext Context(bool force = false, string legacyRoot = null)
        {
            var report = new ValidationReport("MIG-013"); legacyRoot ??= Path.GetFullPath("Import/Legacy");
            var index = LegacyContentIndex.Load(legacyRoot, report); Assert.IsFalse(report.HasErrors, report.ToText());
            return new LegacyMigrationContext(index, LegacyIdMap.Default, new LegacyAssetWriter(Root, manifest, force, report), report, force, legacyRoot) { Catalog = catalog };
        }
        void AssertEntries(string id, string expected)
        {
            var table = Table(id); Assert.NotNull(table); Assert.AreEqual(id, table.IdString); Assert.AreEqual(0, table.DefaultPicks);
            var rows = expected.Split('\n'); Assert.AreEqual(rows.Length, table.Entries.Count);
            for (int i = 0; i < rows.Length; i++)
            {
                var fields = rows[i].Trim().Split(','); var entry = table.Entries[i]; Assert.NotNull(entry.item, id + " row " + i);
                Assert.AreEqual(fields[0], entry.item.IdString); Assert.AreSame(catalog.Get<ItemDefinition>(new ContentId(fields[0])), entry.item);
                Assert.AreEqual(int.Parse(fields[1]), entry.min); Assert.AreEqual(int.Parse(fields[2]), entry.max); Assert.AreEqual(float.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture), entry.chancePercent);
            }
        }
        static string PathOf(string id) => Root + "/Loot/" + id + ".asset";
        static LootTableDefinition Table(string id) => AssetDatabase.LoadAssetAtPath<LootTableDefinition>(PathOf(id));
    }
}
