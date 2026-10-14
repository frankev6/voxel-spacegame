using System;
using Game.Core;
using Game.Input;
using Game.UI.Controls;
using UnityEngine.UIElements;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>HUD-006: owner-backed pause actions, live input hints and a bounded scrollable action list.</summary>
    public sealed class PauseView : UIScreen
    {
        readonly PauseViewModel viewModel;
        readonly IPauseCommands commands;
        InputService input;
        KeyHint backKey, consoleKey;
        VisualElement hints;
        bool observing;

        public PauseView(PauseViewModel vm, IPauseCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "pause";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        Button resume, save;
        Label blocked;
        public override VisualElement DefaultFocus => resume;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            resume = Q<Button>("pause-resume"); resume.clicked += commands.Resume;
            save = Q<Button>("pause-save"); save.clicked += commands.Save;
            Q<Button>("pause-load").clicked += commands.Load;
            Q<Button>("pause-settings").clicked += commands.OpenSettings;
            Q<Button>("pause-main-menu").clicked += commands.MainMenu;
            Q<Button>("pause-quit").clicked += commands.Quit;
            blocked = Q<Label>("pause-save-blocked");
            hints = Q<VisualElement>("pause-key-hints"); backKey = Q<KeyHint>("pause-back-key"); consoleKey = Q<KeyHint>("pause-console-key");
        }
        public override void OnPush(object args)
        {
            if (!observing) { viewModel.propertyChanged += OnModelChanged; observing = true; }
            if (input == null && Game.Core.Services.TryGet<InputService>(out var active))
            { input = active; input.BindingsChanged += RefreshHints; }
            RefreshHints();
            Refresh();
        }
        public override void OnPop()
        {
            if (observing) { viewModel.propertyChanged -= OnModelChanged; observing = false; }
            if (input != null) { input.BindingsChanged -= RefreshHints; input = null; }
        }
        public override void OnRevealed() { RefreshHints(); Refresh(); }
        void RefreshHints()
        {
            hints.EnableInClassList(UIStyleClasses.IsHidden, input == null);
            if (input == null) return;
            backKey.Refresh(input.GetBindingDisplay); consoleKey.Refresh(input.GetBindingDisplay);
        }
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            save.SetEnabled(viewModel.CanSave);
            save.tooltip = viewModel.SaveBlockedReason; blocked.text = viewModel.SaveBlockedReason;
            blocked.EnableInClassList(UIStyleClasses.IsHidden, viewModel.CanSave);
        }
    }
}
