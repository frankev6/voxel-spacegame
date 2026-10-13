using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class RebindRowViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs ActionPathChanged = new BindablePropertyChangedEventArgs(nameof(ActionPath));
        string actionPath;
        [CreateProperty] public string ActionPath { get => actionPath; set => Set(ref actionPath, value, ActionPathChanged); }

        static readonly BindablePropertyChangedEventArgs DisplayNameChanged = new BindablePropertyChangedEventArgs(nameof(DisplayName));
        string displayName;
        [CreateProperty] public string DisplayName { get => displayName; set => Set(ref displayName, value, DisplayNameChanged); }

        static readonly BindablePropertyChangedEventArgs BindingTextChanged = new BindablePropertyChangedEventArgs(nameof(BindingText));
        string bindingText;
        [CreateProperty] public string BindingText { get => bindingText; set => Set(ref bindingText, value, BindingTextChanged); }

        static readonly BindablePropertyChangedEventArgs IsListeningChanged = new BindablePropertyChangedEventArgs(nameof(IsListening));
        bool isListening;
        [CreateProperty] public bool IsListening { get => isListening; set => Set(ref isListening, value, IsListeningChanged); }
    }
}
