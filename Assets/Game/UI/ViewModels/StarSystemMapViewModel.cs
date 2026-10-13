using Game.UI.Data;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class StarSystemMapViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DataChanged = new BindablePropertyChangedEventArgs(nameof(Data));
        MapData data;
        [CreateProperty] public MapData Data { get => data; set => Set(ref data, value, DataChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DataVersionChanged = new BindablePropertyChangedEventArgs(nameof(DataVersion));
        int dataVersion;
        [CreateProperty] public int DataVersion { get => dataVersion; set => Set(ref dataVersion, value, DataVersionChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedIndexChanged = new BindablePropertyChangedEventArgs(nameof(SelectedIndex));
        int selectedIndex = -1;
        [CreateProperty] public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value, SelectedIndexChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedNameChanged = new BindablePropertyChangedEventArgs(nameof(SelectedName));
        string selectedName;
        [CreateProperty] public string SelectedName { get => selectedName; set => Set(ref selectedName, value, SelectedNameChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedInfoChanged = new BindablePropertyChangedEventArgs(nameof(SelectedInfo));
        string selectedInfo;
        [CreateProperty] public string SelectedInfo { get => selectedInfo; set => Set(ref selectedInfo, value, SelectedInfoChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanSetWaypointChanged = new BindablePropertyChangedEventArgs(nameof(CanSetWaypoint));
        bool canSetWaypoint;
        [CreateProperty] public bool CanSetWaypoint { get => canSetWaypoint; set => Set(ref canSetWaypoint, value, CanSetWaypointChanged); }
    }
}
