using Game.Core;
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
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "main-menu";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        // TODO(HUD-003): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
