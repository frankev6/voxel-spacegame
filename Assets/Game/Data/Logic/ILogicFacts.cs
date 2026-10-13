namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface ILogicFacts
    {
        int GetBySlot(int slot);
        int Get(in FactId id);                          // slow path: SlotOf + GetBySlot; unknown → 0 + WarnOnce
        void SetBySlot(int slot, int value, FactWriteSource source);
        void AddBySlot(int slot, int delta, FactWriteSource source);
        FactCatalog Catalog { get; }
    }
}
