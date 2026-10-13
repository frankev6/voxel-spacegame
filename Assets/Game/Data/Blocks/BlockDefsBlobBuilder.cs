using System;
using System.Collections.Generic;
using System.IO;
using Game.Data.Definitions;
using Game.Data.Services;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Data.Blocks
{
    /// <summary>Builds BlockDefsBlob and BlockModulesBlob from the catalog (SPEC-04 §4.7, §5.5).</summary>
    public static class BlockDefsBlobBuilder
    {
        /// <summary>§5.5. propMeshes receives the prop mesh table (index = BlockDef.PropMeshIndex); modules receives the module rows.</summary>
        public static unsafe BlobAssetReference<BlockDefsBlob> Build(
            ContentCatalog catalog,
            Game.Data.Validation.ValidationReport report,
            List<UnityEngine.Mesh> propMeshes,
            Allocator allocator,
            out BlobAssetReference<BlockModulesBlob> modules)
        {
            var blocks = catalog.All<BlockDefinition>();
            int n = blocks.Count;
            propMeshes.Clear();
            var meshIndex = new Dictionary<UnityEngine.Mesh, int>();
            using var hashStream = new MemoryStream();
            using var canonical = new BinaryWriter(hashStream);   // little-endian

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlockDefsBlob>();
            var defs = builder.Allocate(ref root.Blocks, n + 1);
            // Entry 0: air (all zero, no prop mesh, empty arrays, category Structure).
            defs[0] = default;
            defs[0].PropMeshIndex = -1;
            defs[0].Category = BlockCategory.Structure;
            builder.Allocate(ref defs[0].Cells, 0); builder.Allocate(ref defs[0].Shapes, 0); builder.Allocate(ref defs[0].Clearance, 0); builder.Allocate(ref defs[0].AbilityIndices, 0);

            for (int i = 0; i < n; i++)
            {
                var b = blocks[i];
                ushort k = (ushort)(i + 1);
                ref var d = ref defs[k];
                d.Index = k;
                var partnerIndex = catalog.GetBlockIndex(b.MirrorPartner);
                d.MirrorIndex = partnerIndex != 0 ? partnerIndex : k;
                d.Kind = b.Kind; d.Shape = b.Shape; d.AttachFaces = b.AttachFaces;
                d.MaterialLayer = b.MaterialLayer; d.ArmorClass = b.ArmorClass; d.AllowedOrientations = b.AllowedOrientations.Bits;
                d.CellHp = b.CellHp; d.FunctionHp = b.FunctionHp; d.CellMass = b.CellMass; d.Category = b.BlockCategory;
                d.IdHash = b.Id.Hash;
                if (b.PropMesh != null)
                {
                    if (!meshIndex.TryGetValue(b.PropMesh, out int mi)) { mi = propMeshes.Count; propMeshes.Add(b.PropMesh); meshIndex.Add(b.PropMesh, mi); }
                    d.PropMeshIndex = mi;
                }
                else d.PropMeshIndex = -1;

                var cells = Canonical(b.Cells, rootFirst: true);
                var clearance = Canonical(b.Clearance, rootFirst: false);
                var cellArray = builder.Allocate(ref d.Cells, cells.Count);
                for (int c = 0; c < cells.Count; c++) cellArray[c] = cells[c];
                var shapeArray = builder.Allocate(ref d.Shapes, cells.Count);
                var authoredShapes = new Dictionary<int3, BlockShape>(cells.Count);
                for (int c = 0; c < b.Cells.Count; c++)
                {
                    var position = b.Cells[c];
                    authoredShapes.Add(new int3(position.x, position.y, position.z), b.GetShape(c));
                }
                for (int c = 0; c < cells.Count; c++) shapeArray[c] = authoredShapes[cells[c]];
                d.Shape = shapeArray[0];
                var clearArray = builder.Allocate(ref d.Clearance, clearance.Count);
                for (int c = 0; c < clearance.Count; c++) clearArray[c] = clearance[c];
                var abilityArray = builder.Allocate(ref d.AbilityIndices, b.GrantedAbilities.Count);
                for (int a = 0; a < abilityArray.Length; a++)
                    abilityArray[a] = catalog.IndexOf<AbilityDefinition>(b.GrantedAbilities[a]);
                int3 min = cells.Count > 0 ? cells[0] : int3.zero, max = min;
                foreach (var c in cells) { min = math.min(min, c); max = math.max(max, c); }
                d.BoundsMin = min; d.BoundsMax = max;
                d.MirrorOffset = BlockPlacementMath.ComputeMirrorOffset(cells.ToArray(), Canonical(b.MirrorPartner.Cells, true).ToArray(), out bool valid);
                if (!valid) report.Error("DATA-E043", "content:" + b.IdString, "mirrorPartner", "Mirror offset has no exact solution; the block skipped validation.");
                ulong mask = 0;
                foreach (var m in b.Modules) if (m != null) mask |= 1ul << (int)m.Id;
                d.ModuleMask = mask;

                canonical.Write(d.IdHash.Value); canonical.Write((byte)d.Kind); canonical.Write((byte)d.Shape); canonical.Write((byte)d.AttachFaces);
                canonical.Write(d.MaterialLayer); canonical.Write(d.ArmorClass); canonical.Write(d.AllowedOrientations); canonical.Write(d.MirrorIndex);
                canonical.Write(d.MirrorOffset.x); canonical.Write(d.MirrorOffset.y); canonical.Write(d.MirrorOffset.z);
                canonical.Write(d.CellHp); canonical.Write(d.FunctionHp); canonical.Write(d.CellMass);
                canonical.Write((ushort)cells.Count); foreach (var c in cells) { canonical.Write(c.x); canonical.Write(c.y); canonical.Write(c.z); }
                for (int c = 0; c < cells.Count; c++) canonical.Write((byte)shapeArray[c]);
                canonical.Write((ushort)clearance.Count); foreach (var c in clearance) { canonical.Write(c.x); canonical.Write(c.y); canonical.Write(c.z); }
                canonical.Write(checked((ushort)abilityArray.Length));
                for (int a = 0; a < abilityArray.Length; a++) canonical.Write(abilityArray[a]);
                canonical.Write(d.ModuleMask); canonical.Write((byte)d.Category);
            }
            root.MissingBlockIndex = catalog.GetBlockIndex(catalog.MissingBlock);

            modules = BuildModules(catalog, blocks, canonical, allocator);
            canonical.Flush();
            root.BlockDefsHash = Game.Core.StableHash.Hash64(new ReadOnlySpan<byte>(hashStream.GetBuffer(), 0, (int)hashStream.Length));
            return builder.CreateBlobAssetReference<BlockDefsBlob>(allocator);
        }

        /// <summary>§5.5 step 3b; also appends the module part of the canonical hash bytes (block index u16 + raw row) in id, then block order.</summary>
        static unsafe BlobAssetReference<BlockModulesBlob> BuildModules(ContentCatalog catalog, IReadOnlyList<BlockDefinition> blocks, BinaryWriter canonical, Allocator allocator)
        {
            int maxId = 0;
            foreach (BlockModuleId id in Enum.GetValues(typeof(BlockModuleId))) maxId = Math.Max(maxId, (int)id);
            var rowSize = new int[maxId + 1];
            var offset = new int[maxId + 1];
            var holders = new List<(int Block, BlockModule Module)>[maxId + 1];
            for (int m = 0; m <= maxId; m++) holders[m] = new List<(int, BlockModule)>();
            for (int i = 0; i < blocks.Count; i++)
                foreach (var module in blocks[i].Modules)
                    if (module != null && holders[(int)module.Id].TrueForAll(h => h.Block != i + 1)) holders[(int)module.Id].Add((i + 1, module));
            int total = 0;
            for (int m = 0; m <= maxId; m++)
            {
                rowSize[m] = holders[m].Count > 0 ? holders[m][0].Module.RowSize : 0;
                total = (total + 15) & ~15;
                offset[m] = total;
                total += rowSize[m] * holders[m].Count;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlockModulesBlob>();
            var tables = builder.Allocate(ref root.Tables, maxId + 1);
            var data = builder.Allocate(ref root.RowData, total);
            byte* basePtr = (byte*)data.GetUnsafePtr();
            if (total > 0) UnsafeUtility.MemClear(basePtr, total);
            for (int m = 0; m <= maxId; m++)
            {
                tables[m].Id = (BlockModuleId)m;
                tables[m].RowSize = rowSize[m];
                tables[m].DataOffset = offset[m];
                var rows = builder.Allocate(ref tables[m].RowByBlock, blocks.Count + 1);
                for (int k = 0; k < rows.Length; k++) rows[k] = -1;
                for (int r = 0; r < holders[m].Count; r++)
                {
                    var (block, module) = holders[m][r];
                    rows[block] = (short)r;
                    byte* row = basePtr + offset[m] + r * rowSize[m];
                    module.BakeRow(new BlockModuleBakeContext(catalog, blocks[block - 1]), row);
                    canonical.Write((ushort)block);
                    for (int b = 0; b < rowSize[m]; b++) canonical.Write(row[b]);
                }
            }
            return builder.CreateBlobAssetReference<BlockModulesBlob>(allocator);
        }

        /// <summary>Unrotated cells as int3: optionally (0,0,0) first, the rest sorted by (z, y, x).</summary>
        static List<int3> Canonical(IReadOnlyList<UnityEngine.Vector3Int> cells, bool rootFirst)
        {
            var list = new List<int3>(cells?.Count ?? 0);
            bool hasRoot = false;
            if (cells != null)
                foreach (var c in cells)
                {
                    var v = new int3(c.x, c.y, c.z);
                    if (rootFirst && v.Equals(int3.zero) && !hasRoot) { hasRoot = true; continue; }
                    list.Add(v);
                }
            list.Sort((a, b) => a.z != b.z ? a.z.CompareTo(b.z) : a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            if (rootFirst) list.Insert(0, int3.zero);
            return list;
        }
    }
}
