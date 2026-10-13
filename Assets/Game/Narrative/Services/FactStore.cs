namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public sealed class FactStore : Game.Data.Logic.ILogicFacts
    {
        // TODO(NAR-007): implement this runtime service; the contract shell owns no runtime state.

        public FactStore(Game.Data.Logic.FactCatalog catalog, int changeCapacity = 256) => throw new System.NotImplementedException("NAR-007");
        public Game.Data.Logic.FactCatalog Catalog => throw new System.NotImplementedException("NAR-007");
        public int GetBySlot(int slot) => throw new System.NotImplementedException("NAR-007");
        public int Get(in Game.Data.Logic.FactId id) => throw new System.NotImplementedException("NAR-007");
        public void SetBySlot(int slot, int value, Game.Data.Logic.FactWriteSource source) => throw new System.NotImplementedException("NAR-007");
        public void AddBySlot(int slot, int delta, Game.Data.Logic.FactWriteSource source) => throw new System.NotImplementedException("NAR-007");
        public int DrainChanged(System.Collections.Generic.List<int> intoSlots) => throw new System.NotImplementedException("NAR-007");   // dedupe, first-change order; returns count
        public FactSnapshot Capture() => throw new System.NotImplementedException("NAR-007");                                            // §5.2
        public void Restore(FactSnapshot snapshot) => throw new System.NotImplementedException("NAR-007");                               // in place, no events
        public void ResetToDefaults() => throw new System.NotImplementedException("NAR-007");                                            // new game, no events
        public int NonDefaultCount => throw new System.NotImplementedException("NAR-007");
        public LogicTraceLog Trace => throw new System.NotImplementedException("NAR-007");                                       // null in release builds
    }
}
