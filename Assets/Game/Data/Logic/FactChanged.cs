namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    public readonly struct FactChanged
    {
        public readonly FactId Id; public readonly int Slot; public readonly int OldValue; public readonly int NewValue; public readonly FactWriteSource Source;
        public FactChanged(FactId id, int slot, int oldValue, int newValue, FactWriteSource source)
        { Id = id; Slot = slot; OldValue = oldValue; NewValue = newValue; Source = source; }
    }
}
