using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class SettingsView : UIScreen
    {
        readonly SettingsViewModel viewModel;
        readonly ISettingsCommands commands;

        public SettingsView(SettingsViewModel vm, ISettingsCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "settings";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        // TODO(HUD-005): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
