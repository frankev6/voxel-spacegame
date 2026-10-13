using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class ShipLogView : UIScreen
    {
        readonly ShipLogViewModel viewModel;
        readonly IShipLogCommands commands;

        public ShipLogView(ShipLogViewModel vm, IShipLogCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "ship-log";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        // TODO(HUD-014): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
