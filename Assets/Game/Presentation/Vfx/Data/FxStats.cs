namespace Game.Presentation.Vfx
{
    /// <summary>Implements SPEC-63 §4.3; layout is shared with the effect shaders.</summary>
    public sealed class FxStats
    {
        public int EventsDrained, EventsOverHardCap, Unresolved, StateKindRejected, QualitySkipped, Culled, BudgetDropped;
        public int BatchesSubmitted, PooledPlayed, DebrisSpawned, DebrisLiveEstimate;
        public int TracersDrawn, BeamsDrawn, PlumesDrawn, ShieldsDrawn, DissolvingChunks;
        public bool Bound;
    }
}
