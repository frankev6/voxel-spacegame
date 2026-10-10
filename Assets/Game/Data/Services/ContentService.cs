namespace Game.Data.Services
{
    /// <summary>Boot-time owner of the catalog and blobs (architecture §4.2, service #2). Implements SPEC-04 §4.9, §5.1–§5.2.</summary>
    public sealed class ContentService : System.IDisposable
    {
        public ContentService(Game.Data.ContentDatabase database) { throw new System.NotImplementedException("DATA-007"); }   // throws ContentValidationException on errors
        internal static ContentService CreateForTests(System.Collections.Generic.IReadOnlyList<Game.Data.Definitions.ContentDefinition> definitions) { throw new System.NotImplementedException("DATA-007"); }

        public ContentCatalog Catalog { get; }
        public Game.Data.Validation.ValidationReport BootReport { get; }
        public Unity.Entities.BlobAssetReference<Game.Data.Blocks.BlockDefsBlob> BlockDefs { get; }
        public Unity.Entities.BlobAssetReference<Game.Data.Blocks.OrientationTablesBlob> Orientations { get; }
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Mesh> PropMeshes { get; } // index = BlockDef.PropMeshIndex
        public ulong DatabaseHash { get; }                                         // Combine(BlockDefsHash, IdentityHash); saved as diagnostics by SPEC-05

        public void CreateSingletons(Unity.Entities.EntityManager entityManager) { throw new System.NotImplementedException("DATA-007"); }  // idempotent per world
        public void DestroySingletons(Unity.Entities.EntityManager entityManager) { throw new System.NotImplementedException("DATA-007"); }
        public void Dispose() { throw new System.NotImplementedException("DATA-007"); }                                                     // disposes both blobs; idempotent
    }
}
