using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Narrative.Dialog;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.Presentation.Bridges
{
    /// <summary>SPEC-41 §5.7: pooled dialog models and navigation; the service alone drives the runner.</summary>
    public sealed class DialogPresenter : IDialogCommands, IDisposable
    {
        readonly DialogService service;
        readonly UINavigator navigator;
        readonly IUISoundSink sound;
        readonly Dictionary<string, SpeakerDefinition> speakers = new(StringComparer.Ordinal);
        readonly Dictionary<string, SpeakerDefinition> names = new(StringComparer.Ordinal);
        readonly Dictionary<string, IllustrationDefinition> illustrations = new(StringComparer.Ordinal);
        readonly Stack<DialogLineViewModel> lines = new(32);
        readonly Stack<DialogOptionViewModel> options = new(17);
        readonly List<bool> leaves = new(17);
        readonly string[] numbers = { "", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        bool disposed;

        public DialogPresenter(DialogService service, ContentCatalog content, DialogViewModel model,
            UINavigator navigator, IUISoundSink sound)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            if (content == null) throw new ArgumentNullException(nameof(content));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
            foreach (var speaker in content.All<SpeakerDefinition>())
            {
                speakers.Add(speaker.IdString, speaker);
                foreach (string alias in speaker.FormerIds) speakers.Add(alias, speaker);
                if (!string.IsNullOrEmpty(speaker.DisplayName)) names.Add(speaker.DisplayName, speaker);
            }
            foreach (var illustration in content.All<IllustrationDefinition>())
            {
                illustrations.Add(illustration.IdString, illustration);
                foreach (string alias in illustration.FormerIds) illustrations.Add(alias, illustration);
            }
            Model.Lines.Capacity = Math.Max(Model.Lines.Capacity, 32);
            Model.Options.Capacity = Math.Max(Model.Options.Capacity, 17);
            for (int i = 0; i < 32; i++) lines.Push(new DialogLineViewModel());
            for (int i = 0; i < 17; i++) options.Push(new DialogOptionViewModel());
            View = new DialogView(Model, this);
            navigator.Register(View);
            service.Started += OnStarted; service.LinePresented += OnLine; service.OptionsPresented += OnOptions;
            service.PageCleared += ClearPage; service.WaitingForEndAcknowledge += OnAcknowledge; service.Ended += OnEnded;
            EventBus<SessionReset>.Subscribe(OnSessionReset);
        }
        public DialogViewModel Model { get; }
        public DialogView View { get; }
        public void Tick() { if (!disposed) service.Pump(); }
        void OnStarted(DialogRequest request) { ClearPage(); navigator.Push(View); }
        void ClearOptions()
        {
            foreach (var option in Model.Options) options.Push(option);
            Model.Options.Clear(); leaves.Clear();
            Model.CanContinue = false; Model.IsWaitingForChoice = false;
        }
        void ClearPage()
        {
            foreach (var line in Model.Lines) lines.Push(line);
            Model.Lines.Clear(); ClearOptions();
            Model.Portrait = Model.Illustration = null;
            Model.HasPortrait = Model.HasIllustration = false;
            Model.SpeakerName = Model.LineText = ""; Model.ScrollToLine = 0;
            Model.NotifyLinesChanged(); Model.NotifyOptionsChanged();
        }
        SpeakerDefinition Speaker(string id)
            => !string.IsNullOrEmpty(id) && speakers.TryGetValue(id, out var value) ? value : null;
        void OnLine(DialogLine line)
        {
            var speaker = Speaker(line.SpeakerId) ?? Speaker(line.PortraitId);
            if (!string.IsNullOrEmpty(line.SpeakerName) && names.TryGetValue(line.SpeakerName, out var named)) speaker = named;
            if (!string.IsNullOrEmpty(line.PortraitId))
            { Model.Portrait = Speaker(line.PortraitId)?.Portrait; Model.HasPortrait = Model.Portrait != null; }
            if (!string.IsNullOrEmpty(line.IllustrationId))
            {
                Model.Illustration = illustrations.TryGetValue(line.IllustrationId, out var image) ? image.Image : null;
                Model.HasIllustration = Model.Illustration != null;
            }
            DialogLineViewModel row;
            if (Model.Lines.Count == 32) { row = Model.Lines[0]; Model.Lines.RemoveAt(0); }
            else row = lines.Pop();
            row.Text = line.Text; row.SpeakerName = !string.IsNullOrEmpty(line.SpeakerName) ? line.SpeakerName : speaker?.DisplayName ?? "";
            row.SpeakerStyleClass = speaker?.NameStyleClass ?? "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            row.IsDraft = line.IsDraft;
#else
            row.IsDraft = false;
#endif
            Model.Lines.Add(row); Model.SpeakerName = row.SpeakerName; Model.LineText = row.Text; Model.LineVersion++;
            Model.NotifyLinesChanged();
        }
        DialogOptionViewModel AddOption(int id, string text, bool available, bool quest, bool leave, byte synthetic)
        {
            var row = options.Count > 0 ? options.Pop() : new DialogOptionViewModel();
            int number = Model.Options.Count + 1;
            row.Number = number <= 9 ? number : 0; row.NumberLabel = number <= 9 ? numbers[number] : "";
            row.Text = text; row.IsAvailable = row.Enabled = available; row.DisabledReason = "";
            row.IsQuest = quest; row.OptionId = id; row.Synthetic = synthetic;
            Model.Options.Add(row); leaves.Add(leave); return row;
        }
        void OnOptions(DialogOption[] source, int count)
        {
            ClearOptions(); bool available = false;
            for (int i = 0; i < count; i++)
            {
                var option = source[i];
                if (!option.Available && option.HideWhenUnavailable) continue;
                AddOption(option.Id, option.Text, option.Available, option.IsQuest, option.IsLeave, 0);
                available |= option.Available;
            }
            // The view resolves synthetic labels from its UXML, not from presenter strings.
            if (!available) AddOption(-1, "", true, false, false, 1);
            Model.IsWaitingForChoice = true; Model.NotifyOptionsChanged();
        }
        void OnAcknowledge()
        {
            ClearOptions(); AddOption(-1, "", true, false, false, 2);
            Model.CanContinue = true; Model.NotifyOptionsChanged();
        }
        void OnEnded(DialogEnded result) { if (View.IsOpen) navigator.Pop(View); }
        void OnSessionReset(SessionReset reset) => service.Interrupt();
        public void Choose(int number) => ChooseByNumber(number);
        public void ChooseByNumber(int number)
        {
            if (disposed || !service.IsActive) return;
            if (number >= 1 && number <= 9)
                for (int i = 0; i < Model.Options.Count; i++)
                    if (Model.Options[i].NumberLabel == numbers[number]) { ChooseOption(i); return; }
            sound.Play(UISound.Error);
        }
        public void ChooseOption(int listIndex)
        {
            if (disposed || !service.IsActive) return;
            if (listIndex < 0 || listIndex >= Model.Options.Count || !Model.Options[listIndex].IsAvailable)
            { sound.Play(UISound.Error); return; }
            var row = Model.Options[listIndex];
            if (row.Synthetic == 1) service.AbortByPlayer();
            else if (row.Synthetic == 2) service.AcknowledgeEnd();
            else service.SelectOption(row.OptionId);
        }
        public void Continue()
        {
            if (!disposed && service.IsActive && Model.Options.Count == 1 && Model.Options[0].Synthetic == 2)
                service.AcknowledgeEnd();
        }
        public bool Back()
        {
            if (!disposed && service.IsActive)
                for (int i = 0; i < Model.Options.Count; i++)
                    if (leaves[i] && Model.Options[i].IsAvailable) { ChooseOption(i); break; }
            return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            service.Interrupt(); disposed = true;
            EventBus<SessionReset>.Unsubscribe(OnSessionReset);
            service.Started -= OnStarted; service.LinePresented -= OnLine; service.OptionsPresented -= OnOptions;
            service.PageCleared -= ClearPage; service.WaitingForEndAcknowledge -= OnAcknowledge; service.Ended -= OnEnded;
            ClearPage();
        }
    }
}
