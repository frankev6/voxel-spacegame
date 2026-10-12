using Game.UI.Data;
using UnityEngine.UIElements;
namespace Game.UI.Views
{
    /// <summary>ADR-0047: minimal playable pause/resume view; replaced by HUD-006's full pause screen.</summary>
    public sealed class M1PauseView : UIScreen
    {
        Button resume;
        public override string Key => "m1-pause";
        public override UILayer Layer => UILayer.Modals;
        public override VisualElement DefaultFocus => resume;
        protected override void OnBuild() { resume = Q<Button>("m1-pause-resume"); resume.clicked += Close; }
    }
}
