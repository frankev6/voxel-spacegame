using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class DockView : UIScreen
    {
        readonly DockViewModel viewModel;
        readonly IDockCommands commands;

        public DockView(DockViewModel vm, IDockCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "dock";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        // TODO(HUD-019): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
