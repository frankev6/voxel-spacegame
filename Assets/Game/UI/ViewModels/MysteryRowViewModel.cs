using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class MysteryRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title;
        [CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs KnownChanged = new BindablePropertyChangedEventArgs(nameof(Known));
        int known;
        [CreateProperty] public int Known { get => known; set => Set(ref known, value, KnownChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RequiredChanged = new BindablePropertyChangedEventArgs(nameof(Required));
        int required;
        [CreateProperty] public int Required { get => required; set => Set(ref required, value, RequiredChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SolvedChanged = new BindablePropertyChangedEventArgs(nameof(Solved));
        bool solved;
        [CreateProperty] public bool Solved { get => solved; set => Set(ref solved, value, SolvedChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SolvedTextChanged = new BindablePropertyChangedEventArgs(nameof(SolvedText));
        string solvedText;
        [CreateProperty] public string SolvedText { get => solvedText; set => Set(ref solvedText, value, SolvedTextChanged); }
    }
}
