using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class QuestRowViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs QuestIdChanged = new BindablePropertyChangedEventArgs(nameof(QuestId));
        string questId;
        [CreateProperty] public string QuestId { get => questId; set => Set(ref questId, value, QuestIdChanged); }

        static readonly BindablePropertyChangedEventArgs TitleChanged = new BindablePropertyChangedEventArgs(nameof(Title));
        string title;
        [CreateProperty] public string Title { get => title; set => Set(ref title, value, TitleChanged); }

        static readonly BindablePropertyChangedEventArgs StageTextChanged = new BindablePropertyChangedEventArgs(nameof(StageText));
        string stageText;
        [CreateProperty] public string StageText { get => stageText; set => Set(ref stageText, value, StageTextChanged); }

        static readonly BindablePropertyChangedEventArgs IsTrackedChanged = new BindablePropertyChangedEventArgs(nameof(IsTracked));
        bool isTracked;
        [CreateProperty] public bool IsTracked { get => isTracked; set => Set(ref isTracked, value, IsTrackedChanged); }

        static readonly BindablePropertyChangedEventArgs IsCompletedChanged = new BindablePropertyChangedEventArgs(nameof(IsCompleted));
        bool isCompleted;
        [CreateProperty] public bool IsCompleted { get => isCompleted; set => Set(ref isCompleted, value, IsCompletedChanged); }

        static readonly BindablePropertyChangedEventArgs IsSelectedChanged = new BindablePropertyChangedEventArgs(nameof(IsSelected));
        bool isSelected;
        [CreateProperty] public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, IsSelectedChanged); }
    }
}
