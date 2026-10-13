using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorIssueRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IndexChanged = new BindablePropertyChangedEventArgs(nameof(Index));
        int index;
        [CreateProperty] public int Index { get => index; set => Set(ref index, value, IndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TextChanged = new BindablePropertyChangedEventArgs(nameof(Text));
        string text;
        [CreateProperty] public string Text { get => text; set => Set(ref text, value, TextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsErrorChanged = new BindablePropertyChangedEventArgs(nameof(IsError));
        bool isError;
        [CreateProperty] public bool IsError { get => isError; set => Set(ref isError, value, IsErrorChanged); }
    }
}
