using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.1: dense render snapshot from sparse/dense/uniform source records.</summary>
    public static class BrickSnapshotUtility
    {
        public static void FromStructure(VoxelStructureData data, int3 brick, ref BlockDefsBlob blocks,
            ref OrientationTablesBlob orientations, NativeArray<uint> output)
        {
            byte warnings = 0;
            for (int z = -1; z <= 16; z++) for (int y = -1; y <= 16; y++) for (int x = -1; x <= 16; x++)
            {
                int3 position = brick * 16 + new int3(x,y,z); Cell cell = default;
                if (data.BrickIndex.TryGetValue(BrickMath.BrickOf(position), out int index))
                    cell = SparseCellStorage.Read(data.Bricks[index], data.CellPages.AsArray(), data.SparsePages.AsArray(), BrickMath.LocalIndex(BrickMath.LocalOf(position)));
                output[MeshCell.SnapIndex(x,y,z)] = Encode(cell, position, data.Palette.AsArray(), data.Instances.AsArray(),
                    ref blocks, ref orientations, ref warnings, out _, out _);
            }
        }

        // Shared semantic decoding keeps source previews and live ECS rendering identical.
        internal static uint Encode(Cell cell, int3 position, NativeArray<PaletteEntry> palette, NativeArray<BlockInstance> instances,
            ref BlockDefsBlob blocks, ref OrientationTablesBlob orientations, ref byte warnings, out BrickPropInstance prop, out bool isPropRoot)
        {
            prop = default; isPropRoot = false;
            if (cell.IsAir) return 0;
            ushort block; byte orient, colour; BlockInstance instance = default;
            if (cell.IsInstance)
            {
                if ((uint)cell.InstanceIndex >= instances.Length) return Missing(cell, ref warnings);
                instance = instances[cell.InstanceIndex]; block = instance.Block; orient = instance.Orient; colour = instance.Color;
            }
            else
            {
                if (cell.PaletteIndex >= palette.Length) return Missing(cell, ref warnings);
                var entry = palette[cell.PaletteIndex]; block = entry.Block; orient = entry.Orient; colour = entry.Color;
            }
            if (block == 0 || block >= blocks.Blocks.Length || orient >= 24) return Missing(cell, ref warnings);
            ref var def = ref blocks.Blocks[block];
            if (!cell.IsInstance && def.RequiresInstance) return Missing(cell, ref warnings);
            int shape = (int)def.Shape;
            if (cell.IsInstance)
            {
                int3 local = orientations.Rotate(position - instance.Origin, orientations.Inverse(orient));
                int footprintIndex = -1;
                for (int i = 0; i < def.Cells.Length; i++) if (math.all(local == def.Cells[i])) { footprintIndex = i; break; }
                if (footprintIndex < 0) return Missing(cell, ref warnings);
                shape = (int)def.GetShape(footprintIndex);
            }
            if (shape > (int)BlockShape.Xeno) return Missing(cell, ref warnings);
            // ADR-0115: Xeno tissue has no orientation; its orient field carries the depth (eighths of a cell, 0..31).
            if (shape == (int)BlockShape.Xeno) orient = (byte)((cell.Flags & XenoDepth.Mask) >> (XenoDepth.Shift + 1));
            isPropRoot = def.Kind == BlockKind.Prop && (!cell.IsInstance || (cell.Flags & (byte)CellFlags.InstanceRoot) != 0);
            prop = new BrickPropInstance { Block = block, Orient = orient, Color = colour };
            return MeshCell.Encode(def.Kind == BlockKind.Structural ? 1 : 2, def.Kind == BlockKind.Structural ? shape : 0,
                orient, def.MaterialLayer, colour, 3 - (cell.Hp >> 6), (cell.Flags & (byte)CellFlags.Scorched) != 0);
        }
        internal static uint Missing(Cell cell, ref byte warnings)
        {
            warnings |= 1;
            return MeshCell.Encode(1, 0, 0, 63, 0, 3 - (cell.Hp >> 6), (cell.Flags & (byte)CellFlags.Scorched) != 0);
        }
    }
}
