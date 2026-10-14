using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Content.Validators;
using Game.Editor.Migration;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Economy
{
    /// <summary>ECO-009: two immediate checks of actual authored assets and canonical references. No simulation or writes.</summary>
    public sealed class EconomyContentSmokeTests
    {
        internal static readonly (string Id, string Name, int Stack, int Price)[] Narrative =
        {
            ("item_ai_core_data", "AI Core Data", 1, 0), ("item_ancient_data_log", "Ancient Data Log", 1, 0),
            ("item_blueprint_glass_beam", "Glass Beam Blueprint", 1, 0), ("item_blueprint_vanguard_plating", "Vanguard Plating Blueprint", 1, 0),
            ("item_genetic_stimulants", "Genetic Stimulants", 99, 150), ("item_mining_beam_mk2", "Mining Beam Mk II", 1, 0),
            ("item_psycho_gas", "Psycho Gas", 99, 80), ("item_reality_anchor", "Reality Anchor", 1, 0),
            ("item_research_data", "Research Data", 999, 40), ("item_scrap_cannon", "Scrap Cannon", 1, 0),
            ("item_seed_bank", "Seed Bank", 1, 0), ("item_stealth_patch", "Stealth Patch", 1, 0),
            ("item_terraforming_seed", "Terraforming Seed", 1, 0), ("item_vanguard_thruster", "Vanguard Thruster", 1, 0)
        };
        internal static ContentCatalog Catalog()
        {
            var definitions = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot }).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>()).ToArray();
            var report = new ValidationReport("ECO-009 catalog"); var catalog = ContentCatalog.Build(definitions, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText()); return catalog;
        }
        [Test] public void NarrativeItemsExist()
        {
            var catalog = Catalog(); var iconReport = new ValidationReport("ECO-009 icons");
            var sources = AssetDatabase.LoadAssetAtPath<LegacyMigrationSources>(LegacyMigrationSources.AssetPath); Assert.IsNotNull(sources);
            var icons = sources.LoadIcons(iconReport); Assert.AreEqual(0, iconReport.ErrorCount, iconReport.ToText());
            Assert.IsTrue(icons.TryGetValue("spritetest2_transparent_0", out var icon));
            foreach (var row in Narrative)
            {
                Assert.IsTrue(catalog.TryGet<ItemDefinition>(new ContentId(row.Id), out var item), row.Id);
                Assert.AreEqual(row.Name, item.DisplayName, row.Id); Assert.AreEqual(ItemRarity.Rare, item.Rarity, row.Id);
                Assert.AreEqual(row.Price == 0 ? ItemCategory.Quest : ItemCategory.Resource, item.Category, row.Id);
                Assert.AreEqual(row.Stack, item.MaxStack, row.Id); Assert.AreEqual(row.Price, item.BasePrice, row.Id);
                Assert.AreEqual(row.Price != 0, item.Tradeable, row.Id); Assert.AreSame(icon, item.Icon, row.Id);
            }
            var scrap = catalog.Get<ItemDefinition>(new ContentId("resource_scrap"));
            Assert.AreEqual("Scrap", scrap.DisplayName); Assert.AreEqual(ItemCategory.Resource, scrap.Category); Assert.AreEqual(ItemRarity.Common, scrap.Rarity);
            Assert.AreEqual(999, scrap.MaxStack); Assert.AreEqual(2, scrap.BasePrice); Assert.AreSame(icon, scrap.Icon);
            // Existing IDs also emitted by the legacy logic translator; never substitute numeric item IDs.
            foreach (string id in new[] { "resource_fuel", "resource_crew", "resource_palladium", "item_strange_matter_container_full", "item_research_blueprint" })
                Assert.IsTrue(catalog.TryGet<ItemDefinition>(new ContentId(id), out _), id);
            foreach (string id in new[] { "loot_base", "loot_creative", "loot_salvage_module" }) Assert.IsTrue(catalog.TryGet<LootTableDefinition>(new ContentId(id), out _), id);
            foreach (string id in new[] { "merchant_station_market", "merchant_shady_trader" }) Assert.IsTrue(catalog.TryGet<MerchantDefinition>(new ContentId(id), out _), id);
        }
        [Test] public void ContentValidates()
        {
            var catalog = Catalog(); var report = new ValidationReport("ECO-009 scoped content");
            bool Present(ContentDefinition definition) => definition != null && catalog.TryGet<ContentDefinition>(definition.Id, out var actual) && ReferenceEquals(definition, actual);
            foreach (string id in Narrative.Select(r => r.Id).Concat(new[] { "resource_scrap" })) Validate(catalog.Get<ItemDefinition>(new ContentId(id)));
            foreach (var loot in catalog.All<LootTableDefinition>()) Validate(loot);
            foreach (var merchant in catalog.All<MerchantDefinition>()) Validate(merchant);
            foreach (var config in catalog.All<EconomyConfigDefinition>()) Validate(config);
            new EconomyContentValidator().Validate(catalog, report); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            var table = catalog.Get<LootTableDefinition>(new ContentId("loot_salvage_module")); Assert.AreEqual(1, table.DefaultPicks);
            CollectionAssert.AreEqual(new[] { "module_armor_plating", "module_coolant_injector", "module_micro_plating", "module_shield_efficiency" }, table.Entries.Select(e => e.item.IdString));
            CollectionAssert.AreEqual(new[] { 30f, 30f, 30f, 20f }, table.Entries.Select(e => e.chancePercent)); Assert.IsTrue(table.Entries.All(e => e.min == 1 && e.max == 1));
            var station = catalog.Get<MerchantDefinition>(new ContentId("merchant_station_market"));
            Assert.AreEqual("ARK Station Market", station.DisplayName); Assert.AreEqual(24, station.RestockHours); Assert.AreEqual(1, station.SellPriceFactor); Assert.AreEqual(.5f, station.BuyPriceFactor);
            Assert.AreEqual(ItemCategoryMask.Block | ItemCategoryMask.Resource | ItemCategoryMask.Module, station.Buys);
            Stock(station, "resource_fuel:-1,block_hull:200,block_hull_angled:100,block_thruster_small:10,block_cargo_small:6,block_salvage_magnet_small:3,block_shield_small:4,block_reactor_small:2,weapon_plasma_turret_small:4,weapon_small_beam_turret:2");
            Assert.IsEmpty(station.BodyTemplates); Assert.AreEqual(1, station.AuthoredBodies.Count);
            var placement = station.AuthoredBodies.Single(); Assert.IsNotNull(placement.starSystem, "Integrate GAL-012 before ECO-009 validation.");
            Assert.AreEqual("starsys_ark_start", placement.starSystem.IdString); Assert.AreEqual("ark_station", placement.bodyKey);
            Assert.IsTrue(placement.starSystem.Bodies.Any(b => b.key == placement.bodyKey && b.role == BodyRole.Station));
            var shady = catalog.Get<MerchantDefinition>(new ContentId("merchant_shady_trader"));
            Assert.AreEqual("Shady Trader", shady.DisplayName); Assert.AreEqual(48, shady.RestockHours); Assert.AreEqual(1.3f, shady.SellPriceFactor); Assert.AreEqual(.6f, shady.BuyPriceFactor);
            Assert.AreEqual(ItemCategoryMask.Resource | ItemCategoryMask.Module | ItemCategoryMask.Misc, shady.Buys);
            Stock(shady, "resource_fuel:40,module_coolant_injector:2,module_micro_plating:2,item_psycho_gas:3");
            Assert.IsEmpty(shady.AuthoredBodies); Assert.IsEmpty(shady.BodyTemplates);
            void Validate(ContentDefinition definition) => definition.Validate(report.For("content:" + definition.IdString), new ContentValidationContext(definition.IdString, true, Present, catalog));
            void Stock(MerchantDefinition merchant, string expected)
            {
                CollectionAssert.AreEqual(expected.Split(','), merchant.Stock.Select(e => e.item.IdString + ":" + e.count));
                Assert.IsTrue(merchant.Stock.All(e => e.priceFactor == 1));
                foreach (var entry in merchant.Stock) Assert.AreSame(catalog.Get<ItemDefinition>(entry.item.Id), entry.item);
            }
        }
    }
}
