using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorIssuesViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RowsChanged = new BindablePropertyChangedEventArgs(nameof(Rows));
        [CreateProperty] public List<EditorIssueRowViewModel> Rows { get; } = new List<EditorIssueRowViewModel>(16);
        public void NotifyRows() => Notify(RowsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ErrorCountChanged = new BindablePropertyChangedEventArgs(nameof(ErrorCount));
        int errorCount;
        [CreateProperty] public int ErrorCount { get => errorCount; set => Set(ref errorCount, value, ErrorCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs WarningCountChanged = new BindablePropertyChangedEventArgs(nameof(WarningCount));
        int warningCount;
        [CreateProperty] public int WarningCount { get => warningCount; set => Set(ref warningCount, value, WarningCountChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CanRemoveFloatingChanged = new BindablePropertyChangedEventArgs(nameof(CanRemoveFloating));
        bool canRemoveFloating;
        [CreateProperty] public bool CanRemoveFloating { get => canRemoveFloating; set => Set(ref canRemoveFloating, value, CanRemoveFloatingChanged); }
    }
}
