using Game.Core;
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
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "pause";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        // TODO(HUD-006): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
