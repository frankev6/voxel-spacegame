using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class MerchantViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs MerchantNameChanged = new BindablePropertyChangedEventArgs(nameof(MerchantName));
        string merchantName;
        [CreateProperty] public string MerchantName { get => merchantName; set => Set(ref merchantName, value, MerchantNameChanged); }

        static readonly BindablePropertyChangedEventArgs WalletChanged = new BindablePropertyChangedEventArgs(nameof(Wallet));
        long wallet;
        [CreateProperty] public long Wallet { get => wallet; set => Set(ref wallet, value, WalletChanged); }

        static readonly BindablePropertyChangedEventArgs WalletTextChanged = new BindablePropertyChangedEventArgs(nameof(WalletText));
        string walletText;
        [CreateProperty] public string WalletText { get => walletText; set => Set(ref walletText, value, WalletTextChanged); }

        static readonly BindablePropertyChangedEventArgs RefusedChanged = new BindablePropertyChangedEventArgs(nameof(Refused));
        bool refused;
        [CreateProperty] public bool Refused { get => refused; set => Set(ref refused, value, RefusedChanged); }

        static readonly BindablePropertyChangedEventArgs RefusalReasonChanged = new BindablePropertyChangedEventArgs(nameof(RefusalReason));
        string refusalReason;
        [CreateProperty] public string RefusalReason { get => refusalReason; set => Set(ref refusalReason, value, RefusalReasonChanged); }

        static readonly BindablePropertyChangedEventArgs CategoriesChanged = new BindablePropertyChangedEventArgs(nameof(Categories));
        [CreateProperty] public List<string> Categories { get; } = new List<string>(16);
        public void NotifyCategories() => Notify(CategoriesChanged);

        static readonly BindablePropertyChangedEventArgs SelectedCategoryChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCategory));
        int selectedCategory;
        [CreateProperty] public int SelectedCategory { get => selectedCategory; set => Set(ref selectedCategory, value, SelectedCategoryChanged); }

        static readonly BindablePropertyChangedEventArgs StockChanged = new BindablePropertyChangedEventArgs(nameof(Stock));
        [CreateProperty] public List<MerchantRowViewModel> Stock { get; } = new List<MerchantRowViewModel>(128);
        public void NotifyStock() => Notify(StockChanged);

        static readonly BindablePropertyChangedEventArgs CargoChanged = new BindablePropertyChangedEventArgs(nameof(Cargo));
        [CreateProperty] public List<MerchantRowViewModel> Cargo { get; } = new List<MerchantRowViewModel>(200);
        public void NotifyCargo() => Notify(CargoChanged);

        static readonly BindablePropertyChangedEventArgs SelectedStockIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedStockIndex));
        int selectedStockIndex = -1;
        [CreateProperty] public int SelectedStockIndex { get => selectedStockIndex; set => Set(ref selectedStockIndex, value, SelectedStockIndexChanged); }

        static readonly BindablePropertyChangedEventArgs SelectedCargoIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCargoIndex));
        int selectedCargoIndex = -1;
        [CreateProperty] public int SelectedCargoIndex { get => selectedCargoIndex; set => Set(ref selectedCargoIndex, value, SelectedCargoIndexChanged); }

        static readonly BindablePropertyChangedEventArgs QuantityChanged = new BindablePropertyChangedEventArgs(nameof(Quantity));
        int quantity = 1;
        [CreateProperty] public int Quantity { get => quantity; set => Set(ref quantity, value, QuantityChanged); }

        static readonly BindablePropertyChangedEventArgs MaxQuantityChanged = new BindablePropertyChangedEventArgs(nameof(MaxQuantity));
        int maxQuantity;
        [CreateProperty] public int MaxQuantity { get => maxQuantity; set => Set(ref maxQuantity, value, MaxQuantityChanged); }

        static readonly BindablePropertyChangedEventArgs BuyTotalChanged = new BindablePropertyChangedEventArgs(nameof(BuyTotal));
        int buyTotal;
        [CreateProperty] public int BuyTotal { get => buyTotal; set => Set(ref buyTotal, value, BuyTotalChanged); }

        static readonly BindablePropertyChangedEventArgs SellTotalChanged = new BindablePropertyChangedEventArgs(nameof(SellTotal));
        int sellTotal;
        [CreateProperty] public int SellTotal { get => sellTotal; set => Set(ref sellTotal, value, SellTotalChanged); }

        static readonly BindablePropertyChangedEventArgs CanBuyChanged = new BindablePropertyChangedEventArgs(nameof(CanBuy));
        bool canBuy;
        [CreateProperty] public bool CanBuy { get => canBuy; set => Set(ref canBuy, value, CanBuyChanged); }

        static readonly BindablePropertyChangedEventArgs CanSellChanged = new BindablePropertyChangedEventArgs(nameof(CanSell));
        bool canSell;
        [CreateProperty] public bool CanSell { get => canSell; set => Set(ref canSell, value, CanSellChanged); }

        static readonly BindablePropertyChangedEventArgs BuyBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(BuyBlockedReason));
        string buyBlockedReason;
        [CreateProperty] public string BuyBlockedReason { get => buyBlockedReason; set => Set(ref buyBlockedReason, value, BuyBlockedReasonChanged); }

        static readonly BindablePropertyChangedEventArgs SellBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(SellBlockedReason));
        string sellBlockedReason;
        [CreateProperty] public string SellBlockedReason { get => sellBlockedReason; set => Set(ref sellBlockedReason, value, SellBlockedReasonChanged); }

        static readonly BindablePropertyChangedEventArgs DetailNameChanged = new BindablePropertyChangedEventArgs(nameof(DetailName));
        string detailName;
        [CreateProperty] public string DetailName { get => detailName; set => Set(ref detailName, value, DetailNameChanged); }

        static readonly BindablePropertyChangedEventArgs DetailDescriptionChanged = new BindablePropertyChangedEventArgs(nameof(DetailDescription));
        string detailDescription;
        [CreateProperty] public string DetailDescription { get => detailDescription; set => Set(ref detailDescription, value, DetailDescriptionChanged); }
    }
}
