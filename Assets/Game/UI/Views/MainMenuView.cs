using System;
using Game.Core;
using UnityEngine.UIElements;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
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
        Label subtitle;
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
        }
        public override void OnPush(object args)
        {
            viewModel.propertyChanged += OnModelChanged;
            Refresh();
        }
        public override void OnPop() => viewModel.propertyChanged -= OnModelChanged;
        public override bool OnBack() => true;
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            continueButton.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasContinue);
            subtitle.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.HasContinue);
        }
        // TODO(HUD-003): complete presenter integration and the required layout/name-entry acceptance.
    }
}
