using Game.Core;
using Unity.Mathematics;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-64 §4.1 (architecture §6.6 binding fields + Flags). Size 32 bytes.</summary>
    public struct SfxEvent
    {
        public ContentHash Cue;     // binding: hash of an AudioCueDefinition; Value 0 = no sound
        public float3 Pos;          // binding: position in the local frame (ignored for 2D cues)
        public float Volume;        // binding: 0..1 multiplier on the cue volume
        public SfxEventFlags Flags; // added by SPEC-64
        public byte Reserved0;      // keep 0
        public ushort Reserved1;    // keep 0
    }
}
