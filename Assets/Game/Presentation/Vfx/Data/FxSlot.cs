using Game.Data.Definitions;
namespace Game.Presentation.Vfx
{
    internal struct FxSlot
    {
        public FxKind Kind;
        public FxMode Mode;
        public float Scale;
        public uint DefaultColorRgba8;
        public int ParticlesPerEvent, MaxEventsPerFrame, BufferOffset;
        public float MaxLifetime, CullDistanceSq, LodDistanceSq, LodParticleFactor;
        public VfxQuality MinQuality;
    }
}
