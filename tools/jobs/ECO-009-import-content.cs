// ECO-009 lead-only import/readback recipe; outside Assets and unexecuted by the worker.
// Integrate GAL-012's real starsys_ark_start plus the ECO-009 asset candidates first.
// Copy to Assets/Game/Editor/EconomyContentJob.cs, invoke Game.Editor.Migration.EconomyContentJob.Run
// in a stopped Editor (RunCli for headless), then remove the temporary script/meta.
// The candidates are already authored YAML: this job imports and validates them, rebuilds the catalog,
// and verifies byte/GUID stability. It never replaces Scrap, creates stars or changes migration ownership.
// Commit the prepared 17 assets/metas and Merchants folder metadata, then the refreshed ContentDatabase.
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

namespace Game.Editor.Migration
{
    public static class EconomyContentJob
    {
        const string Evidence = "Logs/playtest/eco009";
        static readonly (string Id, string Name, int Stack, int Price)[] Items =
        {
            ("item_ai_core_data", "AI Core Data", 1, 0), ("item_ancient_data_log", "Ancient Data Log", 1, 0),
            ("item_blueprint_glass_beam", "Glass Beam Blueprint", 1, 0), ("item_blueprint_vanguard_plating", "Vanguard Plating Blueprint", 1, 0),
            ("item_genetic_stimulants", "Genetic Stimulants", 99, 150), ("item_mining_beam_mk2", "Mining Beam Mk II", 1, 0),
            ("item_psycho_gas", "Psycho Gas", 99, 80), ("item_reality_anchor", "Reality Anchor", 1, 0),
            ("item_research_data", "Research Data", 999, 40), ("item_scrap_cannon", "Scrap Cannon", 1, 0),
            ("item_seed_bank", "Seed Bank", 1, 0), ("item_stealth_patch", "Stealth Patch", 1, 0),
            ("item_terraforming_seed", "Terraforming Seed", 1, 0), ("item_vanguard_thruster", "Vanguard Thruster", 1, 0)
        };
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Economy content import requires a stopped Editor.");
            Directory.CreateDirectory(Evidence);
            var report = new ValidationReport("ECO-009 import/readback");
            string[] ids = Items.Select(i => i.Id).Concat(new[] { "resource_scrap", "loot_salvage_module", "merchant_station_market", "merchant_shady_trader" }).ToArray();
            foreach (string id in ids)
                if (!File.Exists(PathOf(id)) || !File.Exists(PathOf(id) + ".meta")) report.Error("MIG-E004", PathOf(id), "", "Integrate the prepared candidate asset and metadata first.");
            string[] snapshots = Directory.GetFiles(ContentDatabaseBuilder.ContentRoot, "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(p => p != ContentDatabaseBuilder.DatabasePath).ToArray();
            var bytes = snapshots.ToDictionary(p => p, File.ReadAllBytes);
            string[] legacy = Directory.GetFiles("Import/Legacy", "*", SearchOption.AllDirectories);
            var inputs = legacy.ToDictionary(p => p, File.ReadAllBytes);
            byte[] ownership = File.ReadAllBytes("Import/Migration/manifest.json");
            if (report.HasErrors) { File.WriteAllText(Evidence + "/preflight.txt", report.ToText()); throw new ContentValidationException(report); }
            AssetDatabase.Refresh();
            ContentCatalog Live() => ContentCatalog.Build(AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot }).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>()).ToArray(), report);
            var catalog = Live();
            var sources = AssetDatabase.LoadAssetAtPath<LegacyMigrationSources>(LegacyMigrationSources.AssetPath);
            var icons = sources == null ? new Dictionary<string, UnityEngine.Sprite>() : sources.LoadIcons(report);
            if (!icons.TryGetValue("spritetest2_transparent_0", out var icon)) report.Error("MIG-E004", LegacyMigrationSources.AssetPath, "itemIcons", "Required existing sheet sprite is unavailable.");
            if (!catalog.TryGet<AuthoredStarSystemDefinition>(new ContentId("starsys_ark_start"), out var ark) || !ark.Bodies.Any(b => b != null && b.key == "ark_station" && b.role == BodyRole.Station))
                report.Error("ECO-E003", "merchant_station_market", "authoredBodies", "Integrate GAL-012's real ARK system and ark_station body; no substitute is created.");
            foreach (string id in ids)
                if (!catalog.TryGet<ContentDefinition>(new ContentId(id), out var asset) || AssetDatabase.GetAssetPath(asset) != PathOf(id))
                    report.Error("MIG-E010", PathOf(id), "id", "Missing, duplicate or misplaced canonical identity.");
            File.WriteAllText(Evidence + "/preflight.txt", report.ToText());
            if (report.HasErrors) throw new ContentValidationException(report);
            var guids = ids.ToDictionary(id => PathOf(id), id => AssetDatabase.AssetPathToGUID(PathOf(id)));
            bool Present(ContentDefinition definition) => definition != null && catalog.TryGet<ContentDefinition>(definition.Id, out var actual) && ReferenceEquals(definition, actual);
            void Validate(ContentDefinition definition) => definition.Validate(report.For("content:" + definition.IdString), new ContentValidationContext(definition.IdString, true, Present, catalog));
            foreach (var row in Items)
            {
                var item = catalog.Get<ItemDefinition>(new ContentId(row.Id)); Validate(item);
                if (item.DisplayName != row.Name || item.Category != (row.Price == 0 ? ItemCategory.Quest : ItemCategory.Resource) || item.Rarity != ItemRarity.Rare ||
                    item.BasePrice != row.Price || item.MaxStack != row.Stack || item.Tradeable != (row.Price != 0) || item.Icon != icon)
                    report.Error("DATA-E020", PathOf(row.Id), "", "Narrative item differs from ECO-009's exact table.");
            }
            var scrap = catalog.Get<ItemDefinition>(new ContentId("resource_scrap")); Validate(scrap);
            if (scrap.DisplayName != "Scrap" || scrap.Category != ItemCategory.Resource || scrap.Rarity != ItemRarity.Common || scrap.BasePrice != 2 || scrap.MaxStack != 999 || scrap.Icon != icon)
                report.Error("DATA-E020", PathOf("resource_scrap"), "", "Existing brought-forward Scrap must already match ECO-009; this job never replaces it.");
            foreach (var loot in catalog.All<LootTableDefinition>()) Validate(loot);
            foreach (var merchant in catalog.All<MerchantDefinition>()) Validate(merchant);
            foreach (var config in catalog.All<EconomyConfigDefinition>()) Validate(config);
            new EconomyContentValidator().Validate(catalog, report);
            var salvage = catalog.Get<LootTableDefinition>(new ContentId("loot_salvage_module"));
            if (salvage.DefaultPicks != 1 || salvage.Entries.Count != 4 || !salvage.Entries.All(e => e.min == 1 && e.max == 1) ||
                !salvage.Entries.Select(e => e.item.IdString + ":" + e.chancePercent).SequenceEqual(new[] { "module_armor_plating:30", "module_coolant_injector:30", "module_micro_plating:30", "module_shield_efficiency:20" }))
                report.Error("ECO-E001", PathOf("loot_salvage_module"), "", "Salvage module weights/order/counts differ.");
            var station = catalog.Get<MerchantDefinition>(new ContentId("merchant_station_market"));
            CheckMerchant(station, "ARK Station Market", 24, 1, .5f, ItemCategoryMask.Block | ItemCategoryMask.Resource | ItemCategoryMask.Module,
                "resource_fuel:-1,block_hull:200,block_hull_angled:100,block_thruster_small:10,block_cargo_small:6,block_shield_small:4,block_reactor_small:2,weapon_plasma_turret_small:4,weapon_small_beam_turret:2");
            if (station.AuthoredBodies.Count != 1 || station.AuthoredBodies[0].starSystem != ark || station.AuthoredBodies[0].bodyKey != "ark_station")
                report.Error("ECO-E003", PathOf(station.IdString), "authoredBodies", "Station market must target the real ARK station.");
            var shady = catalog.Get<MerchantDefinition>(new ContentId("merchant_shady_trader"));
            CheckMerchant(shady, "Shady Trader", 48, 1.3f, .6f, ItemCategoryMask.Resource | ItemCategoryMask.Module | ItemCategoryMask.Misc,
                "resource_fuel:40,module_coolant_injector:2,module_micro_plating:2,item_psycho_gas:3");
            if (shady.AuthoredBodies.Count != 0) report.Error("ECO-E003", PathOf(shady.IdString), "authoredBodies", "Shady Trader is dialog-only, with no location.");
            File.WriteAllText(Evidence + "/readback.txt", report.ToText());
            if (report.HasErrors) throw new ContentValidationException(report);
            var database = ContentDatabaseBuilder.Rebuild();
            foreach (string id in ids) if (!database.Definitions.Any(d => d != null && d.IdString == id)) report.Error("MIG-E004", "ContentDatabase", id, "Canonical asset omitted from rebuilt database.");
            foreach (var pair in bytes.Concat(inputs)) if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) report.Error("MIG-E099", pair.Key, "", "Import/rebuild changed content, metadata or read-only input bytes.");
            foreach (var pair in guids) if (pair.Value != AssetDatabase.AssetPathToGUID(pair.Key)) report.Error("MIG-E099", pair.Key, "", "GUID changed.");
            if (!ownership.SequenceEqual(File.ReadAllBytes("Import/Migration/manifest.json"))) report.Error("MIG-E099", "Import/Migration/manifest.json", "", "Unrelated migration ownership changed.");
            File.WriteAllText(Evidence + "/catalog-and-stability.txt", report.ToText());
            File.WriteAllLines(Evidence + "/outputs.tsv", ids.Select(id => id + "\t" + PathOf(id) + "\t" + AssetDatabase.AssetPathToGUID(PathOf(id))));
            if (report.HasErrors) throw new ContentValidationException(report);
            GameLog.Info("Economy", "ECO-009: fourteen narrative items, existing Scrap, weighted loot and two real merchants verified with zero ECO errors; only catalog refreshed.");
            void CheckMerchant(MerchantDefinition merchant, string name, float hours, float sell, float buy, ItemCategoryMask buys, string stock)
            {
                if (merchant.DisplayName != name || merchant.RestockHours != hours || merchant.SellPriceFactor != sell || merchant.BuyPriceFactor != buy || merchant.Buys != buys ||
                    merchant.BodyTemplates.Count != 0 || !merchant.Stock.All(e => e.priceFactor == 1) || !merchant.Stock.Select(e => e.item.IdString + ":" + e.count).SequenceEqual(stock.Split(',')))
                    report.Error("ECO-E002", PathOf(merchant.IdString), "", "Merchant differs from ECO-009's exact table.");
            }
        }
        public static void RunCli() { try { Run(); EditorApplication.Exit(0); } catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); } }
        static string PathOf(string id) => ContentDatabaseBuilder.ContentRoot + (id.StartsWith("merchant_", StringComparison.Ordinal) ? "/Merchants/" : id.StartsWith("loot_", StringComparison.Ordinal) ? "/Loot/" : "/Items/") + id + ".asset";
    }
}
