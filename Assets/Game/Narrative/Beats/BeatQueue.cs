using System.Collections.Generic;
using Game.Core;
using Game.Data.Logic;

namespace Game.Narrative.Beats
{
    /// <summary>Bounded FIFO with deterministic overflow and head retry.</summary>
    internal sealed class BeatQueue
    {
        internal readonly struct Entry
        {
            internal readonly int Beat;
            internal readonly LogicScope Scope;
            internal readonly bool Condition;
            internal Entry(int beat, in LogicScope scope, bool condition) { Beat = beat; Scope = scope; Condition = condition; }
        }
        readonly List<Entry> entries = new(32);
        internal int Count => entries.Count;
        internal IReadOnlyList<Entry> Entries => entries;
        internal bool Contains(int beat) { foreach (var entry in entries) if (entry.Beat == beat) return true; return false; }
        internal bool Add(in Entry entry, bool first = false)
        {
            if (entries.Count == 32)
            {
                int remove = entries.FindIndex(value => value.Condition);
                if (remove < 0) { GameLog.Warn("Narrative", "Beat queue full with no Condition entry; new request refused."); return false; }
                entries.RemoveAt(remove); GameLog.Warn("Narrative", "Beat queue overflow; oldest Condition entry dropped.");
            }
            if (first) entries.Insert(0, entry); else entries.Add(entry);
            return true;
        }
        internal Entry Pop() { var entry = entries[0]; entries.RemoveAt(0); return entry; }
        internal void Clear() => entries.Clear();
    }
}
