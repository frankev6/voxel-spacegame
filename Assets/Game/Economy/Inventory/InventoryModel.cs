using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-003 owns behavior.</summary>
    public sealed class InventoryModel : Game.Data.Economy.ICargo
    {
        public const int MaxSlots = 200;
        public InventoryModel(Game.Data.Services.ContentCatalog catalog) => throw new System.NotImplementedException("ECO-003");
        public int Capacity => throw new System.NotImplementedException("ECO-003");                                   // decision 1
        public int UsedSlots => throw new System.NotImplementedException("ECO-003");                                  // non-empty slots in 0..199
        public ItemStack Get(int slot) => throw new System.NotImplementedException("ECO-003");                                // ArgumentOutOfRange outside 0..199
        public int CountOf(ContentId item) => throw new System.NotImplementedException("ECO-003");                            // cargo + overflow; 0 for unknown
        public int CountOf(int itemIndex) => throw new System.NotImplementedException("ECO-003");                             // ICargo / ILogicEconomy
        public void SetCapacity(int capacity) => throw new System.NotImplementedException("ECO-003");                         // clamps to [1, 200]; never moves items; raises Reset

        // Player-driven, all-or-nothing into cargo slots (decision 2)
        public bool TryAdd(ContentId item, int count, out string reason) => throw new System.NotImplementedException("ECO-003");                     // source Dev (SPEC-70)
        public bool TryAdd(ItemDefinition item, int count, AcquireSource source, out string reason) => throw new System.NotImplementedException("ECO-003");
        public bool CanAddAll(ItemDefinition item, int count) => throw new System.NotImplementedException("ECO-003");
        public int AddUpTo(int itemIndex, int count, AcquireSource source) => throw new System.NotImplementedException("ECO-003");                  // ICargo: as many as fit in cargo; returns added
        // Gives: never fail (decision 2)
        public void Give(ItemDefinition item, int count, AcquireSource source) => throw new System.NotImplementedException("ECO-003");
        // Removal
        public bool TryTake(ItemDefinition item, int count) => throw new System.NotImplementedException("ECO-003");                                 // all-or-nothing, removal order of §5.1
        public bool TryTakeFromSlot(int slot, int count) => throw new System.NotImplementedException("ECO-003");                                    // merchant sell
        // Atomic delta sets (SPEC-51)
        public bool CanApply(System.ReadOnlySpan<ItemDelta> deltas, System.Collections.Generic.List<ItemShortfall> shortfalls, out bool cargoFull) => throw new System.NotImplementedException("ECO-003");
        public bool TryApplyAtomic(System.ReadOnlySpan<ItemDelta> deltas) => throw new System.NotImplementedException("ECO-003");                   // additions use AcquireSource.EditorRefund
        // Organisation (SPEC-61)
        public bool TryMove(int fromSlot, int toSlot) => throw new System.NotImplementedException("ECO-003");
        public void Sort() => throw new System.NotImplementedException("ECO-003");
        public void StackAll() => throw new System.NotImplementedException("ECO-003");
        // Session
        public void ResetToEmpty() => throw new System.NotImplementedException("ECO-003");                                    // all slots empty, counts 0, capacity unchanged; raises Reset
        internal void RestoreSlot(int slot, ItemDefinition item, int count, ContentId originalId) => throw new System.NotImplementedException("ECO-003"); // INVT restore only

        public event System.Action<int> SlotChanged { add => throw new System.NotImplementedException("ECO-003"); remove => throw new System.NotImplementedException("ECO-003"); }                   // slot index
        public event System.Action Reset { add => throw new System.NotImplementedException("ECO-003"); remove => throw new System.NotImplementedException("ECO-003"); }                              // bulk change: restore, new game, sort, capacity change
        public event System.Action Changed { add => throw new System.NotImplementedException("ECO-003"); remove => throw new System.NotImplementedException("ECO-003"); }                            // after any change (SPEC-51 BOM refresh)
        public event System.Action<int> ItemCountChanged { add => throw new System.NotImplementedException("ECO-003"); remove => throw new System.NotImplementedException("ECO-003"); }              // item family index (ILogicEconomy)
    }
}
