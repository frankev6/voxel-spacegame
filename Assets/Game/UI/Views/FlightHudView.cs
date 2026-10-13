using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class FlightHudView : UIScreen
    {
        readonly HudViewModel viewModel;
        readonly IHudRadarSource radar;
        readonly System.Func<string, string> bindingDisplay;

        public FlightHudView(HudViewModel vm, IHudRadarSource radar, System.Func<string, string> bindingDisplay)
        {
            viewModel = vm;
            this.radar = radar;
            this.bindingDisplay = bindingDisplay;
        }

        public override string Key => "hud";
        public override UILayer Layer => UILayer.Hud;
        public override InputContext Context => InputContext.Flight;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => false;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        // TODO(HUD-007): wire this screen's authored elements and behavior.
        protected override void OnBuild() { }
    }
}
