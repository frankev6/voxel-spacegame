using Game.Core;
using Game.Input;
using Game.UI.Data;
using Game.UI.ViewModels;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-41 §5.7: event-driven, recycled dialog rows and the owned Dialog input map.</summary>
    public sealed class DialogView : UIScreen
    {
        readonly DialogViewModel viewModel;
        readonly IDialogCommands commands;
        ListView lines, options;
        Image portrait, illustration;
        Label endLabel, continueLabel;
        InputService input;
        InputAction[] choices;
        IVisualElementScheduledItem scroll;
        bool observing;

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
            internal int Index = -1;
            internal OptionRow(VisualElement root, IDialogCommands commands)
            {
                Root = root; this.commands = commands;
                Button = root.Q<Button>("dialog-option-button"); Number = root.Q<Label>("dialog-option-number"); Text = root.Q<Label>("dialog-option-text");
                Button.clicked += Click;
            }
            void Click() { if (Index >= 0) commands.ChooseOption(Index); }
            internal void Clear()
            {
                Index = -1; Root.dataSource = null; Number.text = Text.text = ""; Button.SetEnabled(false);
                Button.RemoveFromClassList("dialog__option--quest"); Button.RemoveFromClassList(UIStyleClasses.IsDisabled);
            }
            internal void Destroy() { Clear(); Button.clicked -= Click; }
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
        public override VisualElement DefaultFocus => options;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            lines = Q<ListView>("dialog-lines"); options = Q<ListView>("dialog-options");
            var lineTemplate = lines.itemTemplate ?? throw new InvalidOperationException("dialog: missing line template");
            var optionTemplate = options.itemTemplate ?? throw new InvalidOperationException("dialog: missing option template");
            lines.itemTemplate = options.itemTemplate = null;
            lines.makeItem = () => { var row = lineTemplate.Instantiate(); row.AddToClassList("dialog-line-container"); row.userData = new LineRow(row); return row; };
            options.makeItem = () => { var row = optionTemplate.Instantiate(); row.userData = new OptionRow(row, commands); return row; };
            lines.bindItem = BindLine; options.bindItem = BindOption;
            lines.unbindItem = (row, _) => ((LineRow)row.userData).Clear();
            options.unbindItem = (row, _) => ((OptionRow)row.userData).Clear();
            lines.destroyItem = row => ((LineRow)row.userData).Clear();
            options.destroyItem = row => ((OptionRow)row.userData).Destroy();
            lines.selectionType = options.selectionType = SelectionType.None;
            portrait = Q<Image>("dialog-portrait"); illustration = Q<Image>("dialog-illustration");
            endLabel = Q<Label>("dialog-end-label"); continueLabel = Q<Label>("dialog-continue-label");
            scroll = Root.schedule.Execute(ScrollPageTop); scroll.Pause();
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
            var row = (OptionRow)root.userData; row.Clear();
            var model = viewModel.Options[index]; root.dataSource = model; row.Index = index;
            row.Number.text = model.NumberLabel;
            row.Text.text = model.Synthetic == 1 ? endLabel.text : model.Synthetic == 2 ? continueLabel.text : model.Text;
            row.Button.SetEnabled(model.IsAvailable);
            row.Button.EnableInClassList("dialog__option--quest", model.IsQuest);
            row.Button.EnableInClassList(UIStyleClasses.IsDisabled, !model.IsAvailable);
        }
        public override void OnPush(object args)
        {
            if (observing) return;
            observing = true; Root.dataSource = viewModel; viewModel.propertyChanged += Changed;
            lines.itemsSource = viewModel.Lines; options.itemsSource = viewModel.Options;
            RefreshImages(); RefreshLines(); options.RefreshItems();
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
            lines.itemsSource = options.itemsSource = null; Root.dataSource = null;
        }
        void Choice(InputAction.CallbackContext context)
        {
            if (!IsOpen || !ReferenceEquals(Navigator.Top, this)) return;
            for (int i = 0; i < choices.Length; i++) if (ReferenceEquals(context.action, choices[i])) { commands.ChooseByNumber(i + 1); return; }
        }
        void Continue(InputAction.CallbackContext context)
        { if (IsOpen && ReferenceEquals(Navigator.Top, this)) commands.Continue(); }
        void Changed(object sender, BindablePropertyChangedEventArgs args)
        {
            if (args.propertyName == nameof(DialogViewModel.Lines)) RefreshLines();
            else if (args.propertyName == nameof(DialogViewModel.Options)) options.RefreshItems();
            else if (args.propertyName == nameof(DialogViewModel.ScrollToLine)) { ScrollPageTop(); scroll.ExecuteLater(0); }
            else if (args.propertyName == nameof(DialogViewModel.Portrait) || args.propertyName == nameof(DialogViewModel.HasPortrait) ||
                args.propertyName == nameof(DialogViewModel.Illustration) || args.propertyName == nameof(DialogViewModel.HasIllustration)) RefreshImages();
        }
        void RefreshImages()
        {
            portrait.sprite = viewModel.Portrait; illustration.sprite = viewModel.Illustration;
            portrait.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasPortrait);
            illustration.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasIllustration);
        }
        void RefreshLines() { lines.RefreshItems(); ScrollPageTop(); scroll.ExecuteLater(0); }
        void ScrollPageTop()
        {
            if (viewModel.ScrollToLine == 0) lines.Q<ScrollView>().scrollOffset = Vector2.zero;
            else if (viewModel.ScrollToLine > 0 && viewModel.ScrollToLine < viewModel.Lines.Count) lines.ScrollToItem(viewModel.ScrollToLine);
        }
    }
}
