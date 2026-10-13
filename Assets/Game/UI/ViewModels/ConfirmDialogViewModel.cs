using Game.UI.Data;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>SPEC-60 §5.9: plain bindable dialog state, written by its dialog service.</summary>
    public sealed class ConfirmDialogViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title = "";
        [Unity.Properties.CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }
        [Game.Core.StaticStateOk("immutable property-name notification cache")]
        static readonly BindablePropertyChangedEventArgs BodyChanged = new BindablePropertyChangedEventArgs(nameof(Body));
        string body = "";
        [Unity.Properties.CreateProperty] public string Body { get => body; set => Set(ref body, value, BodyChanged); }
        internal readonly ConfirmOptionViewModel[] Options = { new ConfirmOptionViewModel(), new ConfirmOptionViewModel(), new ConfirmOptionViewModel() };
        internal int DefaultFocusIndex;
    }
}
