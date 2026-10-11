using System;
using Game.Data.Services;
using Game.Voxel.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.6. Converts structure storage to/from the sole SPEC-05 VOXS codec; no byte layout of its own.</summary>
    public static class VoxelStructureCodec
    {
        public static unsafe VoxsSourceBuffers ToSource(VoxelStructure structure, Allocator allocator)
        {
            var buffers = new VoxsSourceBuffers();
            try
            {
                var data = structure.Data;
                buffers.Source.CellSize = structure.CellSize; buffers.Source.AnchorInstance = -1;
                buffers.Source.Palette = data.Palette.AsArray(); buffers.Source.Instances = data.Instances.AsArray();
                buffers.Source.BrickCoords = new NativeArray<int3>(data.Bricks.Length, allocator, NativeArrayOptions.UninitializedMemory);
                buffers.Source.Cells = new NativeArray<Cell>(checked(data.Bricks.Length * BrickMath.CellsPerBrick), allocator, NativeArrayOptions.UninitializedMemory);
                using var order = structure.SortedBrickOrder(Allocator.TempJob);
                for (int k = 0; k < order.Length; k++)
                {
                    var record = data.Bricks[order[k]]; buffers.Source.BrickCoords[k] = record.Coord;
                    SparseCellStorage.Expand(record, data.CellPages.AsArray(), data.SparsePages.AsArray(),
                        buffers.Source.Cells.GetSubArray(k * BrickMath.CellsPerBrick, BrickMath.CellsPerBrick));
                }
                return buffers;
            }
            catch { buffers.Dispose(); throw; }
        }
        public static byte[] Encode(VoxelStructure structure, VoxsBlockNames names, VoxsEncodeOptions options)
        {
            using var buffers = ToSource(structure, Allocator.TempJob);
            return VoxsCodec.Encode(buffers.Source, names, options);
        }
        public static bool TryDecode(ReadOnlySpan<byte> data, ContentCatalog catalog, out VoxelStructure result, out string error)
        {
            result = null;
            if (!VoxsCodec.TryDecode(data, catalog, Allocator.TempJob, out var decoded, out error)) return false;
            using (decoded)
            {
                if (decoded.Partial) { error = "VOXS partial payload cannot create a complete structure"; return false; }
                result = FromDecoded(decoded); return true;
            }
        }
        public static VoxelStructure FromDecoded(VoxsDecoded decoded)
        {
            if (decoded.Partial) throw new InvalidOperationException("Partial VOXS payloads must be applied by their owner");
            var structure = new VoxelStructure(decoded.CellSize);
            try
            {
                var data = structure.Data;
                // Raw VOXS permits an empty palette for an empty payload; structures always reserve slot zero.
                if (decoded.Palette.Length != 0) { data.Palette.Clear(); data.Palette.AddRange(decoded.Palette.AsArray()); }
                data.Instances.AddRange(decoded.Instances.AsArray());
                for (int i = 0; i < data.Instances.Length; i++) if (data.Instances[i].Block == 0) data.FreeInstances.Add(i);
                for (int i = 0; i < decoded.BrickCoords.Length; i++)
                    structure.SetBrickDense(decoded.BrickCoords[i], decoded.Cells.AsArray().GetSubArray(i * BrickMath.CellsPerBrick, BrickMath.CellsPerBrick));
                structure.OptimizeStorage(); return structure;
            }
            catch { structure.Dispose(); throw; }
        }
    }
}
