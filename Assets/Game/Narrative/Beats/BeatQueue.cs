using System.Collections.Generic;
using Game.Core;
using Game.Data.Logic;

namespace Game.Narrative.Beats
{
    /// <summary>Bounded FIFO with an initial introduction prefix, deterministic overflow and head retry.</summary>
    internal sealed class BeatQueue
    {
        internal readonly struct Entry
        {
            internal readonly int Beat;
            internal readonly ContentId RawId;
            internal readonly LogicScope Scope;
            internal readonly bool Condition;
            internal readonly bool Introduction;
            internal Entry(int beat, in LogicScope scope, bool condition, ContentId rawId = default, bool introduction = false)
            { Beat = beat; Scope = scope; Condition = condition; RawId = rawId; Introduction = introduction; }
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
            if (first) entries.Insert(0, entry);
            else if (entry.Introduction)
            {
                // Initial campaign introduction is a FIFO prefix. Arrival/Condition
                // requests already queued during streaming keep their relative order.
                int ambient = entries.FindIndex(value => !value.Introduction);
                entries.Insert(ambient < 0 ? entries.Count : ambient, entry);
            }
            else entries.Add(entry);
            return true;
        }
        internal bool TryPop(out Entry entry)
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].Beat > 0)
            { entry = entries[i]; entries.RemoveAt(i); return true; }
            entry = default; return false; // Unknown saved beats remain dormant and round-trip intact.
        }
        internal void Clear() => entries.Clear();
    }
}
