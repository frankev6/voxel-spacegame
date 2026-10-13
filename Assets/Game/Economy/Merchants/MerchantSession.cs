using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.4: ordered trade checks and all-or-nothing purchases/sales on the main thread.</summary>
    public sealed class MerchantSession
    {
        readonly MerchantService owner;
        readonly MerchantService.State state;
        bool closed;
        public MerchantDefinition Merchant { get; }
        public bool Refused { get; }
        public string RefusalReason { get; }
        public int Relation { get; }
        public IReadOnlyList<MerchantStockState> Stock => state.View;
        public event Action Changed;
        internal MerchantSession(MerchantService owner, MerchantService.State state, MerchantDefinition merchant, int relation, bool refuse, string reason)
        {
            this.owner = owner; this.state = state; Merchant = merchant; Relation = relation;
            Refused = refuse; RefusalReason = refuse ? reason ?? "" : "";
        }
        public int BuyPriceOf(int stockIndex)
        {
            if ((uint)stockIndex >= state.Rows.Count) throw new ArgumentOutOfRangeException(nameof(stockIndex));
            var row = state.Rows[stockIndex];
            return PriceMath.BuyPrice(row.Item.BasePrice, row.PriceFactor, Merchant.SellPriceFactor, Relation, owner.Config.RelationPriceSlope);
        }
        bool Buys(ItemDefinition item) => item != null && item.Tradeable && (int)item.Category < 8 &&
            (Merchant.Buys & (ItemCategoryMask)(1 << (int)item.Category)) != 0;
        public int SellPriceOf(ItemDefinition item) => Buys(item) ?
            PriceMath.SellPrice(item.BasePrice, Merchant.BuyPriceFactor, Relation, owner.Config.RelationPriceSlope) : 0;
        bool Available(out string reason)
        { reason = closed ? "Session closed" : Refused ? RefusalReason : null; return !closed && !Refused; }
        public bool CanBuy(int stockIndex, int quantity, out string reason)
        {
            if (!Available(out reason)) return false;
            if ((uint)stockIndex >= state.Rows.Count) { reason = "Not in stock"; return false; }
            if (quantity < 1) { reason = "Invalid quantity"; return false; }
            var row = state.Rows[stockIndex];
            if (row.Count != -1 && quantity > row.Count) { reason = $"Only {row.Count} in stock"; return false; }
            long total = (long)BuyPriceOf(stockIndex) * quantity;
            if (owner.Wallet.Credits < total) { reason = $"Not enough credits: {total} needed"; return false; }
            if (!owner.Inventory.CanAddAll(row.Item, quantity)) { reason = "Cargo full"; return false; }
            return true;
        }
        public bool TryBuy(int stockIndex, int quantity, out string reason)
        {
            if (!CanBuy(stockIndex, quantity, out reason) || !owner.BeginTrade(out reason)) return false;
            try
            {
                var row = state.Rows[stockIndex]; long total = (long)BuyPriceOf(stockIndex) * quantity;
                if (!owner.Wallet.TrySpend(total)) { reason = $"Not enough credits: {total} needed"; return false; }
                if (!owner.Inventory.TryAdd(row.Item, quantity, AcquireSource.Purchase, out reason))
                {
                    owner.Wallet.Add(total);
                    GameLog.Error(LogCategories.Economy, "Merchant purchase failed after cargo preflight; credits refunded."); return false;
                }
                if (row.Count != -1) { row.Count -= quantity; if (row.Count == 0 && row.Buyback) state.Rows.RemoveAt(stockIndex); }
                Changed?.Invoke(); return true;
            }
            finally { owner.EndTrade(); }
        }
        int Find(ItemDefinition item)
        { for (int i = 0; i < state.Rows.Count; i++) if (state.Rows[i].Item == item) return i; return -1; }
        public bool CanSell(int slot, int quantity, out string reason)
        {
            if (!Available(out reason)) return false;
            if ((uint)slot >= InventoryModel.MaxSlots || owner.Inventory.Get(slot).IsEmpty) { reason = "Nothing to sell"; return false; }
            var stack = owner.Inventory.Get(slot);
            if (!stack.Item.Tradeable) { reason = "Cannot sell this item"; return false; }
            if (!Buys(stack.Item)) { reason = $"{Merchant.DisplayName} does not buy {stack.Item.Category}"; return false; }
            if (quantity < 1 || quantity > stack.Count) { reason = "Invalid quantity"; return false; }
            if (SellPriceOf(stack.Item) == 0) { reason = "No value"; return false; }
            int index = Find(stack.Item);
            if (index >= 0 && state.Rows[index].Count != -1 && state.Rows[index].Count > int.MaxValue - quantity)
            { reason = "Stock full"; return false; }
            return true;
        }
        public bool TrySell(int slot, int quantity, out string reason)
        {
            if (!CanSell(slot, quantity, out reason) || !owner.BeginTrade(out reason)) return false;
            try
            {
                var stack = owner.Inventory.Get(slot); int price = SellPriceOf(stack.Item), index = Find(stack.Item);
                if (!owner.Inventory.TryTakeFromSlot(slot, quantity)) { reason = "Invalid quantity"; return false; }
                owner.Wallet.Add((long)price * quantity);
                if (index >= 0) { if (state.Rows[index].Count != -1) state.Rows[index].Count += quantity; }
                else state.Rows.Add(new MerchantStockState { Item = stack.Item, Count = quantity, PriceFactor = 1, Buyback = true, OriginalId = stack.OriginalId });
                Changed?.Invoke(); return true;
            }
            finally { owner.EndTrade(); }
        }
        public void Close() { if (closed) return; closed = true; owner.Closed(this); }
    }
}
