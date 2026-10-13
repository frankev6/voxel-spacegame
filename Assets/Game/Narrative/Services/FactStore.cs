using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Logic;

namespace Game.Narrative
{
    /// <summary>SPEC-40 §5.2: main-thread owner of dense facts, deferred changes and lossless save snapshots.</summary>
    public sealed class FactStore : ILogicFacts
    {
        readonly int[] values;
        readonly bool[] changedFlag;
        int[] changedSlots;
        int changedCount, nonDefaultCount;
        readonly Dictionary<string, int> orphans = new Dictionary<string, int>(StringComparer.Ordinal);
        public FactCatalog Catalog { get; }
        public LogicTraceLog Trace { get; }
        public int NonDefaultCount => nonDefaultCount;
        public FactStore(FactCatalog catalog, int changeCapacity = 256)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (changeCapacity < 0) throw new ArgumentOutOfRangeException(nameof(changeCapacity));
            values = new int[catalog.Count]; changedFlag = new bool[catalog.Count];
            changedSlots = new int[Math.Max(1, changeCapacity)];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Trace = new LogicTraceLog();
#endif
            ResetToDefaults();
        }
        bool Valid(int slot)
        {
            if ((uint)slot < (uint)values.Length) return true;
            GameLog.ErrorOnce("FactStore.InvalidSlot:" + slot, "Narrative", "Invalid fact slot " + slot + "."); return false;
        }
        public int GetBySlot(int slot) => Valid(slot) ? values[slot] : 0;
        public int Get(in FactId id) => GetBySlot(Catalog.SlotOf(id));
        int Clamp(int slot, int value)
        {
            if (Catalog.KindAt(slot) == FactKind.Bool) return value == 0 ? 0 : 1;
            int min = Catalog.MinAt(slot), max = Catalog.MaxAt(slot);
            return min < max ? Math.Clamp(value, min, max) : value;
        }
        public void SetBySlot(int slot, int value, FactWriteSource source)
        {
            if (!Valid(slot)) return;
            value = Clamp(slot, value); int old = values[slot]; if (old == value) return;
            int baseline = Catalog.DefaultAt(slot);
            if (old == baseline) nonDefaultCount++;
            if (value == baseline) nonDefaultCount--;
            values[slot] = value;
            if (!changedFlag[slot])
            {
                if (changedCount == changedSlots.Length) Array.Resize(ref changedSlots, checked(changedSlots.Length * 2));
                changedSlots[changedCount++] = slot; changedFlag[slot] = true;
            }
            EventBus<FactChanged>.Publish(new FactChanged(Catalog.IdAt(slot), slot, old, value, source));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Trace.Add(new LogicTraceEntry { Frame = UnityEngine.Time.frameCount, Kind = LogicTraceKind.FactWrite,
                Source = source, Subject = slot, OldValue = old, NewValue = value });
#endif
        }
        public void AddBySlot(int slot, int delta, FactWriteSource source)
        {
            if (!Valid(slot)) return;
            if (Catalog.KindAt(slot) == FactKind.Bool)
            { GameLog.ErrorOnce("FactStore.AddBool:" + slot, "Narrative", "Cannot add to Bool fact " + Catalog.IdAt(slot).Name + "."); return; }
            long sum = (long)values[slot] + delta;
            SetBySlot(slot, (int)Math.Clamp(sum, int.MinValue, int.MaxValue), source);
        }
        public int DrainChanged(List<int> intoSlots)
        {
            if (intoSlots == null) throw new ArgumentNullException(nameof(intoSlots));
            int count = changedCount;
            for (int i = 0; i < count; i++) { int slot = changedSlots[i]; intoSlots.Add(slot); changedFlag[slot] = false; }
            changedCount = 0; return count;
        }
        public FactSnapshot Capture()
        {
            var saved = new FactSnapshot();
            var unknown = new List<string>(orphans.Keys); unknown.Sort(StringComparer.Ordinal);
            int orphan = 0;
            for (int slot = 0; slot < values.Length; slot++)
            {
                if (Catalog.ScopeAt(slot) != FactScope.Save || values[slot] == Catalog.DefaultAt(slot)) continue;
                string name = Catalog.IdAt(slot).Name;
                while (orphan < unknown.Count && StringComparer.Ordinal.Compare(unknown[orphan], name) < 0)
                { string key = unknown[orphan++]; saved.Entries.Add(new FactEntry(key, orphans[key])); }
                saved.Entries.Add(new FactEntry(name, values[slot]));
            }
            while (orphan < unknown.Count) { string key = unknown[orphan++]; saved.Entries.Add(new FactEntry(key, orphans[key])); }
            return saved;
        }
        public void Restore(FactSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ResetToDefaults();
            var seen = new bool[values.Length]; var live = new bool[values.Length];
            foreach (var entry in snapshot.Entries)
            {
                int slot = Catalog.SlotOf(entry.Name);
                if (slot < 0)
                {
                    orphans[entry.Name] = entry.Value;
                    GameLog.WarnOnce("FactStore.Orphan:" + entry.Name, "Narrative", "Preserving unknown saved fact '" + entry.Name + "'."); continue;
                }
                if (Catalog.ScopeAt(slot) != FactScope.Save) continue;
                bool canonical = StringComparer.Ordinal.Equals(entry.Name, Catalog.IdAt(slot).Name);
                if (seen[slot])
                {
                    GameLog.Warn("Narrative", "Multiple saved names resolve to fact '" + Catalog.IdAt(slot).Name + "'; the live name wins.");
                    if (live[slot] && !canonical) continue;
                }
                seen[slot] = true; live[slot] = canonical; values[slot] = Clamp(slot, entry.Value);
            }
            for (int slot = 0; slot < values.Length; slot++) if (values[slot] != Catalog.DefaultAt(slot)) nonDefaultCount++;
        }
        public void ResetToDefaults()
        {
            for (int slot = 0; slot < values.Length; slot++) values[slot] = Catalog.DefaultAt(slot);
            for (int i = 0; i < changedCount; i++) changedFlag[changedSlots[i]] = false;
            changedCount = 0; nonDefaultCount = 0; orphans.Clear();
        }
    }
}
