using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class MerchantRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ItemIdChanged = new BindablePropertyChangedEventArgs(nameof(ItemId));
        string itemId;
        [CreateProperty] public string ItemId { get => itemId; set => Set(ref itemId, value, ItemIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs NameChanged = new BindablePropertyChangedEventArgs(nameof(Name));
        string name;
        [CreateProperty] public string Name { get => name; set => Set(ref name, value, NameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IconChanged = new BindablePropertyChangedEventArgs(nameof(Icon));
        UnityEngine.Sprite icon;
        [CreateProperty] public UnityEngine.Sprite Icon { get => icon; set => Set(ref icon, value, IconChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs PriceChanged = new BindablePropertyChangedEventArgs(nameof(Price));
        int price;
        [CreateProperty] public int Price { get => price; set => Set(ref price, value, PriceChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuantityChanged = new BindablePropertyChangedEventArgs(nameof(Quantity));
        int quantity;
        [CreateProperty] public int Quantity { get => quantity; set => Set(ref quantity, value, QuantityChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
