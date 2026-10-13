using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorPaletteItemViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs BlockIdChanged = new BindablePropertyChangedEventArgs(nameof(BlockId));
        string blockId;
        [CreateProperty] public string BlockId { get => blockId; set => Set(ref blockId, value, BlockIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DisplayNameChanged = new BindablePropertyChangedEventArgs(nameof(DisplayName));
        string displayName;
        [CreateProperty] public string DisplayName { get => displayName; set => Set(ref displayName, value, DisplayNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IconChanged = new BindablePropertyChangedEventArgs(nameof(Icon));
        UnityEngine.Sprite icon;
        [CreateProperty] public UnityEngine.Sprite Icon { get => icon; set => Set(ref icon, value, IconChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs AvailableChanged = new BindablePropertyChangedEventArgs(nameof(Available));
        int available;
        [CreateProperty] public int Available { get => available; set => Set(ref available, value, AvailableChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ShowCountChanged = new BindablePropertyChangedEventArgs(nameof(ShowCount));
        bool showCount;
        [CreateProperty] public bool ShowCount { get => showCount; set => Set(ref showCount, value, ShowCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsLockedChanged = new BindablePropertyChangedEventArgs(nameof(IsLocked));
        bool isLocked;
        [CreateProperty] public bool IsLocked { get => isLocked; set => Set(ref isLocked, value, IsLockedChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LockReasonChanged = new BindablePropertyChangedEventArgs(nameof(LockReason));
        string lockReason;
        [CreateProperty] public string LockReason { get => lockReason; set => Set(ref lockReason, value, LockReasonChanged); }
    }
}
