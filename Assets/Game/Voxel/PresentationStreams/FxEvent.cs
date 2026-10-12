using Game.Core;
using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-63 §4.1; queue ownership follows ADR-0050.</summary>
    public struct FxEvent
    {
        public FxKind Kind;        // binding
        public FxEventFlags Flags; // added by SPEC-63
        public ushort Reserved;    // keep 0 (padding made explicit)
        public float3 Pos;         // binding: position in the local frame (metres)
        public float3 Dir;         // binding: unit direction; meaning per kind (§5.6). Zero = (0,1,0)
        public float3 Vel;         // binding: velocity to inherit (m/s, local frame)
        public float Scale;        // binding: size in metres; meaning per kind (§5.6). <= 0 = definition default
        public uint Color;         // binding: 0 = definition default; else sRGB RGBA8 packed R | G<<8 | B<<16 | A<<24,
                                   //          or a palette index in the low byte when Flags has PaletteColor
        public ContentHash Fx;     // added by SPEC-63: hash of a specific FxDefinition; default (Value 0) = default for Kind
    }
}
