using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class InventoryViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CapacityChanged = new BindablePropertyChangedEventArgs(nameof(Capacity));
        int capacity;
        [CreateProperty] public int Capacity { get => capacity; set => Set(ref capacity, value, CapacityChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs UsedSlotsChanged = new BindablePropertyChangedEventArgs(nameof(UsedSlots));
        int usedSlots;
        [CreateProperty] public int UsedSlots { get => usedSlots; set => Set(ref usedSlots, value, UsedSlotsChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs OverflowCountChanged = new BindablePropertyChangedEventArgs(nameof(OverflowCount));
        int overflowCount;
        [CreateProperty] public int OverflowCount { get => overflowCount; set => Set(ref overflowCount, value, OverflowCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SlotsChanged = new BindablePropertyChangedEventArgs(nameof(Slots));
        [CreateProperty] public List<InventorySlotViewModel> Slots { get; } = new List<InventorySlotViewModel>(200);
        public void NotifySlots() => Notify(SlotsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedIndex));
        int selectedIndex = -1;
        [CreateProperty] public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value, SelectedIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedNameChanged = new BindablePropertyChangedEventArgs(nameof(SelectedName));
        string selectedName;
        [CreateProperty] public string SelectedName { get => selectedName; set => Set(ref selectedName, value, SelectedNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedDescriptionChanged = new BindablePropertyChangedEventArgs(nameof(SelectedDescription));
        string selectedDescription;
        [CreateProperty] public string SelectedDescription { get => selectedDescription; set => Set(ref selectedDescription, value, SelectedDescriptionChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedCountChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCount));
        int selectedCount;
        [CreateProperty] public int SelectedCount { get => selectedCount; set => Set(ref selectedCount, value, SelectedCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedValueChanged = new BindablePropertyChangedEventArgs(nameof(SelectedValue));
        int selectedValue;
        [CreateProperty] public int SelectedValue { get => selectedValue; set => Set(ref selectedValue, value, SelectedValueChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs WalletChanged = new BindablePropertyChangedEventArgs(nameof(Wallet));
        long wallet;
        [CreateProperty] public long Wallet { get => wallet; set => Set(ref wallet, value, WalletChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs WalletTextChanged = new BindablePropertyChangedEventArgs(nameof(WalletText));
        string walletText;
        [CreateProperty] public string WalletText { get => walletText; set => Set(ref walletText, value, WalletTextChanged); }

        public InventoryViewModel()
        {
            for (int i = 0; i < 200; i++) Slots.Add(new InventorySlotViewModel());
        }
    }
}
