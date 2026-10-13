using System;
using Game.Core;
using UnityEngine.UIElements;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class PauseView : UIScreen
    {
        readonly PauseViewModel viewModel;
        readonly IPauseCommands commands;

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
        }
        public override void OnPush(object args)
        {
            viewModel.propertyChanged += OnModelChanged;
            Refresh();
        }
        public override void OnPop() => viewModel.propertyChanged -= OnModelChanged;
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            save.SetEnabled(viewModel.CanSave);
            blocked.EnableInClassList(UIStyleClasses.IsHidden, viewModel.CanSave);
        }
        // TODO(HUD-006): complete presenter integration, key hints and the required acceptance.
    }
}
