using Game.Core;
using Game.UI.Data;
namespace Game.UI.Views
{
    /// <summary>ADR-0047: temporary scene metadata until HUD-002 installs the full scene roots.</summary>
    public sealed class M1SceneRootView : UIScreen
    {
        InputContext context;
        public override string Key => "m1-scene-root";
        public override UILayer Layer => UILayer.Hud;
        public override InputContext Context => context;
        public override bool IsRoot => true;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => context != InputContext.Flight && context != InputContext.Editor;
        protected override void OnBuild() { }
        public override void OnPush(object args) => context = args is InputContext value ? value : InputContext.None;
        public override bool OnBack()
        {
            if (context == InputContext.Flight || context == InputContext.Editor) Navigator.Push<M1PauseView>();
            return true;
        }
    }
}
