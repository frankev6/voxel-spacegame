using Game.App.Flow;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Systems;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Game.UI.Services;
using Game.UI.Views;
using Unity.Entities;

namespace Game.App.Boot
{
    /// <summary>ADR-0047: an external pause owner (flow, exit save, harness) neutralizes input through a blocker screen; never writes
    /// context or acquires pause. QA-001: EDT-023's real scene roots replaced the temporary M1 roots, so only the blocker remains.</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateBefore(typeof(PlayerInputSampleSystem))]
    public partial class SceneNavigationBridgeSystem : SystemBase
    {
        SceneFlow flow;
        IPauseService pause;
        UINavigator navigator;
        M1InputBlockView blocker;
        protected override void OnCreate() => RequireForUpdate<PlayerCommand>();
        internal void Bind(SceneFlow flow, IPauseService pause, UIRoot ui)
        {
            this.flow = flow; this.pause = pause; navigator = ui.Navigator;
            blocker = new M1InputBlockView(); ui.Register(blocker);
        }
        internal void Unbind() { navigator = null; flow = null; pause = null; blocker = null; }
        protected override void OnUpdate()
        {
            if (navigator == null) return;
            bool externalPause = !flow.IsTransitioning && pause.ActiveTokenCount > (navigator.IsPausing ? 1 : 0);
            if (externalPause && !blocker.IsOpen) navigator.Push(blocker);
            else if (!externalPause && blocker.IsOpen) navigator.Pop(blocker);
        }
        protected override void OnDestroy() => Unbind();
    }
}
