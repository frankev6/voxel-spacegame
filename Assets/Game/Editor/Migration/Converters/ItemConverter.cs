using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Editor.Migration.Dto;
using UnityEditor;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §4.5: imports the 35 non-block items, preserving text and reporting intentionally dropped behaviour.</summary>
    public sealed class ItemConverter : ILegacyConverter
    {
        public string Name => "Items";
        public int Order => 100;
        public void Convert(LegacyMigrationContext context)
        {
            var sources = AssetDatabase.LoadAssetAtPath<LegacyMigrationSources>(LegacyMigrationSources.AssetPath);
            if (sources == null) { context.Report.Error("MIG-E004", LegacyMigrationSources.AssetPath, "", "Migration source references are missing."); return; }
            var icons = sources.LoadIcons(context.Report);
            foreach (var file in context.Index.Files)
            {
                LegacyItemDto dto;
                switch (file.Type)
                {
                    case "ResourceItem": dto = LegacyJson.Read<LegacyItemDto>(file, context.Report); break;
                    case "SlottableItem": dto = LegacyJson.Read<LegacySlottableItemDto>(file, context.Report); break;
                    case "PaintBucketItem": dto = LegacyJson.Read<LegacyPaintBucketItemDto>(file, context.Report); break;
                    default: continue;
                }
                if (dto == null) continue;
                if (!context.IdMap.TryMap(LegacyIdKind.Item, dto.RegistryKey, out string id) || id == null)
                { context.Report.Error("MIG-E003", file.RelativePath, "RegistryKey", "Legacy item key is not mapped: " + dto.RegistryKey); continue; }
                if (!NamedEnum(dto.Category, out ItemCategory category) || !NamedEnum(dto.Rarity, out ItemRarity rarity))
                { context.Report.Error("MIG-E021", file.RelativePath, "Category/Rarity", "Unknown item category or rarity name."); continue; }
                var dropped = new List<string>();
                if (dto is LegacyPaintBucketItemDto paint) { category = ItemCategory.Paint; dropped.Add("PaletteIndex " + paint.PaletteIndex); }
                else if (id.StartsWith("module_", StringComparison.Ordinal)) category = ItemCategory.Module;
                else if (id == "item_strange_matter" || id == "item_strange_matter_container_empty" || id == "item_strange_matter_container_full") category = ItemCategory.Quest;
                if (category.ToString() != dto.Category) dropped.Add("legacy Category " + dto.Category);
                if (!string.IsNullOrEmpty(dto.EffectType) && dto.EffectType != "None") dropped.Add("EffectType " + dto.EffectType);
                if (dto is LegacySlottableItemDto slottable)
                {
                    if (!string.IsNullOrEmpty(slottable.RequiredSocketType)) dropped.Add("RequiredSocketType " + slottable.RequiredSocketType);
                    if (slottable.Modifiers?.Length > 0) dropped.Add(slottable.Modifiers.Length + " Modifiers");
                }
                if (dto.GrantsAbilities?.Length > 0) dropped.Add("GrantsAbilities (non-block item behaviour is inert)");
                if (dto.ItemImage.IsNull || string.IsNullOrEmpty(dto.ItemImage.Name) || !icons.TryGetValue(dto.ItemImage.Name, out var icon))
                { context.Report.Error("MIG-E004", file.RelativePath, "ItemImage", "Icon sprite not found in configured sheet: " + dto.ItemImage.Name); continue; }
                try
                {
                    context.Writer.CreateOrUpdate<ItemDefinition>("Items", id, so =>
                    {
                        var writer = context.Writer;
                        writer.Set(so, "displayName", p => p.stringValue = dto.ItemName);
                        writer.Set(so, "description", p => p.stringValue = dto.Description);
                        writer.Set(so, "category", p => p.intValue = (int)category);
                        writer.Set(so, "rarity", p => p.intValue = (int)rarity);
                        writer.Set(so, "basePrice", p => p.intValue = dto.BasePrice);
                        writer.Set(so, "maxStack", p => p.intValue = dto.IsStackable ? 999 : 1);
                        writer.Set(so, "icon", p => p.objectReferenceValue = icon);
                    }, out _);
                    if (dropped.Count > 0) context.Report.Info("MIG-I091", file.RelativePath, "", "Dropped: " + string.Join(", ", dropped) + ".");
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Item conversion failed: " + e.Message); }
            }
        }
        static bool NamedEnum<T>(string name, out T value) where T : struct, Enum
            => Enum.TryParse(name, false, out value) && Enum.IsDefined(typeof(T), value) && value.ToString() == name;
    }
}
