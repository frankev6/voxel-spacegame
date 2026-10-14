using System;
using System.Collections.Generic;

namespace Game.Narrative.Dialog
{
    /// <summary>SPEC-41 §4.7/4.10: ordinal visit and engine-variable memory, shared by both runner paths.</summary>
    public sealed class DialogMemory
    {
        readonly Dictionary<string, int> values = new(StringComparer.Ordinal);
        static string VisitKey(string node)
        {
            if (string.IsNullOrEmpty(node)) throw new ArgumentException("Node name is required", nameof(node));
            return "visit:" + node;
        }
        public int GetVisitCount(string node) => GetRaw(VisitKey(node));
        public void IncrementVisit(string node)
        {
            string key = VisitKey(node); int previous = GetRaw(key);
            values[key] = previous == int.MaxValue ? previous : previous + 1;
        }
        public int GetRaw(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Memory key is required", nameof(key));
            return values.TryGetValue(key, out int value) ? value : 0;
        }
        public void SetRaw(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Memory key is required", nameof(key));
            values[key] = value;
        }
        public void Clear() => values.Clear();
        public void CopySorted(List<(string key, int value)> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            foreach (var pair in values) into.Add((pair.Key, pair.Value));
            into.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        }
    }
}
