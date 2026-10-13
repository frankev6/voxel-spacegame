using System;
using System.Collections.Generic;

namespace Game.Data.Logic
{
    /// <summary>Fallback resolver with stable one-based ordinal indices.</summary>
    public sealed class ListRefResolver : IRefResolver
    {
        readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
        public RefKind Kind { get; }
        public IReadOnlyList<string> AllIds { get; }
        public ListRefResolver(RefKind kind, IReadOnlyList<string> ids)
        {
            Kind = kind;
            var sorted = new SortedSet<string>(StringComparer.Ordinal);
            if (ids != null) foreach (string id in ids)
                if (!string.IsNullOrEmpty(id)) sorted.Add(id);
            var values = new string[sorted.Count]; sorted.CopyTo(values);
            for (int i = 0; i < values.Length; i++) indices.Add(values[i], i + 1);
            AllIds = Array.AsReadOnly(values);
        }
        public bool TryResolve(string id, out int index)
        {
            index = 0;
            return id != null && indices.TryGetValue(id, out index);
        }
    }
}
