using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Dto;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §5.3: validate exported geometry against SPEC-04 before writing the 21 legacy blocks.</summary>
    public sealed class BlockConverter : ILegacyConverter
    {
        public string Name => "Blocks";
        public int Order => 110;
        public void Convert(LegacyMigrationContext context)
        {
            var sources = AssetDatabase.LoadAssetAtPath<LegacyMigrationSources>(LegacyMigrationSources.AssetPath);
            if (sources == null) { context.Report.Error("MIG-E031", LegacyMigrationSources.AssetPath, "", "Migration source references are missing."); return; }
            var meshes = sources.LoadMeshes(context.Report); var icons = sources.LoadIcons(context.Report);
            foreach (var file in context.Index.Files)
            {
                LegacyBlockItemDto dto = Read(file, context.Report);
                if (dto == null) continue;
                if (!context.IdMap.TryMap(LegacyIdKind.Block, dto.RegistryKey, out string id) || id == null || !LegacyBlockValues.All.TryGetValue(id, out var row))
                { context.Report.Error("MIG-E003", file.RelativePath, "RegistryKey", "Legacy block key is not mapped: " + dto.RegistryKey); continue; }
                try
                {
                    var cells = Footprint(dto);
                    var clearance = new List<Vector3Int>();
                    foreach (var coordinates in dto.ClearanceDirections ?? Array.Empty<int[]>())
                    {
                        var cell = Cell(coordinates);
                        if (cells.Contains(cell)) context.Report.Warning("MIG-W033", file.RelativePath, "ClearanceDirections", "Dropped footprint cell " + cell + ".");
                        else if (id == "block_portal_small" && cell == new Vector3Int(1, 0, 0)) context.Report.Warning("MIG-W034", file.RelativePath, "ClearanceDirections", "Dropped (1,0,0) to keep the portal achiral.");
                        else clearance.Add(cell);
                    }
                    if (!SameCells(cells, row.Cells) || !SameCells(clearance, row.Clearance))
                    { context.Report.Error("MIG-E030", file.RelativePath, "Cells/Clearance", "Export geometry differs from SPEC-04 §4.10."); continue; }
                    Mesh mesh = null;
                    if (row.Kind != BlockKind.Structural)
                    {
                        string meshKey = Path.GetFileNameWithoutExtension(dto.BlockMesh.Path) + "/" + dto.BlockMesh.Name;
                        if (meshKey != row.Mesh || !meshes.TryGetValue(meshKey, out mesh))
                        { context.Report.Error("MIG-E031", file.RelativePath, "BlockMesh", "Expected configured mesh " + row.Mesh + ", export references " + meshKey + "."); continue; }
                        Vector3 min = cells[0], max = cells[0];
                        foreach (var cell in cells) { min = Vector3.Min(min, cell); max = Vector3.Max(max, cell); }
                        min -= Vector3.one * .5f; max += Vector3.one * .5f;
                        if (FaceDifference(mesh.bounds.min, min) > .6f || FaceDifference(mesh.bounds.max, max) > .6f)
                            context.Report.Warning("MIG-W032", file.RelativePath, "BlockMesh", "Mesh bounds differ from the footprint by more than 0.6 cell.");
                    }
                    if (dto.ItemImage.IsNull || !icons.TryGetValue(dto.ItemImage.Name ?? "", out var icon))
                    { context.Report.Error("MIG-E004", file.RelativePath, "ItemImage", "Icon sprite not found: " + dto.ItemImage.Name); continue; }
                    if (!Enum.TryParse(dto.Rarity, false, out ItemRarity rarity) || !Enum.IsDefined(typeof(ItemRarity), rarity) || rarity.ToString() != dto.Rarity)
                    { context.Report.Error("MIG-E021", file.RelativePath, "Rarity", "Unknown rarity name."); continue; }
                    context.Writer.CreateOrUpdate<BlockDefinition>("Blocks", id, so =>
                    {
                        var writer = context.Writer;
                        writer.Set(so, "displayName", p => p.stringValue = dto.ItemName);
                        writer.Set(so, "description", p => p.stringValue = dto.Description);
                        writer.Set(so, "category", p => p.intValue = (int)ItemCategory.Block);
                        writer.Set(so, "rarity", p => p.intValue = (int)rarity);
                        writer.Set(so, "basePrice", p => p.intValue = dto.BasePrice);
                        writer.Set(so, "maxStack", p => p.intValue = 999);
                        writer.Set(so, "icon", p => p.objectReferenceValue = icon);
                        writer.Set(so, "kind", p => p.intValue = (int)row.Kind);
                        writer.Set(so, "shape", p => p.intValue = (int)row.Shape);
                        writer.Set(so, "cells", p => SetCells(p, row.Cells));
                        writer.Set(so, "clearance", p => SetCells(p, row.Clearance));
                        writer.Set(so, "allowedOrientations.Bits", p => p.longValue = row.Orientations);
                        writer.Set(so, "attachFaces", p => p.intValue = (int)row.Attach);
                        writer.Set(so, "mirrorPartner", p => p.objectReferenceValue = null);
                        writer.Set(so, "cellHp", p => p.floatValue = row.CellHp);
                        writer.Set(so, "functionHp", p => p.floatValue = row.FunctionHp);
                        writer.Set(so, "cellMass", p => p.floatValue = row.Mass);
                        writer.Set(so, "armorClass", p => p.intValue = 0);
                        writer.Set(so, "materialLayer", p => p.intValue = 0);
                        writer.Set(so, "propMesh", p => p.objectReferenceValue = mesh);
                        writer.Set(so, "blockCategory", p => p.intValue = (int)row.Category);
                        writer.Set(so, "modules", p =>
                        {
                            var modules = row.MakeModules(); p.arraySize = modules.Length;
                            // Register before assigning: Unity cannot replace an ID after first serialization.
                            for (int i = 0; i < modules.Length; i++)
                            {
                                if (!UnityEngine.Serialization.ManagedReferenceUtility.SetManagedReferenceIdForObject(so.targetObject, modules[i], (long)modules[i].Id))
                                    throw new InvalidOperationException("Could not assign a stable module reference ID.");
                                p.GetArrayElementAtIndex(i).managedReferenceValue = modules[i];
                            }
                        });
                    }, out _);
                    context.Report.Info("MIG-I091", file.RelativePath, "", "Dropped: " + Dropped(dto, row) + ".");
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Block conversion failed: " + e.Message); }
            }
        }
        internal static LegacyBlockItemDto Read(LegacyFile file, ValidationReport report) => file.Type switch
        {
            "BlockItem" => LegacyJson.Read<LegacyBlockItemDto>(file, report),
            "CoreBlockItem" => LegacyJson.Read<LegacyCoreBlockItemDto>(file, report),
            "ShieldBlockItem" => LegacyJson.Read<LegacyShieldBlockItemDto>(file, report),
            "PortalBlockItem" => LegacyJson.Read<LegacyPortalBlockItemDto>(file, report),
            "MirrorBlockItem" => LegacyJson.Read<LegacyMirrorBlockItemDto>(file, report),
            "WeaponBlockItem" => LegacyJson.Read<LegacyWeaponBlockItemDto>(file, report),
            _ => null,
        };
        internal static Vector3Int[] Footprint(LegacyBlockItemDto dto)
        {
            if (dto.VoxelShape?.Length > 0) return dto.VoxelShape.Select(Cell).ToArray();
            if (dto.BlockSize == null || dto.BlockSize.Length != 3 || dto.BlockSize.Any(n => n < 1 || n > 32)) throw new FormatException("BlockSize must have three extents in 1..32.");
            var result = new List<Vector3Int>();
            for (int z = 0; z < dto.BlockSize[2]; z++) for (int y = 0; y < dto.BlockSize[1]; y++) for (int x = 0; x < dto.BlockSize[0]; x++) result.Add(new Vector3Int(x, y, z));
            return result.ToArray();
        }
        static Vector3Int Cell(int[] xyz) => xyz != null && xyz.Length == 3 ? new Vector3Int(xyz[0], xyz[1], xyz[2]) : throw new FormatException("Expected three cell coordinates.");
        static bool SameCells(IReadOnlyCollection<Vector3Int> a, IReadOnlyCollection<Vector3Int> b) => a.Count == b.Count && new HashSet<Vector3Int>(a).Count == a.Count && new HashSet<Vector3Int>(a).SetEquals(b);
        static float FaceDifference(Vector3 a, Vector3 b) { var d = a - b; return Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z)); }
        static void SetCells(SerializedProperty property, Vector3Int[] cells)
        {
            property.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) property.GetArrayElementAtIndex(i).vector3IntValue = cells[i];
        }
        static string Dropped(LegacyBlockItemDto dto, LegacyBlockValues row)
        {
            var fields = new List<string> { "RotationLocked", "SocketTypeProvided", "SocketsProvidedCount", "Modifiers" };
            if (row.Category != BlockCategory.Core) fields.Add("EnergyRegenRate");
            if (row.Category != BlockCategory.Core && row.Category != BlockCategory.Power) fields.Add("MaxEnergyStorage");
            fields.Add("PassiveDrainRate");
            if (dto is LegacyShieldBlockItemDto) fields.Add("ShieldRadius, ShieldForwardDirection");
            if (dto is LegacyPortalBlockItemDto) fields.Add("EntryPosition, EntryDirection, ExitPosition, ExitDirection, EntryRadius");
            if (dto is LegacyWeaponBlockItemDto) fields.Add("disableInputTrigger, MuzzlePoint");
            if (dto is LegacyMirrorBlockItemDto) fields.Add("BeamThickness, BeamDamageMultiplier, IsRotating, RotationSpeed, MaxRotationAngle");
            return string.Join(", ", fields);
        }
    }
}
