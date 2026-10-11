using System;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §5.2: deterministic disjoint boxes, with conservative coarsening.</summary>
    public static class BrickBoxMerger
    {
        public static int MergeExact(in BrickMaskBits mask, ref NativeList<BrickBox> output)
        {
            var remaining = mask;
            output.Clear();
            for (int z = 0; z < 16; z++)
                for (int y = 0; y < 16; y++)
                {
                    uint row = remaining.Row(y, z);
                    while (row != 0)
                    {
                        int x0 = math.tzcnt(row);
                        int length = math.min(16 - x0, math.tzcnt(~(row >> x0)));
                        uint run = ((1u << length) - 1) << x0;
                        int y1 = y, z1 = z;
                        while (y1 < 15 && (remaining.Row(y1 + 1, z) & run) == run) y1++;
                        while (z1 < 15)
                        {
                            bool covered = true;
                            for (int yy = y; yy <= y1; yy++)
                                if ((remaining.Row(yy, z1 + 1) & run) != run) { covered = false; break; }
                            if (!covered) break;
                            z1++;
                        }
                        for (int zz = z; zz <= z1; zz++)
                            for (int yy = y; yy <= y1; yy++)
                                remaining.SetRow(yy, zz, (ushort)(remaining.Row(yy, zz) & ~run));
                        output.Add(new BrickBox { MinX = (byte)x0, MinY = (byte)y, MinZ = (byte)z,
                            MaxX = (byte)(x0 + length - 1), MaxY = (byte)y1, MaxZ = (byte)z1 });
                        row = remaining.Row(y, z);
                    }
                }
            return output.Length;
        }

        public static int MergeCoarse(in BrickMaskBits mask, int superCell, ref NativeList<BrickBox> output)
        {
            if (superCell != 2 && superCell != 4) throw new ArgumentOutOfRangeException(nameof(superCell));
            int side = 16 / superCell;
            uint run = (1u << superCell) - 1;
            var coarse = new BrickMaskBits();
            for (int z = 0; z < side; z++)
                for (int y = 0; y < side; y++)
                {
                    uint row = 0;
                    for (int zz = z * superCell; zz < (z + 1) * superCell; zz++)
                        for (int yy = y * superCell; yy < (y + 1) * superCell; yy++) row |= mask.Row(yy, zz);
                    ushort bits = 0;
                    for (int x = 0; x < side; x++) if (((row >> (x * superCell)) & run) != 0) bits |= (ushort)(1 << x);
                    coarse.SetRow(y, z, bits);
                }
            // Embed the smaller mask in the low corner of a 16³ mask; the other rows are empty.
            // This reuses the exact same ordered greedy walk without a second scratch allocation.
            MergeExact(coarse, ref output);
            for (int i = 0; i < output.Length; i++)
            {
                var b = output[i];
                b.MinX *= (byte)superCell; b.MinY *= (byte)superCell; b.MinZ *= (byte)superCell;
                b.MaxX = (byte)((b.MaxX + 1) * superCell - 1);
                b.MaxY = (byte)((b.MaxY + 1) * superCell - 1);
                b.MaxZ = (byte)((b.MaxZ + 1) * superCell - 1);
                output[i] = b;
            }
            return output.Length;
        }

        public static int Merge(in BrickMaskBits mask, int maxBoxes, ref NativeList<BrickBox> output)
        {
            if (maxBoxes < 1) throw new ArgumentOutOfRangeException(nameof(maxBoxes));
            if (MergeExact(mask, ref output) <= maxBoxes) return 0;
            if (MergeCoarse(mask, 2, ref output) <= maxBoxes) return 1;
            if (MergeCoarse(mask, 4, ref output) <= maxBoxes) return 2;
            output.Clear();
            if (mask.TryGetBounds(out var min, out var max))
                output.Add(new BrickBox { MinX = (byte)min.x, MinY = (byte)min.y, MinZ = (byte)min.z,
                    MaxX = (byte)max.x, MaxY = (byte)max.y, MaxZ = (byte)max.z });
            return 3;
        }
    }
}
