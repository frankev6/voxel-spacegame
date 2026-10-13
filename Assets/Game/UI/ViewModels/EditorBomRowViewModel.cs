using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorBomRowViewModel : ViewModelBase
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
        static readonly BindablePropertyChangedEventArgs RequiredChanged = new BindablePropertyChangedEventArgs(nameof(Required));
        int required;
        [CreateProperty] public int Required { get => required; set => Set(ref required, value, RequiredChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RefundedChanged = new BindablePropertyChangedEventArgs(nameof(Refunded));
        int refunded;
        [CreateProperty] public int Refunded { get => refunded; set => Set(ref refunded, value, RefundedChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs AvailableChanged = new BindablePropertyChangedEventArgs(nameof(Available));
        int available;
        [CreateProperty] public int Available { get => available; set => Set(ref available, value, AvailableChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ShortfallChanged = new BindablePropertyChangedEventArgs(nameof(Shortfall));
        int shortfall;
        [CreateProperty] public int Shortfall { get => shortfall; set => Set(ref shortfall, value, ShortfallChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LostDamagedChanged = new BindablePropertyChangedEventArgs(nameof(LostDamaged));
        int lostDamaged;
        [CreateProperty] public int LostDamaged { get => lostDamaged; set => Set(ref lostDamaged, value, LostDamagedChanged); }
    }
}
