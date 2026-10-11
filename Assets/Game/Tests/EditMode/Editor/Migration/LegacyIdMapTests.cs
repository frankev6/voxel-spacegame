using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-003: LegacyIdMap.Default against SPEC-72 §4.4 and the legacy export.</summary>
    public sealed class LegacyIdMapTests
    {
        static readonly LegacyIdMap Map = LegacyIdMap.Default;

        // Top-level string values read with a regex: the test assembly does not reference Newtonsoft.Json, and these keys are unique per file.
        static IEnumerable<string> Values(string json, string key)
            => Regex.Matches(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>().Select(m => m.Groups[1].Value);

        [Test]
        public void EveryExportKey_IsMapped()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "..", "Import", "Legacy", "content");
            var missing = new List<string>();
            int checkedKeys = 0;
            void Expect(LegacyIdKind kind, string key, string file)
            {
                if (string.IsNullOrEmpty(key)) return;          // blank entityId / encounterId / systemId fields in the export
                checkedKeys++;
                if (!Map.TryMap(kind, key, out _)) missing.Add($"{kind} '{key}' ({file})");
            }
            foreach (var path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string json = File.ReadAllText(path), file = Path.GetFileName(path);
                string type = Values(json, "$type").FirstOrDefault() ?? "";
                string name = Values(json, "$name").FirstOrDefault();
                foreach (var key in Values(json, "RegistryKey"))
                {
                    checkedKeys++;
                    if (!Map.TryMapItemToken(key, out _, out _)) missing.Add($"item token '{key}' ({file})");
                    if (type == "WeaponBlockItem") Expect(LegacyIdKind.Weapon, key, file);
                }
                switch (type)
                {
                    case "FactionData": Expect(LegacyIdKind.Faction, Values(json, "id").First(), file); break;
                    case "ShieldToggleAbilitySO": case "BoostAbilitySO":
                        Expect(LegacyIdKind.Ability, Values(json, "<ID>k__BackingField").First(), file); Expect(LegacyIdKind.Ability, name, file); break;
                    case "AudioLibrary":
                        foreach (var id in Values(json, "ID").Concat(Values(json, "Track"))) Expect(LegacyIdKind.AudioCue, id, file);
                        break;
                    case "LootTable": case "ShipTableSO": Expect(LegacyIdKind.LootTable, name, file); break;
                    case "SolarSystemEntity": case "PlanetEntity": case "StarEntity": case "Assets.Game.Core.Galaxy_Map.HyperlaneEntity":
                        Expect(LegacyIdKind.Body, name, file); Expect(LegacyIdKind.Body, Values(json, "entityId").First(), file); break;
                    case "NPCFleetEntity": Expect(LegacyIdKind.Encounter, Values(json, "entityId").First(), file); break;
                    case "CombatEncounter": Expect(LegacyIdKind.Encounter, name, file); break;
                    case "SolarSystemData": Expect(LegacyIdKind.StarSystem, Values(json, "systemId").First(), file); break;
                }
            }
            CollectionAssert.IsEmpty(missing, "legacy keys without a §4.4 row");
            Assert.Greater(checkedKeys, 120, "expected the whole export");
        }

        [Test]
        public void V2Ids_AreValidAndUnique()
        {
            var prefixes = new Dictionary<LegacyIdKind, string[]>
            {
                { LegacyIdKind.Weapon, new[] { "wpn_" } }, { LegacyIdKind.Faction, new[] { "faction_" } }, { LegacyIdKind.Ability, new[] { "ability_" } },
                { LegacyIdKind.AudioCue, new[] { "cue_" } }, { LegacyIdKind.LootTable, new[] { "loot_" } }, { LegacyIdKind.Body, new[] { "body_" } },
                { LegacyIdKind.Encounter, new[] { "encounter_" } }, { LegacyIdKind.StarSystem, new[] { "starsys_" } },
            };
            var types = new Dictionary<LegacyIdKind, Type>
            { { LegacyIdKind.Block, typeof(BlockDefinition) }, { LegacyIdKind.Item, typeof(ItemDefinition) }, { LegacyIdKind.Ship, typeof(ShipBlueprintDefinition) } };
            foreach (var r in Map.All.Where(r => r.V2Id != null))
            {
                Assert.IsTrue(ContentIdRules.IsValidFormat(r.V2Id), $"{r.Kind} {r.LegacyKey} → '{r.V2Id}' format");
                if (types.TryGetValue(r.Kind, out var t)) Assert.IsTrue(ContentIdRules.HasAllowedPrefix(t, r.V2Id, out _), $"{r.Kind} {r.LegacyKey} → '{r.V2Id}' prefix");
                else Assert.IsTrue(prefixes[r.Kind].Any(p => r.V2Id.StartsWith(p, StringComparison.Ordinal)), $"{r.Kind} {r.LegacyKey} → '{r.V2Id}' prefix");
            }
            foreach (var g in Map.All.Where(r => r.V2Id != null).GroupBy(r => r.V2Id).Where(g => g.Count() > 1))
            {
                var primaries = g.Where(r => r.Note == null || !r.Note.StartsWith("alias of", StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(1, primaries.Length, $"'{g.Key}' is the target of {string.Join(", ", g.Select(r => r.Kind + " " + r.LegacyKey))}; all but one must be marked as aliases");
                Assert.IsTrue(g.All(r => r.Kind == primaries[0].Kind), $"'{g.Key}' is shared across kinds");
            }
            Assert.IsTrue(Map.TryMap(LegacyIdKind.Faction, "faction_4", out var none)); Assert.IsNull(none, "not converted maps to null");
            Assert.IsFalse(Map.TryMap(LegacyIdKind.Faction, "faction_unknown", out _), "unknown key");
            Assert.IsTrue(Map.TryMap(LegacyIdKind.Weapon, "weapon_railgun_medium", out var wpn)); Assert.AreEqual("wpn_railgun_medium", wpn);
            Assert.AreEqual(9, Map.All.Count(r => r.Kind == LegacyIdKind.Weapon));
            Assert.AreEqual(17, Map.All.Count(r => r.Kind == LegacyIdKind.Item && r.LegacyKey.StartsWith("PaintBucket_", StringComparison.Ordinal)));
        }

        [Test]
        public void ItemTokens_Numeric_And_Ship()
        {
            Assert.IsTrue(Map.TryMapItemToken("500", out var id, out var kind)); Assert.AreEqual("resource_palladium", id); Assert.AreEqual(LegacyIdKind.Item, kind);
            Assert.IsTrue(Map.TryMapItemToken("717185106", out id, out kind)); Assert.AreEqual("item_strange_matter_container_full", id); Assert.AreEqual(LegacyIdKind.Item, kind);
            Assert.IsTrue(Map.TryMapItemToken(" ship_fighter ", out id, out kind)); Assert.AreEqual("ship_fighter", id); Assert.AreEqual(LegacyIdKind.Ship, kind);
            Assert.IsTrue(Map.TryMapItemToken("block_hull", out id, out kind)); Assert.AreEqual(LegacyIdKind.Block, kind);
            Assert.IsTrue(Map.TryMapItemToken("PaintBucket_13_SteelBlue", out id, out _)); Assert.AreEqual("paint_13_steel_blue", id);
            Assert.IsFalse(Map.TryMapItemToken("no_such_thing", out id, out _)); Assert.IsNull(id);
            Assert.IsFalse(Map.TryMapItemToken(null, out _, out _));
        }
    }
}
