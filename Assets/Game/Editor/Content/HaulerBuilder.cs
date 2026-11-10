using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>EXT-015 the crew's hauler (owner, 2026-11-10: "we should use a bigger ship that can actually pull the 200t so we don't have
    /// to use a command"): the VESPER trader with a cargo refit. Four cargo blocks (4 × 50 t = a 200 t hold) replace hull cells deep
    /// inside the hull, so the silhouette is the VESPER's own; written as `ship_vesper_hauler`. Run by the Lantern build and from the menu.</summary>
    public static class HaulerBuilder
    {
        internal const string Id = "ship_vesper_hauler", Source = "ship_nocturne_vesper", Cargo = "block_cargo_small";
        const int Holds = 4;

        [MenuItem("Game/Content/Build Vesper Hauler")]
        public static void BuildMenu()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            Build(service);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
        }

        internal static int Build(ContentService service)
        {
            var catalog = service.Catalog; var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            if (!catalog.TryGet<ShipBlueprintDefinition>(new Game.Core.ContentId(Source), out var source)) throw new System.InvalidOperationException(Source + " missing");
            if (!VoxelStructureCodec.TryDecode(source.Payload, catalog, out var structure, out string error)) throw new System.InvalidOperationException(Source + ": " + error);
            using (structure)
            {
                ushort cargo = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new Game.Core.ContentId(Cargo)));
                structure.TryGetCellBounds(out var min, out var max);
                var centre = (float3)(min + max) * .5f;
                // candidates: a pair of cells along x, both solid, single-cell blocks, every neighbour solid (deep inside), nearest the centre first
                var candidates = new List<int3>();
                for (int z = min.z + 1; z < max.z; z++) for (int y = min.y + 1; y < max.y; y++) for (int x = min.x + 1; x < max.x - 1; x++)
                {
                    var c = new int3(x, y, z);
                    if (Deep(structure, c) && Deep(structure, c + new int3(1, 0, 0))) candidates.Add(c);
                }
                candidates.Sort((a, b) => math.distancesq((float3)a, centre).CompareTo(math.distancesq((float3)b, centre)));
                var placed = new List<int3>();
                foreach (var c in candidates)
                {
                    if (placed.Count == Holds) break;
                    bool near = false; foreach (var p in placed) if (math.distancesq((float3)p, (float3)c) < 16) near = true;
                    if (near || !Single(structure, catalog, c) || !Single(structure, catalog, c + new int3(1, 0, 0))) continue;
                    structure.TryRemoveBlockAt(c, content, out var a); structure.TryRemoveBlockAt(c + new int3(1, 0, 0), content, out var b);
                    if (structure.TryPlaceBlock(new BlockPlacement { Block = cargo, Origin = c, Orient = 0, Color = a.Color }, content) == PlaceResult.Ok) { placed.Add(c); continue; }
                    structure.TryPlaceBlock(a, content); structure.TryPlaceBlock(b, content);
                }
                if (placed.Count < Holds) throw new System.InvalidOperationException($"{Id}: only {placed.Count} of {Holds} cargo blocks fit");
                LanternBuilder.Write(service, Id, "VESPER hauler", source.CellSize, structure);
                Debug.Log($"[Content] Built {Id}: {Holds} cargo blocks ({Holds * 50} t hold), {structure.SolidCellCount} cells");
                return structure.SolidCellCount;
            }
        }

        static bool Deep(VoxelStructure s, int3 c)
        {
            if (s.GetCell(c).IsAir) return false;
            for (int i = 0; i < 6; i++)
            {
                var d = new int3(i == 0 ? 1 : i == 1 ? -1 : 0, i == 2 ? 1 : i == 3 ? -1 : 0, i == 4 ? 1 : i == 5 ? -1 : 0);
                if (s.GetCell(c + d).IsAir) return false;
            }
            return true;
        }

        static bool Single(VoxelStructure s, Game.Data.Services.ContentCatalog catalog, int3 c)
        {
            if (!s.TryGetBlockAt(c, out var block, out _)) return false;
            var def = catalog.GetBlock(block.Block);
            return def != null && def.Cells.Count == 1 && !def.RequiresInstance;
        }
    }
}
