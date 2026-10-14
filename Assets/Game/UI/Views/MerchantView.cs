using Game.Core;
using System;
using System.Globalization;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>HUD-012: two virtualized lists and bounded scrolling tabs; economy remains behind commands.</summary>
    public sealed class MerchantView : UIScreen
    {
        readonly MerchantViewModel viewModel;
        readonly IMerchantCommands commands;
        ListView stock, cargo;
        TabBar tabs;
        SliderInt quantity;
        Button buy, sell, close;
        Label merchant, wallet, detail, description, refusal, buyTotal, sellTotal, buyReason, sellReason;
        readonly int[] selected = new int[1];
        bool observing, updating;

        public MerchantView(MerchantViewModel vm, IMerchantCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "merchant";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => viewModel.Refused ? close : stock;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            stock = Q<ListView>("merchant-stock-list"); cargo = Q<ListView>("merchant-cargo-list");
            Configure(stock, true); Configure(cargo, false);
            stock.selectionChanged += _ => { if (!updating) commands.SelectStock(stock.selectedIndex); };
            cargo.selectionChanged += _ => { if (!updating) commands.SelectCargo(cargo.selectedIndex); };
            tabs = Q<TabBar>("merchant-tabs"); tabs.SelectionChanged += index => { if (!updating) commands.SelectCategory(index); };
            quantity = Q<SliderInt>("merchant-quantity"); quantity.RegisterValueChangedCallback(evt => { if (!updating) commands.SetQuantity(evt.newValue); });
            merchant = Q<Label>("merchant-name"); wallet = Q<Label>("merchant-wallet");
            detail = Q<Label>("merchant-detail-name"); description = Q<Label>("merchant-detail-description");
            refusal = Q<Label>("merchant-refusal"); buyTotal = Q<Label>("merchant-buy-total"); sellTotal = Q<Label>("merchant-sell-total");
            buyReason = Q<Label>("merchant-buy-blocked"); sellReason = Q<Label>("merchant-sell-blocked");
            buy = Q<Button>("merchant-buy"); sell = Q<Button>("merchant-sell"); close = Q<Button>("merchant-close");
            buy.clicked += commands.Buy; sell.clicked += commands.Sell; close.clicked += Close;
        }
        void Configure(ListView list, bool isStock)
        {
            var template = list.itemTemplate ?? throw new InvalidOperationException("merchant: missing row template");
            list.itemTemplate = null; list.makeItem = () => template.Instantiate();
            list.bindItem = (element, index) => BindItem(element, isStock ? viewModel.Stock[index] : viewModel.Cargo[index]);
            list.unbindItem = (element, _) => element.dataSource = null;
            list.itemsSource = isStock ? viewModel.Stock : viewModel.Cargo;
            list.selectionType = SelectionType.Single;
        }
        static void BindItem(VisualElement element, MerchantRowViewModel row)
        {
            element.dataSource = row;
            element.Q<VisualElement>("merchant-row").EnableInClassList(UIStyleClasses.IsSelected, row.IsSelected);
            element.Q<Image>("merchant-row-icon").sprite = row.Icon;
            element.Q<Label>("merchant-row-name").text = row.Name;
            element.Q<Label>("merchant-row-price").text = row.Price.ToString("N0", CultureInfo.InvariantCulture);
            element.Q<Label>("merchant-row-quantity").text = row.Quantity == -1 ? "∞" : row.Quantity.ToString("N0", CultureInfo.InvariantCulture);
        }
        public override void OnPush(object args)
        {
            if (!observing) { viewModel.propertyChanged += Changed; observing = true; }
            Refresh(null);
        }
        public override void OnPop()
        { if (observing) { viewModel.propertyChanged -= Changed; observing = false; } }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh(args.propertyName);
        void Refresh(string property)
        {
            if (updating) return;
            updating = true;
            try
            {
                merchant.text = viewModel.MerchantName; wallet.text = "Credits " + viewModel.WalletText;
                detail.text = viewModel.DetailName; description.text = viewModel.DetailDescription;
                refusal.text = viewModel.RefusalReason; refusal.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.Refused);
                buyTotal.text = "Buy total " + viewModel.BuyTotal.ToString("N0", CultureInfo.InvariantCulture);
                sellTotal.text = "Sell total " + viewModel.SellTotal.ToString("N0", CultureInfo.InvariantCulture);
                buyReason.text = viewModel.BuyBlockedReason; sellReason.text = viewModel.SellBlockedReason;
                buyReason.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(viewModel.BuyBlockedReason));
                sellReason.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(viewModel.SellBlockedReason));
                buy.SetEnabled(viewModel.CanBuy && !viewModel.Refused); sell.SetEnabled(viewModel.CanSell && !viewModel.Refused);
                buy.tooltip = viewModel.BuyBlockedReason; sell.tooltip = viewModel.SellBlockedReason;
                quantity.highValue = Math.Max(1, viewModel.MaxQuantity); quantity.SetValueWithoutNotify(viewModel.Quantity);
                quantity.SetEnabled(viewModel.MaxQuantity > 0 && !viewModel.Refused);
                if (property == null || property == nameof(MerchantViewModel.Categories))
                {
                    var names = new string[viewModel.Categories.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = "merchant-tab-" + i;
                    tabs.SetTabs(viewModel.Categories, names);
                }
                if (property == null || property == nameof(MerchantViewModel.Categories) || property == nameof(MerchantViewModel.SelectedCategory))
                    tabs.SelectedIndex = (uint)viewModel.SelectedCategory < viewModel.Categories.Count && viewModel.SelectedCategory < tabs.TabCount ? viewModel.SelectedCategory : -1;
                if (property == null || property == nameof(MerchantViewModel.Stock) || property == nameof(MerchantViewModel.SelectedStockIndex)) RefreshList(stock, viewModel.SelectedStockIndex);
                if (property == null || property == nameof(MerchantViewModel.Cargo) || property == nameof(MerchantViewModel.SelectedCargoIndex)) RefreshList(cargo, viewModel.SelectedCargoIndex);
            }
            finally { updating = false; }
        }
        void RefreshList(ListView list, int index)
        {
            selected[0] = index; list.SetSelectionWithoutNotify(index >= 0 ? selected : Array.Empty<int>());
            list.RefreshItems();
        }
    }
}
