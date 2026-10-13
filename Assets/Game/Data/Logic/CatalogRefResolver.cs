using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Data.Logic
{
    /// <summary>Reference resolver over one content-catalog family (SPEC-40 §4.4).</summary>
    public sealed class CatalogRefResolver<T> : IRefResolver where T : ContentDefinition
    {
        // TODO(NAR-006): index the content family and expose IDs in ordinal order.
        public CatalogRefResolver(RefKind kind, ContentCatalog content) => throw new System.NotImplementedException("NAR-006");
        public RefKind Kind => throw new System.NotImplementedException("NAR-006");
        public IReadOnlyList<string> AllIds => throw new System.NotImplementedException("NAR-006");
        public bool TryResolve(string id, out int index) => throw new System.NotImplementedException("NAR-006");
    }
}
