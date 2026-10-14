using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class StatRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs StatIdChanged = new BindablePropertyChangedEventArgs(nameof(StatId));
        string statId;
        [CreateProperty] public string StatId { get => statId; set => Set(ref statId, value, StatIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LabelChanged = new BindablePropertyChangedEventArgs(nameof(Label));
        string label;
        [CreateProperty] public string Label { get => label; set => Set(ref label, value, LabelChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ValueChanged = new BindablePropertyChangedEventArgs(nameof(Value));
        string statValue;
        [CreateProperty] public string Value { get => statValue; set => Set(ref statValue, value, ValueChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DeltaChanged = new BindablePropertyChangedEventArgs(nameof(Delta));
        string delta;
        [CreateProperty] public string Delta { get => delta; set => Set(ref delta, value, DeltaChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DeltaSignChanged = new BindablePropertyChangedEventArgs(nameof(DeltaSign));
        int deltaSign;
        [CreateProperty] public int DeltaSign { get => deltaSign; set => Set(ref deltaSign, value, DeltaSignChanged); }
    }
}
