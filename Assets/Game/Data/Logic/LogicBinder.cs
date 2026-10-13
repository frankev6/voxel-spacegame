namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    public sealed class LogicBinder
    {
        // TODO(NAR-003): implement compiler/binder behavior and diagnostics.
        public LogicBinder(FactCatalog facts, RefResolverRegistry refs, Game.Data.Validation.ValidationReport report) => throw new System.NotImplementedException("NAR-003");
        public int BindFact(string name) => throw new System.NotImplementedException("NAR-003");                 // slot or -1 (+ NAR-E006 into report)
        public int BindRef(RefKind kind, string id) => throw new System.NotImplementedException("NAR-003");      // index or 0 (+ NAR-E007, or NAR-W001 when no resolver)
        public int FailedBindings => throw new System.NotImplementedException("NAR-003");
    }
}
