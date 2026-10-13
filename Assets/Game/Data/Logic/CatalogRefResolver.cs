using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Data.Logic
{
    /// <summary>Immutable ID lookup retaining canonical content-family indices and former IDs.</summary>
    public sealed class CatalogRefResolver<T> : IRefResolver where T : ContentDefinition
    {
        readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
        public RefKind Kind { get; }
        public IReadOnlyList<string> AllIds { get; }
        public CatalogRefResolver(RefKind kind, ContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            Kind = kind;
            var definitions = content.All<T>();
            var ids = new string[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                ids[i] = definition.IdString;
                indices.Add(definition.IdString, content.IndexOf(definition));
            }
            foreach (var definition in definitions)
                foreach (string former in definition.FormerIds)
                    if (!string.IsNullOrEmpty(former) && !indices.ContainsKey(former))
                        indices.Add(former, content.IndexOf(definition));
            Array.Sort(ids, StringComparer.Ordinal);
            AllIds = Array.AsReadOnly(ids);
        }
        public bool TryResolve(string id, out int index)
        {
            index = 0;
            return id != null && indices.TryGetValue(id, out index);
        }
    }
}
