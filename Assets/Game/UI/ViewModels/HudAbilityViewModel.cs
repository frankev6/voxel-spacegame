using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class HudAbilityViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs HasAbilityChanged = new BindablePropertyChangedEventArgs(nameof(HasAbility));
        bool hasAbility;
        [CreateProperty] public bool HasAbility { get => hasAbility; set => Set(ref hasAbility, value, HasAbilityChanged); }

        static readonly BindablePropertyChangedEventArgs NameChanged = new BindablePropertyChangedEventArgs(nameof(Name));
        string name;
        [CreateProperty] public string Name { get => name; set => Set(ref name, value, NameChanged); }

        static readonly BindablePropertyChangedEventArgs IconChanged = new BindablePropertyChangedEventArgs(nameof(Icon));
        UnityEngine.Sprite icon;
        [CreateProperty] public UnityEngine.Sprite Icon { get => icon; set => Set(ref icon, value, IconChanged); }

        static readonly BindablePropertyChangedEventArgs CooldownFractionChanged = new BindablePropertyChangedEventArgs(nameof(CooldownFraction));
        float cooldownFraction;
        [CreateProperty] public float CooldownFraction { get => cooldownFraction; set => Set(ref cooldownFraction, value, CooldownFractionChanged); }

        static readonly BindablePropertyChangedEventArgs ChargesChanged = new BindablePropertyChangedEventArgs(nameof(Charges));
        int charges;
        [CreateProperty] public int Charges { get => charges; set => Set(ref charges, value, ChargesChanged); }

        static readonly BindablePropertyChangedEventArgs ReadyChanged = new BindablePropertyChangedEventArgs(nameof(Ready));
        bool ready;
        [CreateProperty] public bool Ready { get => ready; set => Set(ref ready, value, ReadyChanged); }

        static readonly BindablePropertyChangedEventArgs ActiveChanged = new BindablePropertyChangedEventArgs(nameof(Active));
        bool active;
        [CreateProperty] public bool Active { get => active; set => Set(ref active, value, ActiveChanged); }

        static readonly BindablePropertyChangedEventArgs KeyTextChanged = new BindablePropertyChangedEventArgs(nameof(KeyText));
        string keyText;
        [CreateProperty] public string KeyText { get => keyText; set => Set(ref keyText, value, KeyTextChanged); }
    }
}
