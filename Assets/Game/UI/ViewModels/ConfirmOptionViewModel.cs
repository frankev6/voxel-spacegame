using Game.UI.Data;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>SPEC-60 §5.9: plain bindable dialog state, written by its dialog service.</summary>
    public sealed class ConfirmOptionViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs LabelChanged = new BindablePropertyChangedEventArgs(nameof(Label));
        string label = "";
        [Unity.Properties.CreateProperty] public string Label { get => label; set => Set(ref label, value, LabelChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs ElementNameChanged = new BindablePropertyChangedEventArgs(nameof(ElementName));
        string elementName = "";
        [Unity.Properties.CreateProperty] public string ElementName { get => elementName; set => Set(ref elementName, value, ElementNameChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs VariantChanged = new BindablePropertyChangedEventArgs(nameof(Variant));
        ButtonVariant variant = ButtonVariant.Secondary;
        [Unity.Properties.CreateProperty] public ButtonVariant Variant { get => variant; set => Set(ref variant, value, VariantChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs EnabledChanged = new BindablePropertyChangedEventArgs(nameof(Enabled));
        bool enabled = false;
        [Unity.Properties.CreateProperty] public bool Enabled { get => enabled; set => Set(ref enabled, value, EnabledChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs DisabledReasonChanged = new BindablePropertyChangedEventArgs(nameof(DisabledReason));
        string disabledReason = "";
        [Unity.Properties.CreateProperty] public string DisabledReason { get => disabledReason; set => Set(ref disabledReason, value, DisabledReasonChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs VisibleChanged = new BindablePropertyChangedEventArgs(nameof(Visible));
        bool visible = false;
        [Unity.Properties.CreateProperty] public bool Visible { get => visible; set => Set(ref visible, value, VisibleChanged); }
    }
}
