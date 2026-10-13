using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class BlueprintRowViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BlueprintIdChanged = new BindablePropertyChangedEventArgs(nameof(BlueprintId));
        string blueprintId;
        [CreateProperty] public string BlueprintId { get => blueprintId; set => Set(ref blueprintId, value, BlueprintIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs NameChanged = new BindablePropertyChangedEventArgs(nameof(Name));
        string name;
        [CreateProperty] public string Name { get => name; set => Set(ref name, value, NameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BlockCountChanged = new BindablePropertyChangedEventArgs(nameof(BlockCount));
        int blockCount;
        [CreateProperty] public int BlockCount { get => blockCount; set => Set(ref blockCount, value, BlockCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ReadOnlyChanged = new BindablePropertyChangedEventArgs(nameof(ReadOnly));
        bool readOnly;
        [CreateProperty] public bool ReadOnly { get => readOnly; set => Set(ref readOnly, value, ReadOnlyChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
