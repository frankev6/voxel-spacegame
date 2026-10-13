using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class StarSystemMapView : UIScreen
    {
        readonly StarSystemMapViewModel viewModel;
        readonly IStarSystemMapCommands commands;

        public StarSystemMapView(StarSystemMapViewModel vm, IStarSystemMapCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "system-map";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        // TODO(HUD-016): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
