using System;
using System.Runtime.InteropServices;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. One voxel cell, 4 bytes (architecture §6.3); air is default(Cell).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Cell : IEquatable<Cell>
    {
        public ushort Ref;     // 0 = air; bit15=0: palette index 1..32767; bit15=1: instance index (Ref & 0x7FFF)
        public byte Hp;        // 1..255 for solid cells (255 = full = BlockDef.CellHp); 0 only for air
        public byte Flags;     // CellFlags
        public const ushort InstanceBit = 0x8000;
        public const ushort MaxPaletteIndex = 0x7FFF;          // 32767
        public const int MaxInstanceIndex = 0x7FFF;            // 32767
        public bool IsAir => Ref == 0;
        public bool IsInstance => (Ref & InstanceBit) != 0;
        public int InstanceIndex => Ref & 0x7FFF;              // valid only if IsInstance
        public ushort PaletteIndex => Ref;                     // valid only if !IsInstance && !IsAir
        public static Cell Palette(ushort paletteIndex, byte hp = 255, byte flags = 0) => new Cell { Ref = paletteIndex, Hp = hp, Flags = flags };
        public static Cell Instance(int instanceIndex, byte hp = 255, byte flags = 0) => new Cell { Ref = (ushort)(InstanceBit | instanceIndex), Hp = hp, Flags = flags };
        public bool Equals(Cell o) => Ref == o.Ref && Hp == o.Hp && Flags == o.Flags;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => Ref | (Hp << 16) | (Flags << 24);   // in-memory only, never persisted
    }
}
