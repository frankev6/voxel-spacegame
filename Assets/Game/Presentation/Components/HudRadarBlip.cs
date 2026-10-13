using Unity.Entities;
using Unity.Mathematics;

namespace Game.Presentation.Components
{
    /// <summary>Radar contact buffer on the HUD snapshot entity; at most 64 entries (SPEC-61 §4.5).</summary>
    [InternalBufferCapacity(0)]
    public struct HudRadarBlip : IBufferElementData
    {
        public float2 OffsetXZ;
        public byte Kind;
        public byte Relation;
        public byte Flags;
        public byte Pad;
    }
}
