using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Editor.Migration.Dto;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §4.6 / Q40–41: ordered loot rows, exact export references and protected asset writes.</summary>
    public sealed class LootTableConverter : ILegacyConverter
    {
        public string Name => "Loot";
        public int Order => 410;

        public void Convert(LegacyMigrationContext context)
        {
            foreach (var file in context.Index.OfType("LootTable"))
            {
                var dto = LegacyJson.Read<LegacyLootTableDto>(file, context.Report);
                if (dto == null || !Map(context, file, LegacyIdKind.LootTable, dto.Name, "$name", out string id)) continue;
                if (id == null)
                { context.Report.Info("MIG-I091", file.RelativePath, "", "Loot table is explicitly not converted: " + dto.Name); continue; }
                if (dto.LootItems == null)
                { context.Report.Error("MIG-E002", file.RelativePath, "lootItems", "Loot rows are missing or null; table skipped."); continue; }

                var entries = new List<LootEntry>();
                bool valid = true;
                for (int i = 0; i < dto.LootItems.Length; i++)
                {
                    var row = dto.LootItems[i]; string field = "lootItems[" + i + "]";
                    if (row == null || row.Item.IsNull)
                    { context.Report.Warning("MIG-W071", file.RelativePath, field + ".item", "Null legacy loot item dropped; no replacement item is inferred."); continue; }
                    if (row.MinAmount < 1 || row.MaxAmount < row.MinAmount || row.MaxAmount > 10000 ||
                        double.IsNaN(row.DropChance) || double.IsInfinity(row.DropChance) || row.DropChance <= 0 || row.DropChance > 100 || (float)row.DropChance <= 0)
                    { context.Report.Error("MIG-E072", file.RelativePath, field, "Loot amounts/chance cannot satisfy SPEC-50 without retuning; table skipped."); valid = false; continue; }
                    if (!context.Index.TryResolve(row.Item, out var target))
                    { context.Report.Error("MIG-E004", file.RelativePath, field + ".item", "Loot reference does not resolve in the export: " + row.Item.Path); valid = false; continue; }
                    var itemDto = ReadItem(context, target);
                    if (itemDto == null) { valid = false; continue; }
                    var kind = itemDto is LegacyShipItemDto ? LegacyIdKind.Ship : itemDto is LegacyBlockItemDto ? LegacyIdKind.Block : LegacyIdKind.Item;
                    if (!Map(context, target, kind, itemDto.RegistryKey, "RegistryKey", out string itemId)) { valid = false; continue; }
                    if (kind == LegacyIdKind.Ship)
                    {
                        context.Report.Warning("MIG-W070", file.RelativePath, field + ".item", "Ship loot entry dropped: SPEC-50 LootEntry holds only ItemDefinition: " + itemId);
                        continue;
                    }
                    if (itemId == null)
                    { context.Report.Info("MIG-I091", file.RelativePath, field + ".item", "Item is explicitly not converted: " + itemDto.RegistryKey); continue; }
                    if (context.Catalog == null || !context.Catalog.TryGet<ItemDefinition>(new ContentId(itemId), out var item))
                    { context.Report.Error("MIG-E004", file.RelativePath, field + ".item", "Mapped loot item is unavailable in the current catalog: " + itemId); valid = false; continue; }
                    entries.Add(new LootEntry { item = item, min = row.MinAmount, max = row.MaxAmount, chancePercent = (float)row.DropChance });
                }
                // Unexpected input or unavailable prerequisites must not replace an existing table with a partial result.
                if (!valid) continue;
                try
                {
                    var writer = context.Writer;
                    writer.CreateOrUpdate<LootTableDefinition>("Loot", id, so =>
                    {
                        writer.Set(so, "defaultPicks", p => p.intValue = 0);
                        writer.Set(so, "entries", p =>
                        {
                            p.arraySize = entries.Count;
                            for (int i = 0; i < entries.Count; i++)
                            {
                                var entry = entries[i]; var element = p.GetArrayElementAtIndex(i);
                                element.FindPropertyRelative("item").objectReferenceValue = entry.item;
                                element.FindPropertyRelative("min").intValue = entry.min;
                                element.FindPropertyRelative("max").intValue = entry.max;
                                element.FindPropertyRelative("chancePercent").floatValue = entry.chancePercent;
                            }
                        });
                    }, out _);
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Loot conversion failed: " + e.Message); }
            }

            foreach (var file in context.Index.OfType("ShipTableSO"))
            {
                var dto = LegacyJson.Read<LegacyShipTableDto>(file, context.Report);
                if (dto == null || !Map(context, file, LegacyIdKind.LootTable, dto.Name, "$name", out string id)) continue;
                if (id != null)
                { context.Report.Error("MIG-E004", file.RelativePath, "$type", "ShipTableSO cannot become a SPEC-50 loot table."); continue; }
                context.Report.Info("MIG-I091", file.RelativePath, "ships", "Ship table intentionally not converted: " + dto.Name + " (" + (dto.Ships?.Length ?? 0) + " legacy ship references). SPEC-01/50 own new-game loadouts.");
            }
        }

        static bool Map(LegacyMigrationContext context, LegacyFile file, LegacyIdKind kind, string key, string field, out string id)
        {
            if (context.IdMap.TryMap(kind, key, out id)) return true;
            context.Report.Error("MIG-E003", file.RelativePath, field, "Legacy key is not mapped: " + key); return false;
        }

        static LegacyItemDto ReadItem(LegacyMigrationContext context, LegacyFile file)
        {
            switch (file.Type)
            {
                case "ResourceItem": return LegacyJson.Read<LegacyItemDto>(file, context.Report);
                case "SlottableItem": return LegacyJson.Read<LegacySlottableItemDto>(file, context.Report);
                case "PaintBucketItem": return LegacyJson.Read<LegacyPaintBucketItemDto>(file, context.Report);
                case "BlockItem": return LegacyJson.Read<LegacyBlockItemDto>(file, context.Report);
                case "CoreBlockItem": return LegacyJson.Read<LegacyCoreBlockItemDto>(file, context.Report);
                case "ShieldBlockItem": return LegacyJson.Read<LegacyShieldBlockItemDto>(file, context.Report);
                case "PortalBlockItem": return LegacyJson.Read<LegacyPortalBlockItemDto>(file, context.Report);
                case "WeaponBlockItem": return LegacyJson.Read<LegacyWeaponBlockItemDto>(file, context.Report);
                case "MirrorBlockItem": return LegacyJson.Read<LegacyMirrorBlockItemDto>(file, context.Report);
                case "ShipItem": return LegacyJson.Read<LegacyShipItemDto>(file, context.Report);
                default:
                    context.Report.Error("MIG-E004", file.RelativePath, "$type", "Loot target is not an exported item type: " + file.Type); return null;
            }
        }
    }
}
