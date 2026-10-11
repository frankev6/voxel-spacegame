namespace Game.Presentation.Audio
{
    /// <summary>Public contract; reset each frame by AudioEventDrainSystem. Implements SPEC-64 §4.4.</summary>
    public sealed class AudioStats
    {
        public int EventsDrained, EventsOverHardCap, Unresolved, Silent, Culled, Groups, CooldownDropped;
        public int Started, Stolen, RejectedNoVoice, ActiveSfxVoices, ActiveLoopVoices, CombatSignals;
        public MusicState Music; public bool Bound;
    }
}
