using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class RumorRowViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs RumorIdChanged = new BindablePropertyChangedEventArgs(nameof(RumorId));
        string rumorId;
        [CreateProperty] public string RumorId { get => rumorId; set => Set(ref rumorId, value, RumorIdChanged); }

        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title;
        [CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }

        static readonly BindablePropertyChangedEventArgs LocationTextChanged = new BindablePropertyChangedEventArgs(nameof(LocationText));
        string locationText;
        [CreateProperty] public string LocationText { get => locationText; set => Set(ref locationText, value, LocationTextChanged); }

        static readonly BindablePropertyChangedEventArgs HasMapTargetChanged = new BindablePropertyChangedEventArgs(nameof(HasMapTarget));
        bool hasMapTarget;
        [CreateProperty] public bool HasMapTarget { get => hasMapTarget; set => Set(ref hasMapTarget, value, HasMapTargetChanged); }
    }
}
