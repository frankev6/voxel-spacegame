namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public sealed class FactSnapshot
    {
        public readonly System.Collections.Generic.List<FactEntry> Entries = new();
    }
}
