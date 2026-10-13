using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class ShipLogViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedTabChanged = new BindablePropertyChangedEventArgs(nameof(SelectedTab));
        int selectedTab;
        [CreateProperty] public int SelectedTab { get => selectedTab; set => Set(ref selectedTab, value, SelectedTabChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ShowCompletedChanged = new BindablePropertyChangedEventArgs(nameof(ShowCompleted));
        bool showCompleted;
        [CreateProperty] public bool ShowCompleted { get => showCompleted; set => Set(ref showCompleted, value, ShowCompletedChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuestsChanged = new BindablePropertyChangedEventArgs(nameof(Quests));
        [CreateProperty] public List<QuestRowViewModel> Quests { get; } = new List<QuestRowViewModel>(64);
        public void NotifyQuests() => Notify(QuestsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedQuestChanged = new BindablePropertyChangedEventArgs(nameof(SelectedQuest));
        int selectedQuest = -1;
        [CreateProperty] public int SelectedQuest { get => selectedQuest; set => Set(ref selectedQuest, value, SelectedQuestChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuestTitleChanged = new BindablePropertyChangedEventArgs(nameof(QuestTitle));
        string questTitle;
        [CreateProperty] public string QuestTitle { get => questTitle; set => Set(ref questTitle, value, QuestTitleChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuestStageTextChanged = new BindablePropertyChangedEventArgs(nameof(QuestStageText));
        string questStageText;
        [CreateProperty] public string QuestStageText { get => questStageText; set => Set(ref questStageText, value, QuestStageTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuestDescriptionChanged = new BindablePropertyChangedEventArgs(nameof(QuestDescription));
        string questDescription;
        [CreateProperty] public string QuestDescription { get => questDescription; set => Set(ref questDescription, value, QuestDescriptionChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ObjectivesChanged = new BindablePropertyChangedEventArgs(nameof(Objectives));
        [CreateProperty] public List<ObjectiveRowViewModel> Objectives { get; } = new List<ObjectiveRowViewModel>(16);
        public void NotifyObjectives() => Notify(ObjectivesChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs QuestLogChanged = new BindablePropertyChangedEventArgs(nameof(QuestLog));
        [CreateProperty] public List<string> QuestLog { get; } = new List<string>(32);
        public void NotifyQuestLog() => Notify(QuestLogChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CluesChanged = new BindablePropertyChangedEventArgs(nameof(Clues));
        [CreateProperty] public List<ClueRowViewModel> Clues { get; } = new List<ClueRowViewModel>(256);
        public void NotifyClues() => Notify(CluesChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedClueChanged = new BindablePropertyChangedEventArgs(nameof(SelectedClue));
        int selectedClue = -1;
        [CreateProperty] public int SelectedClue { get => selectedClue; set => Set(ref selectedClue, value, SelectedClueChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ClueBodyChanged = new BindablePropertyChangedEventArgs(nameof(ClueBody));
        string clueBody;
        [CreateProperty] public string ClueBody { get => clueBody; set => Set(ref clueBody, value, ClueBodyChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs MysteriesChanged = new BindablePropertyChangedEventArgs(nameof(Mysteries));
        [CreateProperty] public List<MysteryRowViewModel> Mysteries { get; } = new List<MysteryRowViewModel>(16);
        public void NotifyMysteries() => Notify(MysteriesChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs JournalChanged = new BindablePropertyChangedEventArgs(nameof(Journal));
        [CreateProperty] public List<JournalRowViewModel> Journal { get; } = new List<JournalRowViewModel>(64);
        public void NotifyJournal() => Notify(JournalChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs RumorsChanged = new BindablePropertyChangedEventArgs(nameof(Rumors));
        [CreateProperty] public List<RumorRowViewModel> Rumors { get; } = new List<RumorRowViewModel>(64);
        public void NotifyRumors() => Notify(RumorsChanged);
    }
}
