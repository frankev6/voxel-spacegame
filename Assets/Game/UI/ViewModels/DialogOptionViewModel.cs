using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class DialogOptionViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs NumberChanged = new BindablePropertyChangedEventArgs(nameof(Number));
        int number;
        [CreateProperty] public int Number { get => number; set => Set(ref number, value, NumberChanged); }

        static readonly BindablePropertyChangedEventArgs TextChanged = new BindablePropertyChangedEventArgs(nameof(Text));
        string text;
        [CreateProperty] public string Text { get => text; set => Set(ref text, value, TextChanged); }

        static readonly BindablePropertyChangedEventArgs EnabledChanged = new BindablePropertyChangedEventArgs(nameof(Enabled));
        bool enabled;
        [CreateProperty] public bool Enabled { get => enabled; set => Set(ref enabled, value, EnabledChanged); }

        static readonly BindablePropertyChangedEventArgs DisabledReasonChanged = new BindablePropertyChangedEventArgs(nameof(DisabledReason));
        string disabledReason;
        [CreateProperty] public string DisabledReason { get => disabledReason; set => Set(ref disabledReason, value, DisabledReasonChanged); }
    }
}
