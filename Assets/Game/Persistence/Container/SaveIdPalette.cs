using System;
using Game.Core;

namespace Game.Persistence.Container
{
    /// <summary>Save-wide ContentId palette (IDPL). Implements the SPEC-02 palette interfaces (SPEC-05 §4.5, §4.10).</summary>
    public sealed class SaveIdPalette : Game.Core.Save.ISaveIdPalette, Game.Core.Save.ISaveIdPaletteReader
    {
        public const ushort None = 0xFFFF;
        public static SaveIdPalette CreateEmpty() { throw new System.NotImplementedException("PERS-004"); }
        public static SaveIdPalette SeededFrom(SaveIdPalette loaded) { throw new System.NotImplementedException("PERS-004"); }      // keeps loaded order as prefix
        public static SaveIdPalette Parse(ReadOnlySpan<byte> idplPayload) { throw new System.NotImplementedException("PERS-004"); }  // throws SaveFormatException
        public ushort GetOrAdd(in ContentId id) { throw new System.NotImplementedException("PERS-004"); }
        public ContentId Get(ushort local) { throw new System.NotImplementedException("PERS-004"); }
        public int Count => throw new System.NotImplementedException("PERS-004");
        public byte[] Serialize() { throw new System.NotImplementedException("PERS-004"); }
    }
}
