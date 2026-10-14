using Game.Core;
using Game.Input;
using Game.UI.Data;
using Game.UI.ViewModels;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>HUD-013 comms host and SPEC-41 §5.7 page/overflow support; event-driven with the owned Dialog input map.</summary>
    public sealed class DialogView : UIScreen
    {
        readonly DialogViewModel viewModel;
        readonly IDialogCommands commands;
        ListView lines, options;
        ScrollView textScroll;
        Label speaker, text;
        Button continueButton;
        VisualElement portraitColumn;
        readonly OptionRow[] optionPool = new OptionRow[9];
        readonly List<OptionRow> optionRows = new(9);
        VisualTreeAsset optionTemplate;
        Image portrait, illustration;
        Label endLabel, continueLabel;
        InputService input;
        InputAction[] choices;
        IVisualElementScheduledItem scroll;
        bool observing;
        bool forceScrollTop;

        sealed class LineRow
        {
            internal readonly VisualElement Root;
            internal readonly Label Speaker, Text;
            internal readonly IVisualElementScheduledItem Fade;
            internal string Style;
            internal LineRow(VisualElement root)
            {
                Root = root; Speaker = root.Q<Label>("dialog-line-speaker"); Text = root.Q<Label>("dialog-line-text");
                Fade = root.schedule.Execute(() => root.RemoveFromClassList("dialog__line--enter")); Fade.Pause();
            }
            internal void Clear()
            {
                Fade.Pause(); Root.dataSource = null;
                if (!string.IsNullOrEmpty(Style)) Speaker.RemoveFromClassList(Style);
                Style = null; Speaker.text = Text.text = "";
                Root.RemoveFromClassList("dialog__line--draft"); Root.RemoveFromClassList("dialog__line--enter");
            }
        }
        sealed class OptionRow
        {
            internal readonly VisualElement Root;
            internal readonly Button Button;
            internal readonly Label Number, Text;
            readonly IDialogCommands commands;
            readonly DialogView owner;
            readonly bool pooled;
            DialogOptionViewModel model;
            internal bool InUse;
            internal int Index = -1;
            internal bool Pooled => pooled;
            internal OptionRow(VisualElement root, DialogView owner, IDialogCommands commands, bool pooled = false)
            {
                Root = root; this.owner = owner; this.commands = commands; this.pooled = pooled;
                Button = root.Q<Button>("dialog-option-button"); Number = root.Q<Label>("dialog-option-number"); Text = root.Q<Label>("dialog-option-text");
                Button.clicked += Click;
            }
            bool Available => model != null && (model.Number > 0 ? model.Enabled : model.IsAvailable);
            void Click()
            {
                if (Index < 0 || !Available || !owner.IsOpen || !ReferenceEquals(owner.Navigator.Top, owner)) return;
                if (model.Number > 0) commands.Choose(model.Number);
                else commands.ChooseOption(Index); // SPEC-41 unnumbered overflow and legacy indexed rows.
            }
            internal void Bind(DialogOptionViewModel item, int index, string endText, string continueText)
            {
                Clear(); model = item; Index = index; Root.dataSource = item;
                Button.name = "dialog-option-" + (index + 1);
                Text.text = item.Synthetic == 1 ? endText : item.Synthetic == 2 ? continueText : item.Text;
                model.propertyChanged += Changed; Refresh();
            }
            void Changed(object sender, BindablePropertyChangedEventArgs args)
            {
                if (model.Synthetic == 0) Text.text = model.Text;
                Refresh();
            }
            void Refresh()
            {
                Number.text = model.Number > 0 ? "[" + model.Number + "]" : model.NumberLabel ?? "";
                Button.SetEnabled(Available); Button.tooltip = model.DisabledReason;
                Button.EnableInClassList("dialog__option--quest", model.IsQuest);
                Button.EnableInClassList(UIStyleClasses.IsDisabled, !Available);
            }
            internal void Clear()
            {
                if (model != null) model.propertyChanged -= Changed;
                model = null;
                Index = -1; Root.dataSource = null; Number.text = Text.text = ""; Button.SetEnabled(false);
                Button.tooltip = null;
                Button.RemoveFromClassList("dialog__option--quest"); Button.RemoveFromClassList(UIStyleClasses.IsDisabled);
            }
            internal void Destroy() { Clear(); InUse = false; if (!pooled) Button.clicked -= Click; }
        }

        public DialogView(DialogViewModel vm, IDialogCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "dialog";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Dialog;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override bool OnBack() => commands.Back();
        public override VisualElement DefaultFocus => continueButton != null && continueButton.enabledSelf ? continueButton : options;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            lines = Q<ListView>("dialog-lines"); options = Q<ListView>("dialog-options");
            var lineTemplate = lines.itemTemplate ?? throw new InvalidOperationException("dialog: missing line template");
            optionTemplate = options.itemTemplate ?? throw new InvalidOperationException("dialog: missing option template");
            lines.itemTemplate = options.itemTemplate = null;
            lines.makeItem = () => { var row = lineTemplate.Instantiate(); row.AddToClassList("dialog-line-container"); row.userData = new LineRow(row); return row; };
            for (int i = 0; i < optionPool.Length; i++) optionPool[i] = CreateOption(true);
            options.makeItem = MakeOption;
            lines.bindItem = BindLine; options.bindItem = BindOption;
            lines.unbindItem = (row, _) => ((LineRow)row.userData).Clear();
            options.unbindItem = (row, _) => ((OptionRow)row.userData).Clear();
            lines.destroyItem = row => ((LineRow)row.userData).Clear();
            options.destroyItem = root =>
            {
                var row = (OptionRow)root.userData; row.Destroy();
                if (!row.Pooled) optionRows.Remove(row);
            };
            lines.selectionType = options.selectionType = SelectionType.None;
            portrait = Q<Image>("dialog-portrait"); illustration = Q<Image>("dialog-illustration");
            portraitColumn = Q<VisualElement>("dialog-portrait-column");
            speaker = Q<Label>("dialog-speaker"); text = Q<Label>("dialog-text-label"); textScroll = Q<ScrollView>("dialog-text");
            continueButton = Q<Button>("dialog-continue");
            continueButton.clicked += () => { if (IsOpen && ReferenceEquals(Navigator.Top, this) && viewModel.CanContinue) commands.Continue(); };
            endLabel = Q<Label>("dialog-end-label"); continueLabel = Q<Label>("dialog-continue-label");
            scroll = Root.schedule.Execute(ScrollPageTop); scroll.Pause();
        }
        OptionRow CreateOption(bool pooled)
        {
            var row = optionTemplate.Instantiate(); var state = new OptionRow(row, this, commands, pooled); optionRows.Add(state);
            row.userData = state; return state;
        }
        VisualElement MakeOption()
        {
            foreach (var row in optionPool)
                if (!row.InUse) { row.InUse = true; return row.Root; }
            // DLG-009 permits more than nine displayed options, with mouse-only overflow.
            // The standard nine-choice host reuses the pool; overflow keeps its existing ListView contract.
            return CreateOption(false).Root;
        }
        bool ReducedMotion()
        {
            for (var element = Root; element != null; element = element.parent)
                if (element.ClassListContains(UIStyleClasses.ReduceMotion)) return true;
            return false;
        }
        void BindLine(VisualElement root, int index)
        {
            var row = (LineRow)root.userData; row.Clear();
            var model = viewModel.Lines[index]; root.dataSource = model;
            row.Text.text = model.Text; row.Speaker.text = model.SpeakerName;
            row.Speaker.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(model.SpeakerName));
            row.Style = model.SpeakerStyleClass;
            if (!string.IsNullOrEmpty(row.Style)) row.Speaker.AddToClassList(row.Style);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            root.EnableInClassList("dialog__line--draft", model.IsDraft);
#endif
            if (!ReducedMotion()) { root.AddToClassList("dialog__line--enter"); row.Fade.ExecuteLater(0); }
        }
        void BindOption(VisualElement root, int index)
        {
            ((OptionRow)root.userData).Bind(viewModel.Options[index], index, endLabel.text, continueLabel.text);
        }
        public override void OnPush(object args)
        {
            if (observing) return;
            observing = true; Root.dataSource = viewModel; viewModel.propertyChanged += Changed;
            lines.itemsSource = viewModel.Lines; options.itemsSource = viewModel.Options;
            forceScrollTop = true;
            RefreshImages(); RefreshText(); RefreshLines(); options.RefreshItems(); RefreshContinue();
            // Detached presenter tests can display without an input service. Production boot always registers it.
            if (Game.Core.Services.TryGet<InputService>(out input))
            {
                var map = input.Controls.Dialog;
                choices = new[] { map.Choice1, map.Choice2, map.Choice3, map.Choice4, map.Choice5, map.Choice6, map.Choice7, map.Choice8, map.Choice9 };
                foreach (var choice in choices) choice.performed += Choice;
                map.Continue.performed += Continue;
            }
        }
        public override void OnPop()
        {
            if (!observing) return;
            observing = false; viewModel.propertyChanged -= Changed;
            if (input != null)
            { foreach (var choice in choices) choice.performed -= Choice; input.Controls.Dialog.Continue.performed -= Continue; }
            input = null; choices = null; scroll.Pause();
            foreach (var row in optionRows) row.Clear();
            lines.itemsSource = options.itemsSource = null; Root.dataSource = null;
        }
        void Choice(InputAction.CallbackContext context)
        {
            if (!IsOpen || !ReferenceEquals(Navigator.Top, this)) return;
            for (int i = 0; i < choices.Length; i++) if (ReferenceEquals(context.action, choices[i])) { commands.Choose(i + 1); return; }
        }
        void Continue(InputAction.CallbackContext context)
        { if (IsOpen && ReferenceEquals(Navigator.Top, this)) commands.Continue(); }
        void Changed(object sender, BindablePropertyChangedEventArgs args)
        {
            if (args.propertyName == nameof(DialogViewModel.Lines)) { RefreshText(); RefreshLines(); }
            else if (args.propertyName == nameof(DialogViewModel.Options)) { options.RefreshItems(); RefreshContinue(); }
            else if (args.propertyName == nameof(DialogViewModel.LineText) || args.propertyName == nameof(DialogViewModel.SpeakerName)) RefreshText();
            else if (args.propertyName == nameof(DialogViewModel.LineVersion)) { forceScrollTop = true; ScrollPageTop(); scroll.ExecuteLater(0); }
            else if (args.propertyName == nameof(DialogViewModel.ScrollToLine)) { forceScrollTop = false; ScrollPageTop(); scroll.ExecuteLater(0); }
            else if (args.propertyName == nameof(DialogViewModel.CanContinue)) RefreshContinue();
            else if (args.propertyName == nameof(DialogViewModel.Portrait) || args.propertyName == nameof(DialogViewModel.HasPortrait) ||
                args.propertyName == nameof(DialogViewModel.Illustration) || args.propertyName == nameof(DialogViewModel.HasIllustration)) RefreshImages();
        }
        void RefreshImages()
        {
            portrait.sprite = viewModel.Portrait; illustration.sprite = viewModel.Illustration;
            portraitColumn.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasPortrait);
            illustration.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasIllustration);
        }
        void RefreshText()
        {
            speaker.text = viewModel.SpeakerName;
            speaker.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(viewModel.SpeakerName));
            text.text = viewModel.LineText;
            // SPEC-41's page collection takes precedence; HUD-001's single-line contract works without a driver.
            bool page = viewModel.Lines.Count != 0;
            lines.EnableInClassList(UIStyleClasses.IsHidden, !page);
            textScroll.EnableInClassList(UIStyleClasses.IsHidden, page);
        }
        void RefreshContinue()
        {
            continueButton.SetEnabled(viewModel.CanContinue);
            continueButton.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.CanContinue);
            bool syntheticContinue = viewModel.CanContinue && viewModel.Options.Count == 1 && viewModel.Options[0].Synthetic == 2;
            options.EnableInClassList(UIStyleClasses.IsHidden, syntheticContinue || viewModel.Options.Count == 0);
        }
        void RefreshLines() { lines.RefreshItems(); ScrollPageTop(); scroll.ExecuteLater(0); }
        void ScrollPageTop()
        {
            textScroll.scrollOffset = Vector2.zero;
            if (forceScrollTop || viewModel.ScrollToLine == 0) lines.Q<ScrollView>().scrollOffset = Vector2.zero;
            else if (viewModel.ScrollToLine > 0 && viewModel.ScrollToLine < viewModel.Lines.Count) lines.ScrollToItem(viewModel.ScrollToLine);
        }
    }
}
