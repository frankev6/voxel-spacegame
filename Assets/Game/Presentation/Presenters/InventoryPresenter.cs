using System;
using System.Globalization;
using Game.Economy;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.Presentation.Presenters
{
    /// <summary>SPEC-61 §5.4/HUD-011: read-only projection of cargo; commands use its sole model writer.</summary>
    public sealed class InventoryPresenter : IInventoryCommands, IDisposable
    {
        readonly InventoryViewModel model;
        InventoryModel inventory;
        Wallet wallet;
        bool disposed;
        public InventoryPresenter(InventoryViewModel model) => this.model = model ?? throw new ArgumentNullException(nameof(model));
        public void Bind(InventoryModel inventory, Wallet wallet)
        {
            if (disposed) throw new ObjectDisposedException(nameof(InventoryPresenter));
            if (inventory == null || wallet == null) throw new ArgumentNullException("Inventory and wallet are required.");
            Unbind(); this.inventory = inventory; this.wallet = wallet;
            inventory.SlotChanged += SlotChanged; inventory.Reset += Refresh; wallet.Changed += RefreshWallet;
            model.SelectedIndex = -1; Refresh(); RefreshWallet();
        }
        public void Unbind()
        {
            if (inventory != null) { inventory.SlotChanged -= SlotChanged; inventory.Reset -= Refresh; }
            if (wallet != null) wallet.Changed -= RefreshWallet;
            inventory = null; wallet = null; model.SelectedIndex = -1;
            Refresh(); RefreshWallet();
        }
        void Refresh()
        {
            for (int i = 0; i < InventoryModel.MaxSlots; i++) UpdateSlot(i);
            RefreshCounts(); RefreshSelection(); model.NotifySlots();
        }
        void SlotChanged(int index) { UpdateSlot(index); RefreshCounts(); if (model.SelectedIndex == index) RefreshSelection(); }
        void UpdateSlot(int index)
        {
            var stack = inventory != null ? inventory.Get(index) : default;
            var row = model.Slots[index]; row.Index = index;
            row.ItemId = stack.IsEmpty ? null : string.IsNullOrEmpty(stack.OriginalId.Value) ? stack.Item.IdString : stack.OriginalId.Value;
            row.Name = stack.Item != null ? stack.Item.DisplayName : null; row.Icon = stack.Item != null ? stack.Item.Icon : null; row.Count = stack.Count; row.IsEmpty = stack.IsEmpty;
            row.InCapacity = inventory != null && index < inventory.Capacity; row.IsSelected = model.SelectedIndex == index;
        }
        void RefreshCounts()
        {
            model.Capacity = inventory?.Capacity ?? 0; model.UsedSlots = inventory?.UsedSlots ?? 0;
            int overflow = 0;
            if (inventory != null) for (int i = inventory.Capacity; i < InventoryModel.MaxSlots; i++) if (!inventory.Get(i).IsEmpty) overflow++;
            model.OverflowCount = overflow;
        }
        void RefreshWallet() { model.Wallet = wallet?.Credits ?? 0; model.WalletText = model.Wallet.ToString("N0", CultureInfo.InvariantCulture); }
        void RefreshSelection()
        {
            var stack = inventory != null && model.SelectedIndex >= 0 ? inventory.Get(model.SelectedIndex) : default;
            model.SelectedName = stack.Item != null ? stack.Item.DisplayName : ""; model.SelectedDescription = stack.Item != null ? stack.Item.Description : "";
            model.SelectedCount = stack.Count; model.SelectedValue = stack.Item != null ? stack.Item.BasePrice : 0;
        }
        public void Select(int slot)
        {
            if (inventory == null || (uint)slot >= InventoryModel.MaxSlots || slot >= inventory.Capacity && inventory.Get(slot).IsEmpty) return;
            int previous = model.SelectedIndex; model.SelectedIndex = slot;
            if (previous >= 0) model.Slots[previous].IsSelected = false;
            model.Slots[slot].IsSelected = true; RefreshSelection();
        }
        public void Move(int fromSlot, int toSlot)
        { if (inventory != null && inventory.TryMove(fromSlot, toSlot)) Select(toSlot); }
        public void Sort() { inventory?.Sort(); }
        public void StackAll() { inventory?.StackAll(); }
        public void Split(int slot) { if (inventory != null && inventory.TrySplit(slot, out int to)) Select(to); }
        public void Gather(int slot) { if (inventory != null && inventory.Gather(slot)) Select(slot); }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
