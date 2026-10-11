using Game.Voxel.PresentationStreams;
using Unity.Mathematics;

namespace Game.Presentation.Audio
{
    /// <summary>Implements SPEC-64 §4.4 (internal). One coalesced sound waiting for a voice.</summary>
    internal struct SfxCandidate { public int Slot; public float3 Pos; public float MaxVolume, FinalVolume, Audibility;
                                   public int Count; public ulong GroupKey; public SfxEventFlags Flags; public int Order; }
}
