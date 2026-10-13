using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class LoadingViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs StatusTextChanged = new BindablePropertyChangedEventArgs(nameof(StatusText));
        string statusText;
        [CreateProperty] public string StatusText { get => statusText; set => Set(ref statusText, value, StatusTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ProgressChanged = new BindablePropertyChangedEventArgs(nameof(Progress));
        float progress;
        [CreateProperty] public float Progress { get => progress; set => Set(ref progress, value, ProgressChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ShowProgressChanged = new BindablePropertyChangedEventArgs(nameof(ShowProgress));
        bool showProgress;
        [CreateProperty] public bool ShowProgress { get => showProgress; set => Set(ref showProgress, value, ShowProgressChanged); }
    }
}
