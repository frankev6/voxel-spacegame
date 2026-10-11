using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>SPEC-72 MIG-005: real export to a disposable content root, checked against the explicit value table.</summary>
    public sealed class ItemConverterTests
    {
        const string Root = "Assets/Game/Content/_MigrationTest";
        LegacyMigrationContext context;
        Dictionary<string, ItemDefinition> items;
        bool ownsRoot;
        [OneTimeSetUp] public void ConvertExport()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root), "Never remove existing user content."); ownsRoot = true;
            var report = new ValidationReport("MIG"); string legacyRoot = Path.GetFullPath("Import/Legacy");
            var index = LegacyContentIndex.Load(legacyRoot, report);
            var writer = new LegacyAssetWriter(Root, new MigrationManifest(), false, report);
            context = new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, false, legacyRoot);
            new ItemConverter().Convert(context);
            Assert.IsFalse(report.HasErrors, report.ToText());
            items = writer.Outcomes.ToDictionary(o => Path.GetFileNameWithoutExtension(o.AssetPath), o => AssetDatabase.LoadAssetAtPath<ItemDefinition>(o.AssetPath), StringComparer.Ordinal);
        }
        [OneTimeTearDown] public void Clean() { if (ownsRoot) AssetDatabase.DeleteAsset(Root); }
        [Test] public void RealExport_ProducesExpectedItems()
        {
            Assert.AreEqual(35, items.Count);
            var expected = new (string Id, string Name, ItemCategory Category, ItemRarity Rarity, int Price, int Stack)[]
            {
                ("item_regeneration_serum", "Regeneration Serum", ItemCategory.Resource, ItemRarity.Epic, 100, 999),
                ("item_research_blueprint", "Research Blueprint", ItemCategory.Resource, ItemRarity.Rare, 0, 999),
                ("item_strange_matter", "Strange Matter", ItemCategory.Quest, ItemRarity.Legendary, 0, 1),
                ("resource_palladium", "Palladium [Pd]", ItemCategory.Resource, ItemRarity.Common, 100, 1),
                ("resource_crew", "Crew", ItemCategory.Resource, ItemRarity.Common, 5, 999),
                ("resource_fuel", "Fuel", ItemCategory.Resource, ItemRarity.Common, 100, 999),
                ("item_slot_expansion", "Slot Expansion", ItemCategory.Consumable, ItemRarity.Epic, 500, 999),
                ("item_strange_matter_container_empty", "Strange Matter Container [EMPTY]", ItemCategory.Quest, ItemRarity.Legendary, 1000, 999),
                ("item_strange_matter_container_full", "Strange Matter Container [FULL]", ItemCategory.Quest, ItemRarity.Legendary, 2500, 1),
                ("module_armor_plating", "Armor Plating", ItemCategory.Module, ItemRarity.Rare, 10, 999),
                ("module_coolant_injector", "Coolant Injector", ItemCategory.Module, ItemRarity.Common, 10, 999),
                ("module_scrap_collector", "Scrap Collector", ItemCategory.Module, ItemRarity.Common, 15, 999),
                ("module_increase_weapon_rotation", "Targeting Computer", ItemCategory.Module, ItemRarity.Common, 10, 999),
                ("module_micro_plating", "Micro-Plating", ItemCategory.Module, ItemRarity.Common, 10, 999),
                ("module_shield_efficiency", "Shield Efficiency", ItemCategory.Module, ItemRarity.Common, 20, 999),
                ("module_trained_crew", "Trained Crew", ItemCategory.Module, ItemRarity.Legendary, 19, 999),
                ("officer_august", "August", ItemCategory.Officer, ItemRarity.Unique, 0, 1),
                ("officer_gunner_vax", "Vax Vandervalk", ItemCategory.Officer, ItemRarity.Unique, 1000, 1),
            };
            foreach (var e in expected) Check(items[e.Id], e.Id, e.Name, e.Category, e.Rarity, e.Price, e.Stack);
            string[] colours = { "Default", "Dark Gray", "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Cyan", "Pink", "Brown", "Black", "Military Green", "Steel Blue", "Tan", "Dark Red", "White" };
            for (int i = 0; i < colours.Length; i++)
            {
                string id = $"paint_{i:00}_{colours[i].ToLowerInvariant().Replace(' ', '_')}";
                Check(items[id], id, "Paint Bucket (" + colours[i] + ")", ItemCategory.Paint, ItemRarity.Common, 1, 999);
            }
            var validation = new ValidationReport("Items");
            var definitions = items.Values.Cast<ContentDefinition>().ToList();
            definitions.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"));
            definitions.Add(AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"));
            ContentCatalog.Build(definitions, validation);
            foreach (var item in items.Values)
            {
                string path = AssetDatabase.GetAssetPath(item);
                item.Validate(validation.For(path), new ContentValidationContext(path, true, definitions.Contains));
            }
            Assert.IsFalse(validation.HasErrors, validation.ToText());
        }
        static void Check(ItemDefinition item, string id, string name, ItemCategory category, ItemRarity rarity, int price, int stack)
        {
            Assert.NotNull(item, id); Assert.AreEqual(id, item.IdString); Assert.AreEqual(name, item.DisplayName, id);
            Assert.AreEqual(category, item.Category, id); Assert.AreEqual(rarity, item.Rarity, id);
            Assert.AreEqual(price, item.BasePrice, id); Assert.AreEqual(stack, item.MaxStack, id);
        }
        [Test] public void Icons_Assigned()
        {
            int checkedCount = 0;
            foreach (var file in context.Index.Files)
            {
                if (file.Type != "ResourceItem" && file.Type != "SlottableItem" && file.Type != "PaintBucketItem") continue;
                LegacyItemDto dto = file.Type == "SlottableItem" ? LegacyJson.Read<LegacySlottableItemDto>(file, context.Report)
                    : file.Type == "PaintBucketItem" ? LegacyJson.Read<LegacyPaintBucketItemDto>(file, context.Report)
                    : LegacyJson.Read<LegacyItemDto>(file, context.Report);
                Assert.NotNull(dto);
                Assert.IsTrue(context.IdMap.TryMap(LegacyIdKind.Item, dto.RegistryKey, out string id));
                Assert.NotNull(items[id].Icon, id); Assert.AreEqual(dto.ItemImage.Name, items[id].Icon.name, id);
                StringAssert.StartsWith("spritetest2_transparent_", items[id].Icon.name);
                Assert.AreEqual(dto.Description, items[id].Description, id); checkedCount++;
            }
            Assert.AreEqual(35, checkedCount);
        }
        [Test] public void DroppedFields_Reported()
        {
            var entries = context.Report.Entries.Where(e => e.Code == "MIG-I091").ToArray();
            Assert.AreEqual(30, entries.Length);
            foreach (var file in context.Index.Files.Where(f => f.Type == "PaintBucketItem"))
            {
                string message = entries.Single(e => e.AssetPath == file.RelativePath).Message;
                StringAssert.Contains("PaletteIndex", message); StringAssert.Contains("EffectType Paint", message);
            }
            string full = entries.Single(e => e.AssetPath.EndsWith("strange_matter_container_full.json", StringComparison.Ordinal)).Message;
            StringAssert.Contains("RequiredSocketType", full); StringAssert.Contains("3 Modifiers", full);
            StringAssert.Contains("EffectType AddModuleSlot", entries.Single(e => e.AssetPath.EndsWith("slot_expansion_item.json", StringComparison.Ordinal)).Message);
        }
    }
}
