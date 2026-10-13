using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class ClueRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ClueIdChanged = new BindablePropertyChangedEventArgs(nameof(ClueId));
        string clueId;
        [CreateProperty] public string ClueId { get => clueId; set => Set(ref clueId, value, ClueIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title;
        [CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MysteryTitleChanged = new BindablePropertyChangedEventArgs(nameof(MysteryTitle));
        string mysteryTitle;
        [CreateProperty] public string MysteryTitle { get => mysteryTitle; set => Set(ref mysteryTitle, value, MysteryTitleChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsNewChanged = new BindablePropertyChangedEventArgs(nameof(IsNew));
        bool isNew;
        [CreateProperty] public bool IsNew { get => isNew; set => Set(ref isNew, value, IsNewChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
