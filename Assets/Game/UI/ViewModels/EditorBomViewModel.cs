using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorBomViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RowsChanged = new BindablePropertyChangedEventArgs(nameof(Rows));
        [CreateProperty] public List<EditorBomRowViewModel> Rows { get; } = new List<EditorBomRowViewModel>(64);
        public void NotifyRows() => Notify(RowsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TotalShortfallChanged = new BindablePropertyChangedEventArgs(nameof(TotalShortfall));
        int totalShortfall;
        [CreateProperty] public int TotalShortfall { get => totalShortfall; set => Set(ref totalShortfall, value, TotalShortfallChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs HasShortfallChanged = new BindablePropertyChangedEventArgs(nameof(HasShortfall));
        bool hasShortfall;
        [CreateProperty] public bool HasShortfall { get => hasShortfall; set => Set(ref hasShortfall, value, HasShortfallChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SummaryTextChanged = new BindablePropertyChangedEventArgs(nameof(SummaryText));
        string summaryText;
        [CreateProperty] public string SummaryText { get => summaryText; set => Set(ref summaryText, value, SummaryTextChanged); }
    }
}
