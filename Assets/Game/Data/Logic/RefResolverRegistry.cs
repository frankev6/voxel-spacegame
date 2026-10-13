namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.4); later task implementations are explicitly labelled.</summary>
    public sealed class RefResolverRegistry
    {
        // TODO(NAR-006): register canonical content-family resolvers and implement lookup.
        public static RefResolverRegistry CreateDefault(Game.Data.Services.ContentCatalog content)
        {
            // QST-001 supplies the four canonical families. NAR-006 still owns resolver behavior and the remaining families.
            var registry = new RefResolverRegistry();
            registry.Register(new CatalogRefResolver<Game.Data.Definitions.QuestDefinition>(RefKind.Quest, content));
            registry.Register(new CatalogRefResolver<Game.Data.Definitions.ClueDefinition>(RefKind.Clue, content)); // Includes RumorDefinition through inheritance.
            registry.Register(new CatalogRefResolver<Game.Data.Definitions.StoryBeatDefinition>(RefKind.Beat, content));
            registry.Register(new CatalogRefResolver<Game.Data.Definitions.MysteryDefinition>(RefKind.Mystery, content));
            throw new System.NotImplementedException("NAR-006");
        }
        public void Register(IRefResolver resolver) => throw new System.NotImplementedException("NAR-006");                    // replaces an existing resolver of the same kind
        public bool TryGet(RefKind kind, out IRefResolver resolver) => throw new System.NotImplementedException("NAR-006");
    }
}
