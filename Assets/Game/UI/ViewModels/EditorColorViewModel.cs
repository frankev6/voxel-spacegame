using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorColorViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IndexChanged = new BindablePropertyChangedEventArgs(nameof(Index));
        int index;
        [CreateProperty] public int Index { get => index; set => Set(ref index, value, IndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SwatchChanged = new BindablePropertyChangedEventArgs(nameof(Swatch));
        UnityEngine.Color swatch;
        [CreateProperty] public UnityEngine.Color Swatch { get => swatch; set => Set(ref swatch, value, SwatchChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
