using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class SaveSlotItemViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SlotIdChanged = new BindablePropertyChangedEventArgs(nameof(SlotId));
        string slotId;
        [CreateProperty] public string SlotId { get => slotId; set => Set(ref slotId, value, SlotIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title;
        [CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs LocationTextChanged = new BindablePropertyChangedEventArgs(nameof(LocationText));
        string locationText;
        [CreateProperty] public string LocationText { get => locationText; set => Set(ref locationText, value, LocationTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TimestampTextChanged = new BindablePropertyChangedEventArgs(nameof(TimestampText));
        string timestampText;
        [CreateProperty] public string TimestampText { get => timestampText; set => Set(ref timestampText, value, TimestampTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs PlaytimeTextChanged = new BindablePropertyChangedEventArgs(nameof(PlaytimeText));
        string playtimeText;
        [CreateProperty] public string PlaytimeText { get => playtimeText; set => Set(ref playtimeText, value, PlaytimeTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsAutosaveChanged = new BindablePropertyChangedEventArgs(nameof(IsAutosave));
        bool isAutosave;
        [CreateProperty] public bool IsAutosave { get => isAutosave; set => Set(ref isAutosave, value, IsAutosaveChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
