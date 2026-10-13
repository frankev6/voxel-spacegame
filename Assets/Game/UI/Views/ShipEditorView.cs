using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class ShipEditorView : UIScreen
    {
        readonly ShipEditorViewModel viewModel;
        readonly IShipEditorCommands commands;

        public ShipEditorView(ShipEditorViewModel vm, IShipEditorCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "ship-editor";
        public override UILayer Layer => UILayer.Hud;
        public override InputContext Context => InputContext.Editor;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => false;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        // TODO(HUD-018): wire this screen's authored elements and behavior.
        protected override void OnBuild() { Root.dataSource = viewModel; }
        public override bool OnBack() => commands.OnBackPressed();
    }
}
