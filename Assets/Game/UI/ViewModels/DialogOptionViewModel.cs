using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Dialog option data (SPEC-41 §4.9), preserving SPEC-61 §4.2's staged properties.</summary>
    public sealed class DialogOptionViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs NumberChanged = new BindablePropertyChangedEventArgs(nameof(Number));
        int number;
        [CreateProperty] public int Number { get => number; set => Set(ref number, value, NumberChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs TextChanged = new BindablePropertyChangedEventArgs(nameof(Text));
        string text;
        [CreateProperty] public string Text { get => text; set => Set(ref text, value, TextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs EnabledChanged = new BindablePropertyChangedEventArgs(nameof(Enabled));
        bool enabled;
        [CreateProperty] public bool Enabled { get => enabled; set => Set(ref enabled, value, EnabledChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs DisabledReasonChanged = new BindablePropertyChangedEventArgs(nameof(DisabledReason));
        string disabledReason;
        [CreateProperty] public string DisabledReason { get => disabledReason; set => Set(ref disabledReason, value, DisabledReasonChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs NumberLabelChanged = new BindablePropertyChangedEventArgs(nameof(NumberLabel));
        string numberLabel;
        [CreateProperty] public string NumberLabel { get => numberLabel; set => Set(ref numberLabel, value, NumberLabelChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsAvailableChanged = new BindablePropertyChangedEventArgs(nameof(IsAvailable));
        bool isAvailable;
        [CreateProperty] public bool IsAvailable { get => isAvailable; set => Set(ref isAvailable, value, IsAvailableChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs IsQuestChanged = new BindablePropertyChangedEventArgs(nameof(IsQuest));
        bool isQuest;
        [CreateProperty] public bool IsQuest { get => isQuest; set => Set(ref isQuest, value, IsQuestChanged); }

        public int OptionId;
        public byte Synthetic;
    }
}
