using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>The core ladder (owner, 2026-10-18: "cores should have a minimum and a maximum threshold of voxels they can hold. so a
    /// bigger core allows for a bigger ship and so on. but the minimum threshold of voxels should generally be 20% of the max
    /// threshold"). Every rung is a 1×1 core, so a ship's core is swapped in place for the one whose range (CoreParams.Floor ..
    /// MaxCells) holds the ship. Build() makes the new rungs from Core 1x1 and the Xeno heart and refits every shipped blueprint;
    /// the NOCTURNE, racer and Xeno builders call FitCore before they save.</summary>
    public static class CoreCapacityBuilder
    {
        const string Blocks = "Assets/Game/Content/Blocks/";
        /// <summary>Each line's 1×1 cores, smallest first. A core outside every line (the Destroyer cross) is never swapped.</summary>
        internal static readonly string[][] Ladders =
        {
            new[] { "block_core_micro", "block_core_small", "block_core_frigate", "block_core_capital" },
            new[] { "block_xeno_heart", "block_xeno_heart_great" },
        };

        [MenuItem("Game/Content/Build Core Ladder")]
        public static void Build()
        {
            Core("block_core_small", "block_core_micro", "Core | Courier Class", "A compact core for small craft: 200 to 1,000 cells.", 1000, 60, 60);
            Core("block_core_small", "block_core_frigate", "Core | Frigate Class", "A frigate core: 2,000 to 10,000 cells.", 10000, 150, 250);
            Core("block_core_small", "block_core_capital", "Core | Capital Class", "A capital ship's core: 5,000 to 25,000 cells.", 25000, 300, 600);
            Core("block_xeno_heart", "block_xeno_heart_great", "Xeno great heart", "The heart of a brood ship: 5,000 to 25,000 cells.", 25000, 300, 0);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            int refitted = 0;
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild())) refitted = FitAll(service);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the core ladder; refitted " + refitted + " blueprints.");
        }

        static void Core(string template, string id, string name, string description, int maxCells, int maxExtent, int price)
        {
            string path = Blocks + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + template + ".asset", path))
                throw new System.InvalidOperationException("copy " + template);
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description; so.FindProperty("formerIds").arraySize = 0;
            so.FindProperty("basePrice").intValue = price;
            var modules = so.FindProperty("modules"); bool found = false;
            for (int i = 0; i < modules.arraySize; i++)
            {
                var cells = modules.GetArrayElementAtIndex(i).FindPropertyRelative("Params.MaxCells");
                if (cells == null) continue;
                cells.intValue = maxCells; found = true;
                modules.GetArrayElementAtIndex(i).FindPropertyRelative("Params.MaxExtentCells").intValue = maxExtent;
                modules.GetArrayElementAtIndex(i).FindPropertyRelative("Params.MinCells").intValue = 0;
            }
            if (!found) throw new System.InvalidOperationException(template + " has no core module");
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }

        /// <summary>Refits every blueprint that flies (one core, at least one thruster; stations and turrets are left alone) whose
        /// cells fall outside its core's range. Returns how many were rewritten.</summary>
        internal static int FitAll(ContentService service)
        {
            var catalog = service.Catalog; var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            int refitted = 0;
            foreach (var blueprint in catalog.All<ShipBlueprintDefinition>())
            {
                if (!VoxelStructureCodec.TryDecode(blueprint.Payload, catalog, out var structure, out string error))
                { Debug.LogWarning(blueprint.IdString + ": " + error); continue; }
                using (structure)
                {
                    if (!Flies(structure, catalog)) continue;
                    string before = CoreOf(structure, catalog);
                    if (!FitCore(structure, content, catalog)) continue;
                    structure.TryGetCellBounds(out var min, out var max);
                    blueprint.EditorSetPayload(VoxelStructureCodec.Encode(structure, VoxsBlockNames.FromCatalog(catalog), VoxsEncodeOptions.ForAsset),
                        structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
                    EditorUtility.SetDirty(blueprint); refitted++;
                    Debug.Log($"{blueprint.IdString}: {structure.SolidCellCount} cells, core {before} -> {CoreOf(structure, catalog)}");
                }
            }
            return refitted;
        }

        /// <summary>Swaps the structure's single core for the smallest core of its line whose range holds the structure's cells.
        /// False when nothing changed: it already fits, it has no core or several, the core is not on a ladder, or no rung fits
        /// (logged).</summary>
        internal static bool FitCore(VoxelStructure structure, VoxelContent content, ContentCatalog catalog)
        {
            if (!SingleCore(structure, catalog, out var placement, out var core, out var module)) return false;
            int cells = structure.SolidCellCount;
            if (module.Params.Fits(cells)) return false;
            string[] line = null;
            foreach (var ladder in Ladders) if (System.Array.IndexOf(ladder, core.IdString) >= 0) line = ladder;
            if (line == null) return false;
            foreach (string id in line)
            {
                if (!catalog.TryGet<BlockDefinition>(new ContentId(id), out var rung) || !rung.TryGetModule<CoreModule>(out var rungCore) || !rungCore.Params.Fits(cells)) continue;
                if (!structure.TryRemoveBlockAt(placement.Origin, content, out _)) return false;
                var swapped = placement; swapped.Block = catalog.GetBlockIndex(rung);
                if (structure.TryPlaceBlock(swapped, content) == PlaceResult.Ok) return true;
                structure.TryPlaceBlock(placement, content);
                Debug.LogWarning($"core swap to {id} refused at {placement.Origin}");
                return false;
            }
            Debug.LogWarning($"no core on the {core.IdString} line holds {cells} cells");
            return false;
        }

        static bool SingleCore(VoxelStructure structure, ContentCatalog catalog, out BlockPlacement placement, out BlockDefinition core, out CoreModule module)
        {
            placement = default; core = null; module = null; int count = 0;
            using var blocks = new NativeList<StructureBlock>(Allocator.Temp); structure.GetBlocks(blocks);
            foreach (var block in blocks)
            {
                if (block.InstanceIndex < 0) continue;
                var definition = catalog.GetBlock(block.Placement.Block);
                if (definition == null || !definition.TryGetModule<CoreModule>(out var found)) continue;
                count++; placement = block.Placement; core = definition; module = found;
            }
            return count == 1;
        }

        static bool Flies(VoxelStructure structure, ContentCatalog catalog)
        {
            using var blocks = new NativeList<StructureBlock>(Allocator.Temp); structure.GetBlocks(blocks);
            foreach (var block in blocks)
                if (block.InstanceIndex >= 0 && catalog.GetBlock(block.Placement.Block) is { } definition && definition.TryGetModule<ThrusterModule>(out _)) return true;
            return false;
        }

        static string CoreOf(VoxelStructure structure, ContentCatalog catalog) =>
            SingleCore(structure, catalog, out _, out var core, out _) ? core.IdString : "?";
    }
}
