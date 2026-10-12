using Game.Core;
using Game.UI.Data;
namespace Game.UI.Views
{
    /// <summary>ADR-0047: neutralizes input for an external pause owner without acquiring another pause token.</summary>
    public sealed class M1InputBlockView : UIScreen
    {
        public override string Key => "m1-input-blocker";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.None;
        public override bool PausesSimulation => false;
        protected override void OnBuild() { }
        public override bool OnBack() => true;
    }
}
