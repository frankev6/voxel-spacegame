using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Dto;
using Game.Voxel.Data;
using Unity.Collections;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §5.4: verified VOXS assets and cached summaries for the 13 legacy ships.</summary>
    public sealed class ShipConverter : ILegacyConverter
    {
        public string Name => "Ships";
        public int Order => 300;
        public void Convert(LegacyMigrationContext context)
        {
            var names = VoxsBlockNames.FromCatalog(context.Catalog);
            foreach (var file in context.Index.OfType("ShipItem"))
            {
                var dto = LegacyJson.Read<LegacyShipItemDto>(file, context.Report); if (dto == null) continue;
                if (!context.IdMap.TryMap(LegacyIdKind.Ship, dto.RegistryKey, out string id) || id == null)
                { context.Report.Error("MIG-E003", file.RelativePath, "RegistryKey", "Unknown ship key: " + dto.RegistryKey); continue; }
                try
                {
                    using var built = LegacyShipBuilder.Build(dto, key => Resolve(context, key), context.Report, file.RelativePath);
                    byte[] bytes = VoxsCodec.Encode(built.Source, names, VoxsEncodeOptions.ForAsset);
                    if (!VoxsCodec.TryDecode(bytes, context.Catalog, Allocator.TempJob, out var decoded, out string error))
                    { context.Report.Error("MIG-E054", file.RelativePath, "payload", error); continue; }
                    using (decoded)
                        if (!Matches(built.Source, decoded))
                        { context.Report.Error("MIG-E054", file.RelativePath, "payload", "Decoded arrays differ from the imported structure."); continue; }
                    context.Writer.CreateOrUpdate<ShipBlueprintDefinition>("Ships", id, so =>
                    {
                        context.Writer.Set(so, "displayName", p => p.stringValue = dto.ItemName);
                        context.Writer.Set(so, "cellSize", p => p.floatValue = built.Source.CellSize);
                        context.Writer.Set(so, "payloadVersion", p => p.intValue = VoxsCodec.Version);
                        context.Writer.Set(so, "payload", p => { p.arraySize = bytes.Length; for (int i = 0; i < bytes.Length; i++) p.GetArrayElementAtIndex(i).intValue = bytes[i]; });
                        context.Writer.Set(so, "cellCount", p => p.intValue = built.Statistics.Cells);
                        context.Writer.Set(so, "boundsMin", p => p.vector3IntValue = new Vector3Int(built.BoundsMin.x, built.BoundsMin.y, built.BoundsMin.z));
                        context.Writer.Set(so, "boundsMax", p => p.vector3IntValue = new Vector3Int(built.BoundsMax.x, built.BoundsMax.y, built.BoundsMax.z));
                    }, out _);
                    var row = built.Statistics; row.Ship = id; context.Ships.Add(row);
                    context.Report.Info("MIG-I091", file.RelativePath, "", "Dropped: Thumbnail, ItemImage, AbilityIds, OfficerIds, GlobalStats, width, height, length, moduleSlots, Category, Rarity, isStackable, EffectType, GrantsAbilities.");
                    context.Report.Info("MIG-I090", file.RelativePath, "Description/BasePrice", "ShipBlueprintDefinition has no description or price fields; both dropped.");
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Ship conversion failed: " + e.Message); }
            }
        }
        internal static (BlockDefinition Definition, ushort Index) Resolve(LegacyMigrationContext context, string key)
        {
            if (!context.IdMap.TryMap(LegacyIdKind.Block, key, out string id) || id == null || !context.Catalog.TryGet<BlockDefinition>(new ContentId(id), out var def)) return default;
            return (def, context.Catalog.GetBlockIndex(def));
        }
        internal static bool Matches(in VoxsSource source, VoxsDecoded decoded)
        {
            if (source.CellSize != decoded.CellSize || source.AnchorInstance != decoded.AnchorInstance || decoded.Partial || decoded.DriftWarningCount != 0 ||
                source.Palette.Length != decoded.Palette.Length || source.Instances.Length != decoded.Instances.Length || source.BrickCoords.Length != decoded.BrickCoords.Length || source.Cells.Length != decoded.Cells.Length) return false;
            for (int i = 0; i < source.Palette.Length; i++) if (!source.Palette[i].Equals(decoded.Palette[i])) return false;
            for (int i = 0; i < source.Instances.Length; i++) if (!source.Instances[i].Equals(decoded.Instances[i])) return false;
            for (int i = 0; i < source.BrickCoords.Length; i++) if (!source.BrickCoords[i].Equals(decoded.BrickCoords[i])) return false;
            for (int i = 0; i < source.Cells.Length; i++) if (!source.Cells[i].Equals(decoded.Cells[i])) return false;
            return true;
        }
    }
}
