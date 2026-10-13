namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    public sealed class FactCatalog
    {
        // TODO(NAR-006): implement ordinal fact interning, generated families and metadata lookup.
        public static FactCatalog Build(Game.Data.Services.ContentCatalog content, RefResolverRegistry refs, Game.Data.Validation.ValidationReport report) => throw new System.NotImplementedException("NAR-006");
        public int Count => throw new System.NotImplementedException("NAR-006");
        public int SlotOf(in FactId id) => throw new System.NotImplementedException("NAR-006");          // O(1) Dictionary<ulong,int>; -1 if unknown
        public int SlotOf(string name) => throw new System.NotImplementedException("NAR-006");           // compile/editor path; -1 if unknown; also resolves FormerNames to the live slot
        public FactId IdAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public FactKind KindAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public FactScope ScopeAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public FactFlags FlagsAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public FactFamily FamilyAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public int DefaultAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public int MinAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public int MaxAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public bool IsWritableByContent(int slot) => throw new System.NotImplementedException("NAR-006");
        public string JournalTextAt(int slot) => throw new System.NotImplementedException("NAR-006");
        public ulong SourceHashAt(int slot) => throw new System.NotImplementedException("NAR-006");                                   // generated slots: ContentHash.Value of the source definition; 0 otherwise
        public int GeneratedSlot(FactFamily family, ulong sourceHash) => throw new System.NotImplementedException("NAR-006");         // O(1); -1 if none (used by the world-event bridge, no strings)
    }
}
