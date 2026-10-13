using Game.UI.Data;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>SPEC-60 §5.9: plain bindable dialog state, written by its dialog service.</summary>
    public sealed class TextPromptViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title = "";
        [Unity.Properties.CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs LabelChanged = new BindablePropertyChangedEventArgs(nameof(Label));
        string label = "";
        [Unity.Properties.CreateProperty] public string Label { get => label; set => Set(ref label, value, LabelChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs TextChanged = new BindablePropertyChangedEventArgs(nameof(Text));
        string text = "";
        [Unity.Properties.CreateProperty] public string Text { get => text; set => Set(ref text, value, TextChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs ErrorChanged = new BindablePropertyChangedEventArgs(nameof(Error));
        string error = null;
        [Unity.Properties.CreateProperty] public string Error { get => error; set => Set(ref error, value, ErrorChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs OkLabelChanged = new BindablePropertyChangedEventArgs(nameof(OkLabel));
        string okLabel = "OK";
        [Unity.Properties.CreateProperty] public string OkLabel { get => okLabel; set => Set(ref okLabel, value, OkLabelChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs CancelLabelChanged = new BindablePropertyChangedEventArgs(nameof(CancelLabel));
        string cancelLabel = "Cancel";
        [Unity.Properties.CreateProperty] public string CancelLabel { get => cancelLabel; set => Set(ref cancelLabel, value, CancelLabelChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs MaxLengthChanged = new BindablePropertyChangedEventArgs(nameof(MaxLength));
        int maxLength = 32;
        [Unity.Properties.CreateProperty] public int MaxLength { get => maxLength; set => Set(ref maxLength, value, MaxLengthChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs CanSubmitChanged = new BindablePropertyChangedEventArgs(nameof(CanSubmit));
        bool canSubmit = false;
        [Unity.Properties.CreateProperty] public bool CanSubmit { get => canSubmit; set => Set(ref canSubmit, value, CanSubmitChanged); }
    }
}
