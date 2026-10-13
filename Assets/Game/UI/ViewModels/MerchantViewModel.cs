using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class MerchantViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MerchantNameChanged = new BindablePropertyChangedEventArgs(nameof(MerchantName));
        string merchantName;
        [CreateProperty] public string MerchantName { get => merchantName; set => Set(ref merchantName, value, MerchantNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs WalletChanged = new BindablePropertyChangedEventArgs(nameof(Wallet));
        long wallet;
        [CreateProperty] public long Wallet { get => wallet; set => Set(ref wallet, value, WalletChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs WalletTextChanged = new BindablePropertyChangedEventArgs(nameof(WalletText));
        string walletText;
        [CreateProperty] public string WalletText { get => walletText; set => Set(ref walletText, value, WalletTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RefusedChanged = new BindablePropertyChangedEventArgs(nameof(Refused));
        bool refused;
        [CreateProperty] public bool Refused { get => refused; set => Set(ref refused, value, RefusedChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RefusalReasonChanged = new BindablePropertyChangedEventArgs(nameof(RefusalReason));
        string refusalReason;
        [CreateProperty] public string RefusalReason { get => refusalReason; set => Set(ref refusalReason, value, RefusalReasonChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CategoriesChanged = new BindablePropertyChangedEventArgs(nameof(Categories));
        [CreateProperty] public List<string> Categories { get; } = new List<string>(16);
        public void NotifyCategories() => Notify(CategoriesChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedCategoryChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCategory));
        int selectedCategory;
        [CreateProperty] public int SelectedCategory { get => selectedCategory; set => Set(ref selectedCategory, value, SelectedCategoryChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs StockChanged = new BindablePropertyChangedEventArgs(nameof(Stock));
        [CreateProperty] public List<MerchantRowViewModel> Stock { get; } = new List<MerchantRowViewModel>(128);
        public void NotifyStock() => Notify(StockChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CargoChanged = new BindablePropertyChangedEventArgs(nameof(Cargo));
        [CreateProperty] public List<MerchantRowViewModel> Cargo { get; } = new List<MerchantRowViewModel>(200);
        public void NotifyCargo() => Notify(CargoChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedStockIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedStockIndex));
        int selectedStockIndex = -1;
        [CreateProperty] public int SelectedStockIndex { get => selectedStockIndex; set => Set(ref selectedStockIndex, value, SelectedStockIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedCargoIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCargoIndex));
        int selectedCargoIndex = -1;
        [CreateProperty] public int SelectedCargoIndex { get => selectedCargoIndex; set => Set(ref selectedCargoIndex, value, SelectedCargoIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuantityChanged = new BindablePropertyChangedEventArgs(nameof(Quantity));
        int quantity = 1;
        [CreateProperty] public int Quantity { get => quantity; set => Set(ref quantity, value, QuantityChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MaxQuantityChanged = new BindablePropertyChangedEventArgs(nameof(MaxQuantity));
        int maxQuantity;
        [CreateProperty] public int MaxQuantity { get => maxQuantity; set => Set(ref maxQuantity, value, MaxQuantityChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BuyTotalChanged = new BindablePropertyChangedEventArgs(nameof(BuyTotal));
        int buyTotal;
        [CreateProperty] public int BuyTotal { get => buyTotal; set => Set(ref buyTotal, value, BuyTotalChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SellTotalChanged = new BindablePropertyChangedEventArgs(nameof(SellTotal));
        int sellTotal;
        [CreateProperty] public int SellTotal { get => sellTotal; set => Set(ref sellTotal, value, SellTotalChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanBuyChanged = new BindablePropertyChangedEventArgs(nameof(CanBuy));
        bool canBuy;
        [CreateProperty] public bool CanBuy { get => canBuy; set => Set(ref canBuy, value, CanBuyChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanSellChanged = new BindablePropertyChangedEventArgs(nameof(CanSell));
        bool canSell;
        [CreateProperty] public bool CanSell { get => canSell; set => Set(ref canSell, value, CanSellChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BuyBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(BuyBlockedReason));
        string buyBlockedReason;
        [CreateProperty] public string BuyBlockedReason { get => buyBlockedReason; set => Set(ref buyBlockedReason, value, BuyBlockedReasonChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SellBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(SellBlockedReason));
        string sellBlockedReason;
        [CreateProperty] public string SellBlockedReason { get => sellBlockedReason; set => Set(ref sellBlockedReason, value, SellBlockedReasonChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DetailNameChanged = new BindablePropertyChangedEventArgs(nameof(DetailName));
        string detailName;
        [CreateProperty] public string DetailName { get => detailName; set => Set(ref detailName, value, DetailNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DetailDescriptionChanged = new BindablePropertyChangedEventArgs(nameof(DetailDescription));
        string detailDescription;
        [CreateProperty] public string DetailDescription { get => detailDescription; set => Set(ref detailDescription, value, DetailDescriptionChanged); }
    }
}
