using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Dto;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §5.4: functional-first placement and brick encoding, independent of AssetDatabase.</summary>
    internal static class LegacyShipBuilder
    {
        internal static LegacyShipBuildResult Build(LegacyShipItemDto dto, Func<string, (BlockDefinition Definition, ushort Index)> resolve, ValidationReport report, string path)
        {
            var result = new LegacyShipBuildResult();
            var voxels = dto.ShipVoxelData?.Voxels ?? throw new FormatException("ShipVoxelData.voxels is missing.");
            var row = new MigrationShipRow { Ship = dto.RegistryKey, LegacyVoxels = voxels.Length };
            var candidates = new List<(LegacyVoxelDto Voxel, BlockDefinition Def, ushort Index, byte Orient, byte Color, int Order)>();
            for (int i = 0; i < voxels.Length; i++)
            {
                var v = voxels[i]; string field = "voxels[" + i + "]";
                if (string.IsNullOrEmpty(v.BlockKey)) { row.EmptyKeys++; report.Warning("MIG-W050", path, field, "Empty block key skipped."); continue; }
                if (v.IsDestroyed) { report.Info("MIG-I051", path, field, "Destroyed voxel skipped."); continue; }
                var resolved = resolve(v.BlockKey); var def = resolved.Definition;
                if (def == null || resolved.Index == 0) { report.Error("MIG-E003", path, field, "Unknown block key: " + v.BlockKey); continue; }
                if (!LegacyEuler.TryToOrientation((float)v.RotX, (float)v.RotY, (float)v.RotZ, out byte orientation, out float error))
                { report.Error("MIG-E040", path, field, "Rotation is not a cube rotation."); continue; }
                if (error > .001f) report.Warning("MIG-W041", path, field, "Rotation snapped to a cube rotation.");
                if (!def.AllowedOrientations.Allows(orientation)) report.Warning("MIG-W042", path, field, "Disallowed orientation retained.");
                byte color = v.ColorIndex >= 0 && v.ColorIndex <= 16 ? (byte)v.ColorIndex : (byte)0;
                if (color != v.ColorIndex) report.Warning("MIG-W043", path, field, "Invalid colour replaced with 0.");
                foreach (string slot in v.ModuleSlots ?? Array.Empty<string>())
                    if (!string.IsNullOrEmpty(slot)) report.Warning("MIG-W044", path, field, "Module slot dropped: " + slot);
                candidates.Add((v, def, resolved.Index, orientation, color, i));
            }
            var occupied = new Dictionary<int3, (Cell Cell, int Order, string Key)>();
            var palette = new List<PaletteEntry> { default };
            var paletteIndex = new Dictionary<(ushort, byte, byte), ushort>();
            var instances = new List<BlockInstance>(); int anchor = -1;
            // Stable two passes: instances win over palette cells, then original list order wins.
            for (int pass = 0; pass < 2; pass++) foreach (var c in candidates)
            {
                if (c.Def.RequiresInstance != (pass == 0)) continue;
                var origin = new int3(c.Voxel.X, c.Voxel.Y, c.Voxel.Z);
                var cells = new int3[c.Def.Cells.Count]; bool overlap = false;
                for (int k = 0; k < cells.Length; k++)
                {
                    var local = c.Def.Cells[k]; var rotated = OrientationTables.Rotate(new int3(local.x, local.y, local.z), c.Orient);
                    cells[k] = new int3(checked(origin.x + rotated.x), checked(origin.y + rotated.y), checked(origin.z + rotated.z));
                    if (!overlap && occupied.TryGetValue(cells[k], out var previous))
                    {
                        report.Warning("MIG-W030", path, "voxels[" + c.Order + "]", c.Voxel.BlockKey + " overlaps " + previous.Key + " at voxel " + previous.Order + "; dropped.");
                        overlap = true;
                    }
                }
                if (overlap) { row.DroppedOverlaps++; continue; }
                Cell value;
                if (c.Def.RequiresInstance)
                {
                    if (instances.Count > Cell.MaxInstanceIndex) throw new FormatException("Too many block instances.");
                    value = Cell.Instance(instances.Count);
                    if (c.Def.TryGetModule<CoreModule>(out _)) { row.Cores++; anchor = instances.Count; }
                    instances.Add(new BlockInstance { Block = c.Index, Orient = c.Orient, Color = c.Color, Origin = origin,
                        FunctionHp = c.Def.FunctionHp, CellsAlive = checked((ushort)cells.Length) });
                }
                else
                {
                    var key = (c.Index, c.Orient, c.Color);
                    if (!paletteIndex.TryGetValue(key, out ushort p))
                    {
                        if (palette.Count > Cell.MaxPaletteIndex) throw new FormatException("Too many palette entries.");
                        p = (ushort)palette.Count; paletteIndex.Add(key, p); palette.Add(new PaletteEntry { Block = c.Index, Orient = c.Orient, Color = c.Color });
                    }
                    value = Cell.Palette(p);
                }
                foreach (int3 cell in cells)
                {
                    var written = value;
                    if (value.IsInstance && math.all(cell == origin)) written.Flags = (byte)CellFlags.InstanceRoot;
                    occupied.Add(cell, (written, c.Order, c.Voxel.BlockKey));
                    if (row.Cells == 0) result.BoundsMin = result.BoundsMax = cell;
                    else { result.BoundsMin = math.min(result.BoundsMin, cell); result.BoundsMax = math.max(result.BoundsMax, cell); }
                    row.Cells++;
                }
                row.Placed++;
            }
            if (row.Cores == 0) report.Warning("MIG-W052", path, "AnchorInstance", "Ship has no core; imported without an anchor.");
            else if (row.Cores > 1) report.Warning("MIG-W053", path, "AnchorInstance", "Ship has several cores; imported without an anchor.");
            var coords = new List<int3>(); var bricks = new Dictionary<int3, int>();
            foreach (var cell in occupied.Keys) if (!bricks.ContainsKey(cell >> 4)) { bricks.Add(cell >> 4, 0); coords.Add(cell >> 4); }
            coords.Sort((a, b) => { int z = a.z.CompareTo(b.z); if (z != 0) return z; int y = a.y.CompareTo(b.y); return y != 0 ? y : a.x.CompareTo(b.x); });
            for (int i = 0; i < coords.Count; i++) bricks[coords[i]] = i;
            try
            {
                result.Source.CellSize = .5f; result.Source.AnchorInstance = row.Cores == 1 ? anchor : -1;
                result.Source.Palette = new NativeArray<PaletteEntry>(palette.ToArray(), Allocator.TempJob);
                result.Source.Instances = new NativeArray<BlockInstance>(instances.ToArray(), Allocator.TempJob);
                result.Source.BrickCoords = new NativeArray<int3>(coords.ToArray(), Allocator.TempJob);
                result.Source.Cells = new NativeArray<Cell>(checked(coords.Count * 4096), Allocator.TempJob);
                foreach (var pair in occupied) result.Source.Cells[bricks[pair.Key >> 4] * 4096 + VoxsCodec.CellIndex(pair.Key & 15)] = pair.Value.Cell;
                row.Palette = palette.Count - 1; row.Instances = instances.Count; result.Statistics = row; return result;
            }
            catch { result.Dispose(); throw; }
        }
    }
}
