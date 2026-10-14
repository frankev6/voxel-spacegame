using System;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. Per-cell flag bits.</summary>
    [Flags] public enum CellFlags : byte
    {
        None = 0,
        InstanceRoot = 1,      // this cell is footprint cell (0,0,0) of its BlockInstance
        Scorched = 2,          // cosmetic burn mark; written by SPEC-13; SPEC-11 darkens the face
        // bits 2..7: Xeno tissue depth (ADR-0115), 0..63 in sixteenths of a cell below the grown surface; other shapes keep 0.
    }
    public static class XenoDepth
    {
        public const int Shift = 2, Max = 63;
        public const byte Mask = 0xFC;
        /// <summary>Depth below the grown surface in cells (0 .. 3.94).</summary>
        public static float Cells(byte flags) => ((flags & Mask) >> Shift) / 16f;
        public static byte With(byte flags, float cells) => (byte)((flags & ~Mask) | (Unity.Mathematics.math.clamp((int)Unity.Mathematics.math.round(cells * 16), 0, Max) << Shift));
    }
}
