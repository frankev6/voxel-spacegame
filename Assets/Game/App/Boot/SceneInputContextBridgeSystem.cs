using Game.App.Flow;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Systems;
using Game.Input;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Unity.Entities;

namespace Game.App.Boot
{
    /// <summary>Temporary sole context owner before UI-004 (ADR-0044). Reads state before input sampling, even while paused.</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateBefore(typeof(PlayerInputSampleSystem))]
    public partial class SceneInputContextBridgeSystem : SystemBase
    {
        SceneFlow flow;
        IPauseService pause;
        InputService input;
        PauseToken escapePause;
        protected override void OnCreate() => RequireForUpdate<PlayerCommand>();
        internal void Bind(SceneFlow flow, IPauseService pause, InputService input)
        { this.flow = flow; this.pause = pause; this.input = input; }
        internal void Unbind()
        {
            escapePause.Dispose(); escapePause = default;
            input?.SetContext(InputContext.None);
            input = null; pause = null; flow = null;
        }
        protected override void OnUpdate()
        {
            if (input == null) return;
            if (flow.IsTransitioning || flow.CurrentScene != GameScene.World)
            { escapePause.Dispose(); escapePause = default; }
            else if (input.Controls.Global.Back.WasPressedThisFrame() && !input.IsRebinding)
            {
                if (escapePause.IsValid) { escapePause.Dispose(); escapePause = default; }
                else if (!pause.IsPaused) escapePause = pause.Acquire("Flight checkpoint");
            }
            input.SetContext(flow.IsTransitioning || pause.IsPaused ? InputContext.None : flow.CurrentScene switch
            {
                GameScene.World => InputContext.Flight,
                GameScene.BlueprintLab => InputContext.Editor,
                GameScene.MainMenu => InputContext.Menu,
                _ => InputContext.None
            });
        }
        protected override void OnDestroy() => Unbind();
        // TODO(UI-004): unbind/remove this system when UINavigator becomes the sole context owner.
    }
}
