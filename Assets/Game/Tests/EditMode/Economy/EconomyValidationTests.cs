using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content.Validators;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Economy
{
    /// <summary>SPEC-50 ECO-002: one failing/passing fixture per code and quest tradeability; no production assets written.</summary>
    public sealed class EconomyValidationTests
    {
        ItemDefinition fuel, scrap, missingItem;
        BlockDefinition hull, angled, missingBlock;
        AudioCueDefinition cue;

        [SetUp] public void SetUp()
        {
            fuel = Item("resource_fuel", 10);
            scrap = Item("resource_scrap", 2);
            missingItem = Item("item_missing", 0);
            hull = TestContentFactory.Create<BlockDefinition>("block_hull");
            angled = TestContentFactory.Create<BlockDefinition>("block_hull_angled");
            missingBlock = TestContentFactory.Create<BlockDefinition>("block_missing");
            cue = TestContentFactory.Create<AudioCueDefinition>("cue_ui_success");
        }
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();

        static ItemDefinition Item(string id, int price) => TestContentFactory.Create<ItemDefinition>(id, so =>
        {
            TestContentFactory.Set(so, "displayName", id);
            TestContentFactory.Set(so, "category", (int)ItemCategory.Resource);
            TestContentFactory.Set(so, "basePrice", price);
        });

        LootTableDefinition Loot() => TestContentFactory.Create<LootTableDefinition>("loot_test", so =>
            LootRow(so, "entries", 0, fuel, 1, 3, 50));
        MerchantDefinition Merchant(string id = "merchant_test") => TestContentFactory.Create<MerchantDefinition>(id, so =>
        {
            TestContentFactory.Set(so, "displayName", id);
            var stock = so.FindProperty("stock"); stock.arraySize = 1;
            var row = stock.GetArrayElementAtIndex(0);
            row.FindPropertyRelative("item").objectReferenceValue = fuel;
            row.FindPropertyRelative("count").intValue = 5;
            row.FindPropertyRelative("priceFactor").floatValue = 1;
        });
        EconomyConfigDefinition Config(string id = "config_economy") => TestContentFactory.Create<EconomyConfigDefinition>(id, so =>
        {
            TestContentFactory.Set(so, "scrapItem", scrap);
            TestContentFactory.Set(so, "salvageCue", cue);
            LootRow(so, "startItems", 0, fuel, 50, 50, 100);
            LootRow(so, "startItems", 1, hull, 40, 40, 100);
            LootRow(so, "startItems", 2, angled, 20, 20, 100);
        });
        static void LootRow(SerializedObject so, string field, int index, ItemDefinition item, int min, int max, float chance)
        {
            var list = so.FindProperty(field);
            if (list.arraySize <= index) list.arraySize = index + 1;
            var row = list.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("item").objectReferenceValue = item;
            row.FindPropertyRelative("min").intValue = min;
            row.FindPropertyRelative("max").intValue = max;
            row.FindPropertyRelative("chancePercent").floatValue = chance;
        }
        static void Edit(ContentDefinition definition, string field, object value) => TestContentFactory.Edit(definition, so =>
        {
            if (value is long number) so.FindProperty(field).longValue = number;
            else TestContentFactory.Set(so, field, value);
        });
        static ValidationReport Local(ContentDefinition definition) => TestContentFactory.Validate(definition);
        ValidationReport Global(params ContentDefinition[] additions)
        {
            var definitions = new List<ContentDefinition> { fuel, scrap, hull, angled, missingItem, missingBlock, cue };
            definitions.AddRange(additions);
            var report = new ValidationReport("Economy fixtures");
            var catalog = ContentCatalog.Build(definitions, report);
            Assert.AreEqual(0, report.Entries.Count, report.ToText());
            new EconomyContentValidator().Validate(catalog, report);
            return report;
        }
        static void Clean(ValidationReport report) => Assert.AreEqual(0, report.Entries.Count, report.ToText());
        static void Only(ValidationReport report, string code, ValidationSeverity severity = ValidationSeverity.Error)
        {
            Assert.AreEqual(1, report.Entries.Count, report.ToText());
            Assert.AreEqual(code, report.Entries[0].Code);
            Assert.AreEqual(severity, report.Entries[0].Severity);
            Assert.IsNotEmpty(report.Entries[0].AssetPath);
            Assert.IsNotEmpty(report.Entries[0].Message);
        }

        [Test] public void E001_Fails()
        {
            foreach (var failure in new (string Field, object Value)[]
            {
                ("entries.Array.data[0].item", null), ("entries.Array.data[0].min", 0),
                ("entries.Array.data[0].max", 0), ("entries.Array.data[0].max", 10001),
                ("entries.Array.data[0].chancePercent", 0f), ("entries.Array.data[0].chancePercent", 101f),
                ("entries.Array.data[0].chancePercent", float.NaN), ("entries.Array.data[0].chancePercent", float.PositiveInfinity),
                ("defaultPicks", -1), ("defaultPicks", 21)
            })
            { var loot = Loot(); Edit(loot, failure.Field, failure.Value); Only(Local(loot), "ECO-E001"); }
        }
        [Test] public void E001_Passes()
        {
            var loot = Loot(); Clean(Local(loot));
            TestContentFactory.Edit(loot, so => LootRow(so, "entries", 0, fuel, 1, 10000, 100));
            Edit(loot, "defaultPicks", 20); Clean(Local(loot));
            TestContentFactory.Edit(loot, so => so.FindProperty("entries").arraySize = 0);
            Edit(loot, "defaultPicks", 0); Clean(Local(loot));
        }

        [Test] public void E002_Fails()
        {
            foreach (var failure in new (string Field, object Value)[]
            {
                ("stock.Array.data[0].item", null), ("stock.Array.data[0].count", 0), ("stock.Array.data[0].count", -2),
                ("stock.Array.data[0].priceFactor", -1f), ("stock.Array.data[0].priceFactor", float.NaN),
                ("stock.Array.data[0].priceFactor", float.PositiveInfinity), ("sellPriceFactor", .09f),
                ("buyPriceFactor", -1f), ("buyPriceFactor", 1.1f), ("restockHours", -1f), ("restockHours", float.NaN)
            })
            { var merchant = Merchant(); Edit(merchant, failure.Field, failure.Value); Only(Local(merchant), "ECO-E002"); }
            var duplicate = Merchant();
            TestContentFactory.Edit(duplicate, so =>
            {
                var stock = so.FindProperty("stock"); stock.arraySize = 2;
                var row = stock.GetArrayElementAtIndex(1);
                row.FindPropertyRelative("item").objectReferenceValue = fuel;
                row.FindPropertyRelative("count").intValue = 5;
                row.FindPropertyRelative("priceFactor").floatValue = 1;
            });
            Only(Local(duplicate), "ECO-E002");
        }
        [Test] public void E002_Passes()
        {
            var merchant = Merchant(); Clean(Local(merchant));
            Edit(merchant, "stock.Array.data[0].count", -1);
            Edit(merchant, "stock.Array.data[0].priceFactor", 0f);
            Edit(merchant, "sellPriceFactor", .1f); Edit(merchant, "buyPriceFactor", 1f); Edit(merchant, "restockHours", 0f);
            Clean(Local(merchant));
            TestContentFactory.Edit(merchant, so => so.FindProperty("stock").arraySize = 0); Clean(Local(merchant));
        }

        AuthoredStarSystemDefinition SystemWithBody(BodyTemplateDefinition template) => TestContentFactory.Create<AuthoredStarSystemDefinition>("starsys_test", so =>
        {
            var bodies = so.FindProperty("bodies"); bodies.arraySize = 1;
            var body = bodies.GetArrayElementAtIndex(0);
            body.FindPropertyRelative("key").stringValue = "station";
            body.FindPropertyRelative("template").objectReferenceValue = template;
        });
        static void Placement(MerchantDefinition merchant, AuthoredStarSystemDefinition system, string key) => TestContentFactory.Edit(merchant, so =>
        {
            var list = so.FindProperty("authoredBodies"); list.arraySize = 1;
            var row = list.GetArrayElementAtIndex(0);
            row.FindPropertyRelative("starSystem").objectReferenceValue = system;
            row.FindPropertyRelative("bodyKey").stringValue = key;
        });
        static void TemplatePlacement(MerchantDefinition merchant, BodyTemplateDefinition body) => TestContentFactory.Edit(merchant, so =>
        {
            var list = so.FindProperty("bodyTemplates"); list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = body;
        });
        [Test] public void E003_Fails()
        {
            var merchant = Merchant();
            Placement(merchant, null, "station"); Only(Local(merchant), "ECO-E003");
            var system = SystemWithBody(null);
            foreach (string key in new[] { "missing", "Station", "" })
            { Placement(merchant, system, key); Only(Local(merchant), "ECO-E003"); }
        }
        [Test] public void E003_Passes()
        {
            var merchant = Merchant(); Clean(Local(merchant));
            Placement(merchant, SystemWithBody(null), "station"); Clean(Local(merchant));
        }

        [Test] public void E004_Fails()
        {
            var config = Config();
            var body = TestContentFactory.Create<BodyTemplateDefinition>("body_station");
            var system = SystemWithBody(body);
            var first = Merchant("merchant_a"); var second = Merchant("merchant_b");
            TemplatePlacement(first, body); TemplatePlacement(second, body);
            Only(Global(config, body, system, first, second), "ECO-E004");
            TestContentFactory.Edit(first, so => so.FindProperty("bodyTemplates").arraySize = 0);
            TestContentFactory.Edit(second, so => so.FindProperty("bodyTemplates").arraySize = 0);
            Placement(first, system, "station"); Placement(second, system, "station");
            Only(Global(config, body, system, first, second), "ECO-E004");
        }
        [Test] public void E004_Passes()
        {
            var config = Config();
            var body = TestContentFactory.Create<BodyTemplateDefinition>("body_station");
            var system = SystemWithBody(body);
            var first = Merchant("merchant_a"); var second = Merchant("merchant_b");
            TemplatePlacement(first, body); Placement(second, system, "station");
            // A specific authored merchant overrides a template merchant; duplicate rows for one owner are not two owners.
            TestContentFactory.Edit(first, so =>
            {
                var list = so.FindProperty("bodyTemplates"); list.arraySize = 2;
                list.GetArrayElementAtIndex(1).objectReferenceValue = body;
            });
            TestContentFactory.Edit(second, so =>
            {
                var list = so.FindProperty("authoredBodies"); list.arraySize = 2;
                var row = list.GetArrayElementAtIndex(1);
                row.FindPropertyRelative("starSystem").objectReferenceValue = system;
                row.FindPropertyRelative("bodyKey").stringValue = "station";
            });
            Clean(Global(config, body, system, first, second));
        }

        [Test] public void E005_Fails()
        {
            Only(Global(), "ECO-E005");
            Only(Global(Config("config_wrong")), "ECO-E005");
            Only(Global(Config(), Config("config_extra")), "ECO-E005");
            foreach (var failure in new (string Field, object Value)[]
            {
                ("baseCargoSlots", 0), ("baseCargoSlots", 201), ("unitsPerSlot", 0f), ("unitsPerSlot", float.NaN),
                ("startCredits", -1L), ("relationPriceSlope", -1f), ("relationPriceSlope", float.NaN),
                ("salvageFunctionalChance", -1f), ("salvageFunctionalChance", 1.1f), ("salvageFunctionalChance", float.NaN),
                ("salvageStructuralChance", -1f), ("salvageStructuralChance", 1.1f), ("salvageStructuralChance", float.PositiveInfinity),
                ("salvageMaxStructuralItems", 0), ("scrapItem", null), ("scrapPerCell", -1f), ("scrapPerCell", float.NaN),
                ("salvageMaxScrap", 0), ("maxSalvageCells", 0), ("salvageDistanceM", 0f), ("salvageDistanceM", float.NaN), ("salvageCue", null),
                ("startItems.Array.data[0].item", null), ("startItems.Array.data[0].min", 0), ("startItems.Array.data[0].max", 0),
                ("startItems.Array.data[0].chancePercent", 0f)
            })
            { var config = Config(); Edit(config, failure.Field, failure.Value); Only(Local(config), "ECO-E005"); }
        }
        [Test] public void E005_Passes()
        {
            var config = Config(); Clean(Local(config)); Clean(Global(config));
            Assert.AreEqual(20, config.BaseCargoSlots); Assert.AreEqual(10f, config.UnitsPerSlot);
            Assert.AreEqual(2000L, config.StartCredits); Assert.AreEqual(.002f, config.RelationPriceSlope);
            Assert.AreEqual(.5f, config.SalvageFunctionalChance); Assert.AreEqual(.1f, config.SalvageStructuralChance);
            Assert.AreEqual(50, config.SalvageMaxStructuralItems); Assert.AreEqual(.02f, config.ScrapPerCell);
            Assert.AreEqual(500, config.SalvageMaxScrap); Assert.AreEqual(20000, config.MaxSalvageCells); Assert.AreEqual(120f, config.SalvageDistanceM);
            Assert.AreSame(scrap, config.ScrapItem); Assert.AreSame(cue, config.SalvageCue);
            CollectionAssert.AreEqual(new[] { "resource_fuel", "block_hull", "block_hull_angled" }, config.StartItems.Select(e => e.item.IdString));
            CollectionAssert.AreEqual(new[] { 50, 40, 20 }, config.StartItems.Select(e => e.min));
            Assert.IsTrue(config.StartItems.All(e => e.min == e.max && e.chancePercent == 100));
            Edit(config, "baseCargoSlots", 1); Edit(config, "unitsPerSlot", .01f); Edit(config, "startCredits", 0L);
            Edit(config, "salvageFunctionalChance", 0f); Edit(config, "salvageStructuralChance", 1f); Edit(config, "scrapPerCell", 0f);
            Clean(Local(config)); Edit(config, "baseCargoSlots", 200); Clean(Local(config));
        }

        ItemDefinition Quest(bool tradeable) => TestContentFactory.Create<ItemDefinition>("item_quest", so =>
        {
            TestContentFactory.Set(so, "category", (int)ItemCategory.Quest);
            TestContentFactory.Set(so, "tradeable", tradeable);
        });
        [Test] public void W001_Fails() => Only(Global(Config(), Quest(true)), "ECO-W001", ValidationSeverity.Warning);
        [Test] public void W001_Passes() => Clean(Global(Config(), Quest(false)));
        [Test] public void W002_Fails()
        {
            Edit(fuel, "basePrice", 0); Only(Local(Merchant()), "ECO-W002", ValidationSeverity.Warning);
        }
        [Test] public void W002_Passes() => Clean(Local(Merchant()));
        [Test] public void QuestItems_NotTradeable()
        {
            Assert.IsFalse(Quest(true).Tradeable); Assert.IsFalse(Quest(false).Tradeable);
            Assert.IsTrue(fuel.Tradeable); Edit(fuel, "tradeable", false); Assert.IsFalse(fuel.Tradeable);
        }
    }
}
