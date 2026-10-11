using Game.Data.Definitions;

namespace Game.Presentation.Audio
{
    /// <summary>Implements SPEC-64 §4.4 (internal). Per-cue data the drain system reads.</summary>
    internal struct SfxCueInfo { public int Slot; public byte Priority; public float Volume, SpatialBlend, MinDistance, MaxDistance,
                                 Cooldown, CoalesceCellSize, CoalesceGain; public CoalesceMode Coalesce; public int MaxVoices;
                                 public bool CombatSignal; }
}
