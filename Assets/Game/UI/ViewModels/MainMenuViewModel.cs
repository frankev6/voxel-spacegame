using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class MainMenuViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs HasContinueChanged = new BindablePropertyChangedEventArgs(nameof(HasContinue));
        bool hasContinue;
        [CreateProperty] public bool HasContinue { get => hasContinue; set => Set(ref hasContinue, value, HasContinueChanged); }

        static readonly BindablePropertyChangedEventArgs ContinueSubtitleChanged = new BindablePropertyChangedEventArgs(nameof(ContinueSubtitle));
        string continueSubtitle;
        [CreateProperty] public string ContinueSubtitle { get => continueSubtitle; set => Set(ref continueSubtitle, value, ContinueSubtitleChanged); }

        static readonly BindablePropertyChangedEventArgs VersionTextChanged = new BindablePropertyChangedEventArgs(nameof(VersionText));
        string versionText;
        [CreateProperty] public string VersionText { get => versionText; set => Set(ref versionText, value, VersionTextChanged); }
    }
}
