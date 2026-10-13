using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorStatsViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RowsChanged = new BindablePropertyChangedEventArgs(nameof(Rows));
        [CreateProperty] public List<StatRowViewModel> Rows { get; } = new List<StatRowViewModel>(12);
        public void NotifyRows() => Notify(RowsChanged);
    }
}
