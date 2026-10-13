using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class InventorySlotViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs IndexChanged = new BindablePropertyChangedEventArgs(nameof(Index));
        int index;
        [CreateProperty] public int Index { get => index; set => Set(ref index, value, IndexChanged); }

        static readonly BindablePropertyChangedEventArgs ItemIdChanged = new BindablePropertyChangedEventArgs(nameof(ItemId));
        string itemId;
        [CreateProperty] public string ItemId { get => itemId; set => Set(ref itemId, value, ItemIdChanged); }

        static readonly BindablePropertyChangedEventArgs IconChanged = new BindablePropertyChangedEventArgs(nameof(Icon));
        UnityEngine.Sprite icon;
        [CreateProperty] public UnityEngine.Sprite Icon { get => icon; set => Set(ref icon, value, IconChanged); }

        static readonly BindablePropertyChangedEventArgs CountChanged = new BindablePropertyChangedEventArgs(nameof(Count));
        int count;
        [CreateProperty] public int Count { get => count; set => Set(ref count, value, CountChanged); }

        static readonly BindablePropertyChangedEventArgs IsEmptyChanged = new BindablePropertyChangedEventArgs(nameof(IsEmpty));
        bool isEmpty = true;
        [CreateProperty] public bool IsEmpty { get => isEmpty; set => Set(ref isEmpty, value, IsEmptyChanged); }

        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }

        static readonly BindablePropertyChangedEventArgs InCapacityChanged = new BindablePropertyChangedEventArgs(nameof(InCapacity));
        bool inCapacity;
        [CreateProperty] public bool InCapacity { get => inCapacity; set => Set(ref inCapacity, value, InCapacityChanged); }
    }
}
