using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Data.Definitions;
using Game.Economy;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.Presentation.Presenters
{
    /// <summary>HUD-012: model-only trading presenter; filtered indices never escape to the economy owner.</summary>
    public sealed class MerchantPresenter : IMerchantCommands, IDisposable
    {
        readonly UIRoot ui;
        readonly MerchantViewModel model;
        readonly InventoryModel inventory;
        readonly Wallet wallet;
        readonly List<int> stockIndices = new(128), cargoSlots = new(200), categories = new(9);
        readonly List<MerchantRowViewModel> stockPool = new(128), cargoPool = new(200);
        MerchantSession session;
        Action onClosed;
        bool bound, refreshing, trading, awaitingPush, disposed;
        public MerchantPresenter(UIRoot ui, MerchantViewModel model, InventoryModel inventory, Wallet wallet)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }
        public void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MerchantPresenter));
            if (bound) return;
            bound = true; ui.Navigator.StackChanged += StackChanged;
        }
        public void Unbind()
        {
            if (bound) { bound = false; ui.Navigator.StackChanged -= StackChanged; }
            ResetSession();
        }
        public void Show(MerchantSession value, Action closed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MerchantPresenter));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(session, value)) return;
            Bind(); ReleaseCurrent();
            // A close callback may open a newer merchant. Keep that newer request.
            if (session != null || disposed) { value.Close(); closed?.Invoke(); return; }
            session = value; onClosed = closed;
            model.SelectedCategory = 0; model.SelectedStockIndex = model.SelectedCargoIndex = -1; model.Quantity = 1;
            session.Changed += Refresh; inventory.Changed += Refresh; wallet.Changed += Refresh;
            Refresh();
            var view = ui.Navigator.Get<MerchantView>();
            if (!view.IsOpen)
            {
                awaitingPush = true;
                try { ui.Navigator.Push(view); }
                catch { if (ReferenceEquals(session, value)) ReleaseCurrent(); throw; }
            }
        }
        void StackChanged()
        {
            if (session == null) return;
            if (ui.Navigator.IsOpen<MerchantView>()) { awaitingPush = false; return; }
            if (!awaitingPush) ReleaseCurrent();
        }
        void ReleaseCurrent()
        {
            var old = session; var callback = onClosed;
            session = null; onClosed = null; awaitingPush = false;
            if (old == null) return;
            old.Changed -= Refresh; inventory.Changed -= Refresh; wallet.Changed -= Refresh;
            old.Close(); ClearModel(); callback?.Invoke();
        }
        void ClearModel()
        {
            stockIndices.Clear(); cargoSlots.Clear(); categories.Clear();
            model.Stock.Clear(); model.Cargo.Clear(); model.Categories.Clear();
            model.MerchantName = ""; model.Wallet = wallet.Credits; model.WalletText = wallet.Credits.ToString("N0", CultureInfo.InvariantCulture);
            model.Refused = false; model.RefusalReason = ""; model.SelectedCategory = 0;
            model.SelectedStockIndex = model.SelectedCargoIndex = -1; model.Quantity = 1; model.MaxQuantity = 0;
            model.BuyTotal = model.SellTotal = 0; model.CanBuy = model.CanSell = false;
            model.BuyBlockedReason = model.SellBlockedReason = ""; model.DetailName = "Select an item"; model.DetailDescription = "";
            model.NotifyCategories(); model.NotifyStock(); model.NotifyCargo();
        }
        public void ResetSession()
        {
            var old = session;
            if (old == null) return;
            if (awaitingPush || ui.Navigator.IsOpen<MerchantView>()) ui.Navigator.Pop(ui.Navigator.Get<MerchantView>());
            // Pop may be deferred while navigation dispatches callbacks. Detach the old session now.
            if (ReferenceEquals(session, old)) ReleaseCurrent();
        }
        bool CanAct => !disposed && session != null && !refreshing && !trading;
        public void SelectCategory(int index)
        {
            if (!CanAct || (uint)index >= categories.Count) return;
            model.SelectedCategory = index; model.SelectedStockIndex = model.SelectedCargoIndex = -1; model.Quantity = 1;
            Refresh();
        }
        public void SelectStock(int index)
        {
            if (!CanAct || index < -1 || index >= stockIndices.Count) return;
            model.SelectedStockIndex = index; model.SelectedCargoIndex = -1; model.Quantity = 1; RefreshDetails(); model.NotifyStock(); model.NotifyCargo();
        }
        public void SelectCargo(int index)
        {
            if (!CanAct || index < -1 || index >= cargoSlots.Count) return;
            model.SelectedCargoIndex = index; model.SelectedStockIndex = -1; model.Quantity = 1; RefreshDetails(); model.NotifyStock(); model.NotifyCargo();
        }
        public void SetQuantity(int quantity)
        {
            if (!CanAct) return;
            model.Quantity = Math.Max(1, Math.Min(quantity, Math.Max(1, model.MaxQuantity))); RefreshDetails();
        }
        public void Buy() => Trade(true);
        public void Sell() => Trade(false);
        void Trade(bool buy)
        {
            if (!CanAct) return;
            int selected = buy ? model.SelectedStockIndex : model.SelectedCargoIndex;
            var indices = buy ? stockIndices : cargoSlots;
            if ((uint)selected >= indices.Count) return;
            var current = session;
            trading = true;
            string reason;
            bool success;
            try { success = buy ? current.TryBuy(indices[selected], model.Quantity, out reason) : current.TrySell(indices[selected], model.Quantity, out reason); }
            finally { trading = false; }
            if (!ReferenceEquals(current, session)) return;
            Refresh();
            if (!success)
            {
                if (buy) { model.CanBuy = false; model.BuyBlockedReason = reason; }
                else { model.CanSell = false; model.SellBlockedReason = reason; }
            }
        }
        static string Name(ItemDefinition item) => string.IsNullOrWhiteSpace(item.DisplayName) ? item.IdString : item.DisplayName;
        static MerchantRowViewModel Row(List<MerchantRowViewModel> pool, int index)
        {
            while (pool.Count <= index) pool.Add(new MerchantRowViewModel());
            return pool[index];
        }
        void Refresh()
        {
            if (session == null || disposed || refreshing) return;
            refreshing = true;
            try
            {
                string stockId = (uint)model.SelectedStockIndex < model.Stock.Count ? model.Stock[model.SelectedStockIndex].ItemId : null;
                int cargoSlot = (uint)model.SelectedCargoIndex < cargoSlots.Count ? cargoSlots[model.SelectedCargoIndex] : -1;
                string cargoId = (uint)model.SelectedCargoIndex < model.Cargo.Count ? model.Cargo[model.SelectedCargoIndex].ItemId : null;
                int category = (uint)model.SelectedCategory < categories.Count ? categories[model.SelectedCategory] : -1;
                bool[] present = new bool[8];
                foreach (var entry in session.Stock) if (entry.Item != null && (int)entry.Item.Category < present.Length) present[(int)entry.Item.Category] = true;
                for (int slot = 0; slot < InventoryModel.MaxSlots; slot++)
                { var stack = inventory.Get(slot); if (!stack.IsEmpty && (int)stack.Item.Category < present.Length) present[(int)stack.Item.Category] = true; }
                categories.Clear(); model.Categories.Clear(); categories.Add(-1); model.Categories.Add("All");
                for (int i = 0; i < present.Length; i++) if (present[i]) { categories.Add(i); model.Categories.Add(((ItemCategory)i).ToString()); }
                int categoryIndex = categories.IndexOf(category); model.SelectedCategory = categoryIndex >= 0 ? categoryIndex : 0;
                category = categories[model.SelectedCategory];
                stockIndices.Clear(); cargoSlots.Clear(); model.Stock.Clear(); model.Cargo.Clear();
                model.SelectedStockIndex = model.SelectedCargoIndex = -1;
                for (int i = 0; i < session.Stock.Count; i++)
                {
                    var entry = session.Stock[i];
                    if (entry.Item == null || category >= 0 && (int)entry.Item.Category != category) continue;
                    int index = model.Stock.Count; var row = Row(stockPool, index);
                    row.ItemId = entry.Item.IdString; row.Name = Name(entry.Item); row.Icon = entry.Item.Icon;
                    row.Price = session.BuyPriceOf(i); row.Quantity = entry.Count;
                    stockIndices.Add(i); model.Stock.Add(row);
                    if (row.ItemId == stockId) model.SelectedStockIndex = index;
                }
                for (int slot = 0; slot < InventoryModel.MaxSlots; slot++)
                {
                    var stack = inventory.Get(slot);
                    if (stack.IsEmpty || category >= 0 && (int)stack.Item.Category != category) continue;
                    int index = model.Cargo.Count; var row = Row(cargoPool, index);
                    row.ItemId = stack.OriginalId.IsNone ? stack.Item.IdString : stack.OriginalId.Value;
                    row.Name = Name(stack.Item); row.Icon = stack.Item.Icon; row.Price = session.SellPriceOf(stack.Item); row.Quantity = stack.Count;
                    cargoSlots.Add(slot); model.Cargo.Add(row);
                    if (slot == cargoSlot && row.ItemId == cargoId) model.SelectedCargoIndex = index;
                }
                model.MerchantName = string.IsNullOrWhiteSpace(session.Merchant.DisplayName) ? session.Merchant.IdString : session.Merchant.DisplayName;
                model.Wallet = wallet.Credits; model.WalletText = wallet.Credits.ToString("N0", CultureInfo.InvariantCulture);
                model.Refused = session.Refused; model.RefusalReason = session.RefusalReason;
                RefreshDetails(); model.NotifyCategories(); model.NotifyStock(); model.NotifyCargo();
            }
            finally { refreshing = false; }
        }
        void RefreshDetails()
        {
            if (session == null) return;
            int stock = (uint)model.SelectedStockIndex < stockIndices.Count ? stockIndices[model.SelectedStockIndex] : -1;
            int cargo = (uint)model.SelectedCargoIndex < cargoSlots.Count ? cargoSlots[model.SelectedCargoIndex] : -1;
            ItemDefinition item = stock >= 0 ? session.Stock[stock].Item : cargo >= 0 ? inventory.Get(cargo).Item : null;
            int price = stock >= 0 ? session.BuyPriceOf(stock) : item != null ? session.SellPriceOf(item) : 0;
            int available = stock >= 0 ? session.Stock[stock].Count : cargo >= 0 ? inventory.Get(cargo).Count : 0;
            int limit = price > 0 ? int.MaxValue / price : int.MaxValue;
            model.MaxQuantity = item == null ? 0 : Math.Max(0, Math.Min(available == -1 ? int.MaxValue : available, limit));
            model.Quantity = Math.Max(1, Math.Min(model.Quantity, Math.Max(1, model.MaxQuantity)));
            model.DetailName = item == null ? "Select an item" : Name(item); model.DetailDescription = item?.Description ?? "";
            model.BuyTotal = stock >= 0 ? price * model.Quantity : 0; model.SellTotal = cargo >= 0 ? price * model.Quantity : 0;
            bool canBuy = session.CanBuy(stock, model.Quantity, out string buyReason);
            bool canSell = session.CanSell(cargo, model.Quantity, out string sellReason);
            model.CanBuy = canBuy; model.CanSell = canSell;
            model.BuyBlockedReason = canBuy ? "" : stock < 0 && !session.Refused ? "Select stock to buy" : buyReason;
            model.SellBlockedReason = canSell ? "" : cargo < 0 && !session.Refused ? "Select cargo to sell" : sellReason;
            for (int i = 0; i < model.Stock.Count; i++) model.Stock[i].IsSelected = i == model.SelectedStockIndex;
            for (int i = 0; i < model.Cargo.Count; i++) model.Cargo[i].IsSelected = i == model.SelectedCargoIndex;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Unbind();
        }
    }
}
