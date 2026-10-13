using Game.UI.Views;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class SaveSlotsViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ModeChanged = new BindablePropertyChangedEventArgs(nameof(Mode));
        SaveSlotsMode mode;
        [CreateProperty] public SaveSlotsMode Mode { get => mode; set => Set(ref mode, value, ModeChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SlotsChanged = new BindablePropertyChangedEventArgs(nameof(Slots));
        [CreateProperty] public List<SaveSlotItemViewModel> Slots { get; } = new List<SaveSlotItemViewModel>(64);
        public void NotifySlots() => Notify(SlotsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedIndex));
        int selectedIndex = -1;
        [CreateProperty] public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value, SelectedIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanLoadChanged = new BindablePropertyChangedEventArgs(nameof(CanLoad));
        bool canLoad;
        [CreateProperty] public bool CanLoad { get => canLoad; set => Set(ref canLoad, value, CanLoadChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanSaveChanged = new BindablePropertyChangedEventArgs(nameof(CanSave));
        bool canSave;
        [CreateProperty] public bool CanSave { get => canSave; set => Set(ref canSave, value, CanSaveChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanDeleteChanged = new BindablePropertyChangedEventArgs(nameof(CanDelete));
        bool canDelete;
        [CreateProperty] public bool CanDelete { get => canDelete; set => Set(ref canDelete, value, CanDeleteChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs EmptyTextChanged = new BindablePropertyChangedEventArgs(nameof(EmptyText));
        string emptyText;
        [CreateProperty] public string EmptyText { get => emptyText; set => Set(ref emptyText, value, EmptyTextChanged); }
    }
}
