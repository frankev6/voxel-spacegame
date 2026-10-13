using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class RebindRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ActionPathChanged = new BindablePropertyChangedEventArgs(nameof(ActionPath));
        string actionPath;
        [CreateProperty] public string ActionPath { get => actionPath; set => Set(ref actionPath, value, ActionPathChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DisplayNameChanged = new BindablePropertyChangedEventArgs(nameof(DisplayName));
        string displayName;
        [CreateProperty] public string DisplayName { get => displayName; set => Set(ref displayName, value, DisplayNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BindingTextChanged = new BindablePropertyChangedEventArgs(nameof(BindingText));
        string bindingText;
        [CreateProperty] public string BindingText { get => bindingText; set => Set(ref bindingText, value, BindingTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsListeningChanged = new BindablePropertyChangedEventArgs(nameof(IsListening));
        bool isListening;
        [CreateProperty] public bool IsListening { get => isListening; set => Set(ref isListening, value, IsListeningChanged); }
    }
}
