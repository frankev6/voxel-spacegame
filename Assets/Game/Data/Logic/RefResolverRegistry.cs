namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.4); later task implementations are explicitly labelled.</summary>
    public sealed class RefResolverRegistry
    {
        // TODO(NAR-006): register canonical content-family resolvers and implement lookup.
        public static RefResolverRegistry CreateDefault(Game.Data.Services.ContentCatalog content) => throw new System.NotImplementedException("NAR-006");  // registers every kind whose definition type exists (§5.2 step 1)
        public void Register(IRefResolver resolver) => throw new System.NotImplementedException("NAR-006");                    // replaces an existing resolver of the same kind
        public bool TryGet(RefKind kind, out IRefResolver resolver) => throw new System.NotImplementedException("NAR-006");
    }
}
