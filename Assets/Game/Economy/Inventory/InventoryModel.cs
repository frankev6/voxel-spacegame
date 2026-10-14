using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.1: atomic cargo, overflow, cached counts and deterministic organisation.</summary>
    public sealed class InventoryModel : Game.Data.Economy.ICargo
    {
        public const int MaxSlots = 200;
        readonly ContentCatalog catalog;
        readonly ItemStack[] slots = new ItemStack[MaxSlots], scratch = new ItemStack[MaxSlots];
        readonly int[] counts, scratchCounts, order;
        readonly long[] net;
        readonly bool[] seen;
        readonly int[] changedSlots = new int[MaxSlots], oldItems = new int[MaxSlots], newItems = new int[MaxSlots];
        readonly bool[] touched;
        readonly HashSet<ContentId> warnedUnknown = new HashSet<ContentId>();
        bool publishing;
        public int Capacity { get; private set; } = 20;
        public int UsedSlots { get; private set; }
        public event Action<int> SlotChanged;
        public event Action Reset;
        public event Action Changed;
        public event Action<int> ItemCountChanged;

        public InventoryModel(ContentCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            int size = catalog.All<ItemDefinition>().Count + 1;
            counts = new int[size]; scratchCounts = new int[size]; order = new int[size];
            net = new long[size]; seen = new bool[size]; touched = new bool[size];
        }
        public ItemStack Get(int slot)
        { CheckSlot(slot); return slots[slot]; }
        public int CountOf(ContentId item) => catalog.TryGet<ItemDefinition>(item, out var definition) ? CountOf(Index(definition)) : 0;
        public int CountOf(int itemIndex) => itemIndex > 0 && itemIndex < counts.Length ? counts[itemIndex] : 0;
        int Index(ItemDefinition item) => item == null ? 0 : catalog.IndexOf<ItemDefinition>(item);
        static void CheckSlot(int slot)
        { if ((uint)slot >= MaxSlots) throw new ArgumentOutOfRangeException(nameof(slot)); }
        void CheckMutation()
        { if (publishing) throw new InvalidOperationException("Inventory mutations cannot be nested inside change notifications."); }
        void CopyScratch()
        { Array.Copy(slots, scratch, MaxSlots); Array.Copy(counts, scratchCounts, counts.Length); }
        static bool Same(ItemStack a, ItemStack b) => a.Item == b.Item && a.Count == b.Count && a.OriginalId == b.OriginalId;
        static bool SameKind(ItemStack a, ItemStack b) => a.Item == b.Item && a.OriginalId == b.OriginalId;

        // All state is committed before notifications; observers never see half an atomic edit.
        void Commit(bool reset = false, bool force = false)
        {
            int changed = 0; Array.Clear(touched, 0, touched.Length);
            for (int i = 0; i < MaxSlots; i++)
            {
                var before = slots[i]; var after = scratch[i]; if (Same(before, after)) continue;
                int old = Index(before.Item), next = Index(after.Item);
                changedSlots[changed] = i; oldItems[changed] = old; newItems[changed] = next; changed++;
                if (old != 0) { counts[old] -= before.Count; touched[old] = true; }
                if (next != 0) { counts[next] += after.Count; touched[next] = true; }
                if (before.IsEmpty && !after.IsEmpty) UsedSlots++;
                if (!before.IsEmpty && after.IsEmpty) UsedSlots--;
                slots[i] = after;
            }
            if (changed == 0 && !force) return;
            publishing = true;
            try
            {
                if (reset)
                {
                    Reset?.Invoke();
                    for (int i = 1; i < touched.Length; i++) if (touched[i]) ItemCountChanged?.Invoke(i);
                }
                else for (int n = 0; n < changed; n++)
                {
                    SlotChanged?.Invoke(changedSlots[n]);
                    if (oldItems[n] != 0) ItemCountChanged?.Invoke(oldItems[n]);
                    if (newItems[n] != 0 && newItems[n] != oldItems[n]) ItemCountChanged?.Invoke(newItems[n]);
                }
                Changed?.Invoke();
            }
            finally { publishing = false; }
        }
        int Fill(ItemDefinition item, int amount, int first, int end, ContentId original = default)
        {
            int index = Index(item);
            if (index == 0 || amount <= 0 || item.MaxStack <= 0) return amount;
            int remaining = amount, allowed = Math.Min(amount, int.MaxValue - scratchCounts[index]);
            var kind = new ItemStack { Item = item, OriginalId = original };
            for (int i = first; i < end && allowed > 0; i++)
                if (!scratch[i].IsEmpty && SameKind(scratch[i], kind))
                {
                    int add = Math.Min(allowed, item.MaxStack - scratch[i].Count);
                    scratch[i].Count += add; allowed -= add; remaining -= add; scratchCounts[index] += add;
                }
            for (int i = first; i < end && allowed > 0; i++) if (scratch[i].IsEmpty)
            {
                int add = Math.Min(allowed, item.MaxStack);
                scratch[i] = new ItemStack { Item = item, Count = add, OriginalId = original };
                allowed -= add; remaining -= add; scratchCounts[index] += add;
            }
            return remaining;
        }
        void Remove(ItemDefinition item, int amount)
        {
            int index = Index(item);
            for (int i = MaxSlots - 1; i >= 0 && amount > 0; i--) if (scratch[i].Item == item)
            {
                int take = Math.Min(amount, scratch[i].Count); amount -= take;
                scratch[i].Count -= take; scratchCounts[index] -= take;
                if (scratch[i].Count == 0) scratch[i] = default;
            }
        }
        void Acquired(ItemDefinition item, int amount, AcquireSource source)
        {
            if (amount > 0 && source != AcquireSource.EditorRefund && source != AcquireSource.NewGame)
                EventBus<ItemAcquired>.Publish(new ItemAcquired(item.Id.Hash, Index(item), amount, source));
        }
        public void SetCapacity(int capacity)
        {
            CheckMutation(); int next = Math.Clamp(capacity, 1, MaxSlots); if (next == Capacity) return;
            Capacity = next; CopyScratch(); Commit(reset: true, force: true);
        }
        public bool TryAdd(ContentId item, int count, out string reason)
        {
            if (!catalog.TryGet<ItemDefinition>(item, out var definition)) { reason = "Unknown item"; return false; }
            return TryAdd(definition, count, AcquireSource.Dev, out reason);
        }
        public bool TryAdd(ItemDefinition item, int count, AcquireSource source, out string reason)
        {
            CheckMutation(); reason = null;
            if (count <= 0) { reason = "Invalid count"; return false; }
            if (Index(item) == 0) { reason = "Unknown item"; return false; }
            CopyScratch(); if (Fill(item, count, 0, Capacity) != 0) { reason = "Cargo full"; return false; }
            Commit(); Acquired(item, count, source); return true;
        }
        public bool CanAddAll(ItemDefinition item, int count)
        {
            if (count <= 0 || Index(item) == 0) return false;
            CopyScratch(); return Fill(item, count, 0, Capacity) == 0;
        }
        public int AddUpTo(int itemIndex, int count, AcquireSource source)
        {
            CheckMutation(); if (itemIndex <= 0 || itemIndex >= counts.Length || count <= 0) return 0;
            var item = catalog.GetByIndex<ItemDefinition>((ushort)itemIndex); CopyScratch();
            int added = count - Fill(item, count, 0, Capacity); Commit(); Acquired(item, added, source); return added;
        }
        public void Give(ItemDefinition item, int count, AcquireSource source)
        {
            CheckMutation(); if (count <= 0 || Index(item) == 0) return;
            CopyScratch(); int left = Fill(item, count, 0, Capacity); left = Fill(item, left, Capacity, MaxSlots);
            Commit(); Acquired(item, count - left, source);
            if (left > 0)
            {
                string message = $"Hold full: {left} × {item.DisplayName} jettisoned.";
                EventBus<EconomyNotice>.Publish(new EconomyNotice(EconomyNoticeKind.Jettisoned, message));
                GameLog.Warn(LogCategories.Economy, message);
            }
        }
        public bool TryTake(ItemDefinition item, int count)
        {
            CheckMutation(); if (count <= 0 || Index(item) == 0 || CountOf(Index(item)) < count) return false;
            CopyScratch(); Remove(item, count); Commit(); return true;
        }
        public bool TryTakeFromSlot(int slot, int count)
        {
            CheckMutation(); if ((uint)slot >= MaxSlots || count <= 0 || slots[slot].IsEmpty || slots[slot].Count < count) return false;
            CopyScratch(); scratch[slot].Count -= count; if (scratch[slot].Count == 0) scratch[slot] = default;
            Commit(); return true;
        }
        public bool CanApply(ReadOnlySpan<ItemDelta> deltas, List<ItemShortfall> shortfalls, out bool cargoFull)
            => Simulate(deltas, shortfalls, out cargoFull);
        bool Simulate(ReadOnlySpan<ItemDelta> deltas, List<ItemShortfall> shortfalls, out bool cargoFull)
        {
            shortfalls?.Clear(); cargoFull = false; int distinct = 0; bool missing = false;
            Array.Clear(net, 0, net.Length); Array.Clear(seen, 0, seen.Length);
            for (int i = 0; i < deltas.Length; i++)
            {
                var delta = deltas[i]; if (delta.Count == 0) continue;
                if (!catalog.TryGet<ItemDefinition>(delta.Item, out var item))
                {
                    if (delta.Count < 0)
                    { missing = true; shortfalls?.Add(new ItemShortfall(delta.Item, (int)Math.Min(int.MaxValue, -(long)delta.Count))); }
                    else if (warnedUnknown.Add(delta.Item)) GameLog.WarnOnce("Inventory.Unknown:" + delta.Item.Value, LogCategories.Economy, $"Ignoring inventory addition of unknown item '{delta.Item}'.");
                    continue;
                }
                int index = Index(item); if (!seen[index]) { seen[index] = true; order[distinct++] = index; }
                net[index] += delta.Count;
            }
            for (int n = 0; n < distinct; n++)
            {
                int i = order[n]; if (net[i] >= 0 || -net[i] <= counts[i]) continue;
                missing = true; shortfalls?.Add(new ItemShortfall(catalog.GetByIndex<ItemDefinition>((ushort)i).Id, (int)Math.Min(int.MaxValue, -net[i] - counts[i])));
            }
            if (missing) return false;
            CopyScratch();
            for (int n = 0; n < distinct; n++) { int i = order[n]; if (net[i] < 0) Remove(catalog.GetByIndex<ItemDefinition>((ushort)i), (int)-net[i]); }
            for (int n = 0; n < distinct; n++)
            {
                int i = order[n]; if (net[i] <= 0) continue;
                if (net[i] > int.MaxValue || Fill(catalog.GetByIndex<ItemDefinition>((ushort)i), (int)net[i], 0, Capacity) != 0)
                { cargoFull = true; return false; }
            }
            return true;
        }
        /// <summary>ADR-0096: bounded owner-bound rollback state for a synchronous editor commit.</summary>
        public sealed class Checkpoint
        {
            internal readonly InventoryModel Owner;
            internal readonly ItemStack[] Slots;
            internal readonly int Capacity;
            internal Checkpoint(InventoryModel owner)
            { Owner = owner; Slots = (ItemStack[])owner.slots.Clone(); Capacity = owner.Capacity; }
        }
        public Checkpoint CaptureCheckpoint() { CheckMutation(); return new Checkpoint(this); }
        public void RestoreCheckpoint(Checkpoint checkpoint)
        {
            CheckMutation();
            if (checkpoint == null || !ReferenceEquals(checkpoint.Owner, this)) throw new ArgumentException("Inventory checkpoint belongs to another owner.", nameof(checkpoint));
            Array.Copy(checkpoint.Slots, scratch, MaxSlots); Capacity = checkpoint.Capacity;
            Commit(reset: true, force: true);
        }
        public bool TryApplyAtomic(ReadOnlySpan<ItemDelta> deltas)
        {
            CheckMutation(); if (!Simulate(deltas, null, out _)) return false;
            Commit(); return true;
        }
        public bool TryMove(int fromSlot, int toSlot)
        {
            CheckMutation();
            if ((uint)fromSlot >= MaxSlots || (uint)toSlot >= MaxSlots || slots[fromSlot].IsEmpty || toSlot >= Capacity && toSlot != fromSlot) return false;
            if (fromSlot == toSlot) return true;
            CopyScratch();
            if (SameKind(scratch[fromSlot], scratch[toSlot]))
            {
                int moved = Math.Min(scratch[fromSlot].Count, scratch[toSlot].Item.MaxStack - scratch[toSlot].Count);
                scratch[toSlot].Count += moved; scratch[fromSlot].Count -= moved;
                if (scratch[fromSlot].Count == 0) scratch[fromSlot] = default;
            }
            else (scratch[fromSlot], scratch[toSlot]) = (scratch[toSlot], scratch[fromSlot]);
            Commit(); return true;
        }
        /// <summary>QA-001: moves half of a stack (rounded up) into the first empty in-capacity slot. False if none fits.</summary>
        public bool TrySplit(int slot, out int toSlot)
        {
            CheckMutation(); toSlot = -1;
            if ((uint)slot >= MaxSlots || slots[slot].IsEmpty || slots[slot].Count < 2) return false;
            for (int i = 0; i < Capacity; i++) if (slots[i].IsEmpty) { toSlot = i; break; }
            if (toSlot < 0) return false;
            CopyScratch();
            int moved = (scratch[slot].Count + 1) / 2;
            scratch[toSlot] = scratch[slot]; scratch[toSlot].Count = moved; scratch[slot].Count -= moved;
            Commit(); return true;
        }
        /// <summary>QA-001: pulls every other stack of the same kind into <paramref name="slot"/>, up to its max stack.</summary>
        public bool Gather(int slot)
        {
            CheckMutation();
            if ((uint)slot >= MaxSlots || slots[slot].IsEmpty) return false;
            CopyScratch(); bool changed = false;
            for (int j = 0; j < MaxSlots && scratch[slot].Count < scratch[slot].Item.MaxStack; j++)
                if (j != slot && !scratch[j].IsEmpty && SameKind(scratch[slot], scratch[j]))
                {
                    int moved = Math.Min(scratch[j].Count, scratch[slot].Item.MaxStack - scratch[slot].Count);
                    scratch[slot].Count += moved; scratch[j].Count -= moved; changed = true;
                    if (scratch[j].Count == 0) scratch[j] = default;
                }
            if (changed) Commit();
            return changed;
        }
        public void StackAll()
        {
            CheckMutation(); CopyScratch(); MergeScratch(); Commit();
        }
        void MergeScratch()
        {
            for (int i = 0; i < MaxSlots; i++) if (!scratch[i].IsEmpty)
                for (int j = i + 1; j < MaxSlots && scratch[i].Count < scratch[i].Item.MaxStack; j++)
                    if (!scratch[j].IsEmpty && SameKind(scratch[i], scratch[j]))
                    {
                        int moved = Math.Min(scratch[j].Count, scratch[i].Item.MaxStack - scratch[i].Count);
                        scratch[i].Count += moved; scratch[j].Count -= moved;
                        if (scratch[j].Count == 0) scratch[j] = default;
                    }
        }
        static int Compare(ItemStack a, ItemStack b)
        {
            if (a.IsEmpty) return b.IsEmpty ? 0 : 1; if (b.IsEmpty) return -1;
            int c = ((int)a.Item.Category).CompareTo((int)b.Item.Category); if (c != 0) return c;
            c = string.CompareOrdinal(a.Item.DisplayName, b.Item.DisplayName); if (c != 0) return c;
            c = string.CompareOrdinal(a.Item.IdString, b.Item.IdString);
            return c != 0 ? c : string.CompareOrdinal(a.OriginalId.Value, b.OriginalId.Value);
        }
        public void Sort()
        {
            CheckMutation(); CopyScratch();
            // A bounded insertion sort avoids allocating a comparer/delegate in this managed model.
            for (int i = 1; i < MaxSlots; i++)
            {
                var value = scratch[i]; int j = i;
                while (j > 0 && Compare(value, scratch[j - 1]) < 0) { scratch[j] = scratch[j - 1]; j--; }
                scratch[j] = value;
            }
            MergeScratch(); int write = 0;
            for (int i = 0; i < MaxSlots; i++) if (!scratch[i].IsEmpty) scratch[write++] = scratch[i];
            Array.Clear(scratch, write, MaxSlots - write); Commit(reset: true, force: true);
        }
        public void ResetToEmpty()
        { CheckMutation(); Array.Clear(scratch, 0, MaxSlots); Commit(reset: true, force: true); }
        internal void RestoreSlot(int slot, ItemDefinition item, int count, ContentId originalId)
        {
            CheckMutation(); CheckSlot(slot);
            if (Index(item) == 0 || count <= 0 || count > item.MaxStack) throw new ArgumentOutOfRangeException(nameof(count));
            int index = Index(item);
            long total = (long)counts[index] + count - (slots[slot].Item == item ? slots[slot].Count : 0);
            if (total > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count));
            CopyScratch(); scratch[slot] = new ItemStack { Item = item, Count = count, OriginalId = item == catalog.MissingItem ? originalId : default };
            Commit(reset: true);
        }
    }
}
