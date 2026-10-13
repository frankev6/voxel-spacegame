using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class BlueprintBrowserViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RowsChanged = new BindablePropertyChangedEventArgs(nameof(Rows));
        [CreateProperty] public List<BlueprintRowViewModel> Rows { get; } = new List<BlueprintRowViewModel>(128);
        public void NotifyRows() => Notify(RowsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedIndex));
        int selectedIndex = -1;
        [CreateProperty] public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value, SelectedIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanLoadChanged = new BindablePropertyChangedEventArgs(nameof(CanLoad));
        bool canLoad;
        [CreateProperty] public bool CanLoad { get => canLoad; set => Set(ref canLoad, value, CanLoadChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanDeleteChanged = new BindablePropertyChangedEventArgs(nameof(CanDelete));
        bool canDelete;
        [CreateProperty] public bool CanDelete { get => canDelete; set => Set(ref canDelete, value, CanDeleteChanged); }
    }
}
