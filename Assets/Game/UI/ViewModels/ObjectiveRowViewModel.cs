using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class ObjectiveRowViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs TextChanged = new BindablePropertyChangedEventArgs(nameof(Text));
        string text;
        [CreateProperty] public string Text { get => text; set => Set(ref text, value, TextChanged); }

        static readonly BindablePropertyChangedEventArgs IsDoneChanged = new BindablePropertyChangedEventArgs(nameof(IsDone));
        bool isDone;
        [CreateProperty] public bool IsDone { get => isDone; set => Set(ref isDone, value, IsDoneChanged); }
    }
}
