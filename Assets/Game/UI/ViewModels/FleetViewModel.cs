using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class FleetViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ShipsChanged = new BindablePropertyChangedEventArgs(nameof(Ships));
        [CreateProperty] public List<FleetShipRowViewModel> Ships { get; } = new List<FleetShipRowViewModel>(32);
        public void NotifyShips() => Notify(ShipsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedIndex));
        int selectedIndex = -1;
        [CreateProperty] public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value, SelectedIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DetailNameChanged = new BindablePropertyChangedEventArgs(nameof(DetailName));
        string detailName;
        [CreateProperty] public string DetailName { get => detailName; set => Set(ref detailName, value, DetailNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DetailClassChanged = new BindablePropertyChangedEventArgs(nameof(DetailClass));
        string detailClass;
        [CreateProperty] public string DetailClass { get => detailClass; set => Set(ref detailClass, value, DetailClassChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DetailStatsChanged = new BindablePropertyChangedEventArgs(nameof(DetailStats));
        [CreateProperty] public List<StatRowViewModel> DetailStats { get; } = new List<StatRowViewModel>(12);
        public void NotifyDetailStats() => Notify(DetailStatsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanEditChanged = new BindablePropertyChangedEventArgs(nameof(CanEdit));
        bool canEdit;
        [CreateProperty] public bool CanEdit { get => canEdit; set => Set(ref canEdit, value, CanEditChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs EditBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(EditBlockedReason));
        string editBlockedReason;
        [CreateProperty] public string EditBlockedReason { get => editBlockedReason; set => Set(ref editBlockedReason, value, EditBlockedReasonChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanSetActiveChanged = new BindablePropertyChangedEventArgs(nameof(CanSetActive));
        bool canSetActive;
        [CreateProperty] public bool CanSetActive { get => canSetActive; set => Set(ref canSetActive, value, CanSetActiveChanged); }
    }
}
