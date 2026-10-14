using System;
using Game.Core;
using UnityEngine.UIElements;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>HUD-003: authored root menu, change-driven availability and app-owned command routing.</summary>
    public sealed class MainMenuView : UIScreen
    {
        readonly MainMenuViewModel viewModel;
        readonly IMainMenuCommands commands;

        public MainMenuView(MainMenuViewModel vm, IMainMenuCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "main-menu";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        Button continueButton, newGameButton;
        Label subtitle, version;
        bool observing;
        public override VisualElement DefaultFocus => viewModel.HasContinue ? continueButton : newGameButton;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            continueButton = Q<Button>("main-menu-continue"); continueButton.clicked += commands.Continue;
            newGameButton = Q<Button>("main-menu-new-game"); newGameButton.clicked += commands.NewGame;
            Q<Button>("main-menu-load").clicked += commands.Load;
            Q<Button>("main-menu-lab").clicked += commands.OpenLab;
            Q<Button>("main-menu-settings").clicked += commands.OpenSettings;
            Q<Button>("main-menu-quit").clicked += commands.Quit;
            subtitle = Q<Label>("main-menu-continue-subtitle");
            version = Q<Label>("main-menu-version");
        }
        public override void OnPush(object args)
        {
            if (!observing) { viewModel.propertyChanged += OnModelChanged; observing = true; }
            Refresh();
        }
        public override void OnPop()
        { if (observing) { viewModel.propertyChanged -= OnModelChanged; observing = false; } }
        public override bool OnBack() => true;
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            continueButton.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasContinue);
            continueButton.SetEnabled(viewModel.HasContinue);
            subtitle.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasContinue);
            subtitle.text = viewModel.ContinueSubtitle;
            version.text = viewModel.VersionText;
        }
    }
}
