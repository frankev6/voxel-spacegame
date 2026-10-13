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
    /// <summary>ADR-0047: navigates temporary scene roots before sampling; never writes context or acquires pause.</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateBefore(typeof(PlayerInputSampleSystem))]
    public partial class SceneNavigationBridgeSystem : SystemBase
    {
        SceneFlow flow;
        IPauseService pause;
        UINavigator navigator;
        M1SceneRootView sceneRoot;
        M1InputBlockView blocker;
        InputContext sceneContext;
        protected override void OnCreate() => RequireForUpdate<PlayerCommand>();
        internal void Bind(SceneFlow flow, IPauseService pause, UIRoot ui)
        {
            this.flow = flow; this.pause = pause; navigator = ui.Navigator;
            sceneRoot = new M1SceneRootView(); blocker = new M1InputBlockView();
            ui.Register(sceneRoot); ui.Register(blocker);
            sceneContext = InputContext.None; navigator.SetRoot(sceneRoot, sceneContext);
        }
        internal void Unbind() { navigator = null; flow = null; pause = null; sceneRoot = null; blocker = null; }
        protected override void OnUpdate()
        {
            if (navigator == null) return;
            var next = flow.IsTransitioning ? InputContext.None : flow.CurrentScene switch
            {
                GameScene.World => InputContext.Flight, GameScene.BlueprintLab => InputContext.Editor,
                GameScene.MainMenu => InputContext.Menu, _ => InputContext.None
            };
            if (next != sceneContext) { sceneContext = next; navigator.SetRoot(sceneRoot, next); }
            bool externalPause = !flow.IsTransitioning && pause.ActiveTokenCount > (navigator.IsPausing ? 1 : 0);
            if (externalPause && !blocker.IsOpen) navigator.Push(blocker);
            else if (!externalPause && blocker.IsOpen) navigator.Pop(blocker);
        }
        protected override void OnDestroy() => Unbind();
        // TODO(HUD-002): remove this adapter and its M1 views before installing the full scene roots.
    }
}
