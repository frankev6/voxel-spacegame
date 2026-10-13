using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>One displayed dialog line with cached change notifications (SPEC-41 §4.9).</summary>
    public sealed class DialogLineViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TextChanged = new BindablePropertyChangedEventArgs(nameof(Text));
        string text;
        [CreateProperty] public string Text { get => text; set => Set(ref text, value, TextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SpeakerNameChanged = new BindablePropertyChangedEventArgs(nameof(SpeakerName));
        string speakerName;
        [CreateProperty] public string SpeakerName { get => speakerName; set => Set(ref speakerName, value, SpeakerNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SpeakerStyleClassChanged = new BindablePropertyChangedEventArgs(nameof(SpeakerStyleClass));
        string speakerStyleClass;
        [CreateProperty] public string SpeakerStyleClass { get => speakerStyleClass; set => Set(ref speakerStyleClass, value, SpeakerStyleClassChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsDraftChanged = new BindablePropertyChangedEventArgs(nameof(IsDraft));
        bool isDraft;
        [CreateProperty] public bool IsDraft { get => isDraft; set => Set(ref isDraft, value, IsDraftChanged); }
    }
}
