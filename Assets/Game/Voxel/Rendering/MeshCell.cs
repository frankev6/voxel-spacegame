using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    // ADR-0018: three shape bits; coverage is looked up in BrickShapeTables, not stored here.
    public static class MeshCell
    {
        public static uint Encode(int cls, int shape, int orient, int layer, int colour, int damage, bool scorched)
            => (uint)(cls & 3) | ((uint)(shape & 7) << 2) | ((uint)(orient & 31) << 5)
                | ((uint)(layer & 63) << 10) | ((uint)(colour & 255) << 16)
                | ((uint)(damage & 3) << 24) | (scorched ? 1u << 26 : 0);
        public static int Class(uint c) => (int)(c & 3);
        public static int Shape(uint c) => (int)((c >> 2) & 7);
        /// <summary>Orientation for the shape tables; 0 for a large hull cell, whose orient bits carry its offset (a cube looks the same in
        /// every orientation).</summary>
        public static int Orient(uint c) => LargeSide(c) != 0 ? 0 : (int)((c >> 5) & 31);
        /// <summary>ADR-0121 (DATA-013): side (2 or 3) of the large hull block this cell belongs to, or 0. Bits 27-28.</summary>
        public static int LargeSide(uint c) { int code = (int)((c >> 27) & 3); return code == 0 ? 0 : code + 1; }
        /// <summary>The cell's offset inside its large block's grid-aligned box (0..side−1 on each axis), from the orient bits.</summary>
        public static int3 LargeOffset(uint c)
        {
            int side = LargeSide(c), i = (int)((c >> 5) & 31);
            return side == 0 ? int3.zero : new int3(i % side, i / side % side, i / (side * side));
        }
        public static uint WithLarge(uint c, int side, int3 offset)
        {
            if (side < 2 || side > 3) return c;
            uint index = (uint)(offset.x + side * (offset.y + side * offset.z));
            return (c & ~((31u << 5) | (3u << 27))) | (index << 5) | ((uint)(side - 1) << 27);
        }
        public const int XenoShape = 5;
        public static bool IsXeno(uint c) => (c & 3) == 1 && ((c >> 2) & 7) == XenoShape;
        /// <summary>ADR-0115: a Xeno cell's orient field is its depth in eighths of a cell.</summary>
        public static float XenoDepthCells(uint c) => ((c >> 5) & 31) / 8f;
        public static int Layer(uint c) => (int)((c >> 10) & 63);
        public static int Colour(uint c) => (int)((c >> 16) & 255);
        public static int Damage(uint c) => (int)((c >> 24) & 3);
        public static bool Scorched(uint c) => ((c >> 26) & 1) != 0;
        public const int SnapSize = 18, SnapCells = 5832;
        public static int SnapIndex(int x, int y, int z) => (x + 1) + 18 * (y + 1) + 324 * (z + 1);
    }
}
