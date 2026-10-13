using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class FleetShipRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs PersistentIdChanged = new BindablePropertyChangedEventArgs(nameof(PersistentId));
        ulong persistentId;
        [CreateProperty] public ulong PersistentId { get => persistentId; set => Set(ref persistentId, value, PersistentIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs NameChanged = new BindablePropertyChangedEventArgs(nameof(Name));
        string name;
        [CreateProperty] public string Name { get => name; set => Set(ref name, value, NameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs HullFractionChanged = new BindablePropertyChangedEventArgs(nameof(HullFraction));
        float hullFraction;
        [CreateProperty] public float HullFraction { get => hullFraction; set => Set(ref hullFraction, value, HullFractionChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsActiveChanged = new BindablePropertyChangedEventArgs(nameof(IsActive));
        bool isActive;
        [CreateProperty] public bool IsActive { get => isActive; set => Set(ref isActive, value, IsActiveChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BlockCountChanged = new BindablePropertyChangedEventArgs(nameof(BlockCount));
        int blockCount;
        [CreateProperty] public int BlockCount { get => blockCount; set => Set(ref blockCount, value, BlockCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
