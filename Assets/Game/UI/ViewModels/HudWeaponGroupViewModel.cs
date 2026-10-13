using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class HudWeaponGroupViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs GroupNumberChanged = new BindablePropertyChangedEventArgs(nameof(GroupNumber));
        int groupNumber;
        [CreateProperty] public int GroupNumber { get => groupNumber; set => Set(ref groupNumber, value, GroupNumberChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs WeaponCountChanged = new BindablePropertyChangedEventArgs(nameof(WeaponCount));
        int weaponCount;
        [CreateProperty] public int WeaponCount { get => weaponCount; set => Set(ref weaponCount, value, WeaponCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ReadyFractionChanged = new BindablePropertyChangedEventArgs(nameof(ReadyFraction));
        float readyFraction;
        [CreateProperty] public float ReadyFraction { get => readyFraction; set => Set(ref readyFraction, value, ReadyFractionChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs FiringChanged = new BindablePropertyChangedEventArgs(nameof(Firing));
        bool firing;
        [CreateProperty] public bool Firing { get => firing; set => Set(ref firing, value, FiringChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs KeyTextChanged = new BindablePropertyChangedEventArgs(nameof(KeyText));
        string keyText;
        [CreateProperty] public string KeyText { get => keyText; set => Set(ref keyText, value, KeyTextChanged); }
    }
}
