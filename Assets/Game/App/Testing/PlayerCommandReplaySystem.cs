#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Unity.Entities;

namespace Game.App.Testing
{
    /// <summary>SPEC-71 §5.4 development-only override after input sampling (ADR-0051).</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateAfter(typeof(PlayerInputSampleSystem))]
    public partial class PlayerCommandReplaySystem : SystemBase
    {
        PlayerCommandScript script;
        public bool IsPlaying { get; private set; }
        public bool Finished { get; private set; }
        protected override void OnCreate() { RequireForUpdate<SimClock>(); RequireForUpdate<PlayerCommand>(); }
        public void Play(PlayerCommandScript script)
        { this.script = script ?? throw new ArgumentNullException(nameof(script)); IsPlaying = true; Finished = false; }
        public void Stop() { script = null; IsPlaying = false; Finished = false; }
        protected override void OnUpdate()
        {
            if (!IsPlaying) return;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            if (tick >= script.EndTick) { IsPlaying = false; Finished = true; script = null; return; }
            var command = script.At(tick); command.FrameIndex = (uint)UnityEngine.Time.frameCount;
            SystemAPI.SetSingleton(command);
        }
        protected override void OnStopRunning() => Stop();
    }
}
#endif
