using Game.UI.Views;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class SaveSlotsViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs ModeChanged = new BindablePropertyChangedEventArgs(nameof(Mode));
        SaveSlotsMode mode;
        [CreateProperty] public SaveSlotsMode Mode { get => mode; set => Set(ref mode, value, ModeChanged); }

        static readonly BindablePropertyChangedEventArgs SlotsChanged = new BindablePropertyChangedEventArgs(nameof(Slots));
        [CreateProperty] public List<SaveSlotItemViewModel> Slots { get; } = new List<SaveSlotItemViewModel>(64);
        public void NotifySlots() => Notify(SlotsChanged);

        static readonly BindablePropertyChangedEventArgs SelectedIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedIndex));
        int selectedIndex = -1;
        [CreateProperty] public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value, SelectedIndexChanged); }

        static readonly BindablePropertyChangedEventArgs CanLoadChanged = new BindablePropertyChangedEventArgs(nameof(CanLoad));
        bool canLoad;
        [CreateProperty] public bool CanLoad { get => canLoad; set => Set(ref canLoad, value, CanLoadChanged); }

        static readonly BindablePropertyChangedEventArgs CanSaveChanged = new BindablePropertyChangedEventArgs(nameof(CanSave));
        bool canSave;
        [CreateProperty] public bool CanSave { get => canSave; set => Set(ref canSave, value, CanSaveChanged); }

        static readonly BindablePropertyChangedEventArgs CanDeleteChanged = new BindablePropertyChangedEventArgs(nameof(CanDelete));
        bool canDelete;
        [CreateProperty] public bool CanDelete { get => canDelete; set => Set(ref canDelete, value, CanDeleteChanged); }

        static readonly BindablePropertyChangedEventArgs EmptyTextChanged = new BindablePropertyChangedEventArgs(nameof(EmptyText));
        string emptyText;
        [CreateProperty] public string EmptyText { get => emptyText; set => Set(ref emptyText, value, EmptyTextChanged); }
    }
}
