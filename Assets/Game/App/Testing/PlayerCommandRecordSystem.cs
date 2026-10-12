#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Unity.Entities;

namespace Game.App.Testing
{
    /// <summary>SPEC-71 §5.4 managed recording hook, ordered after the development override (ADR-0051).</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateAfter(typeof(PlayerCommandReplaySystem))]
    public partial class PlayerCommandRecordSystem : SystemBase
    {
        internal PlayerCommandRecorder Recorder;
        protected override void OnCreate() { RequireForUpdate<SimClock>(); RequireForUpdate<PlayerCommand>(); }
        protected override void OnUpdate()
        {
            if (Recorder == null) return;
            Recorder.Sample(SystemAPI.GetSingleton<SimClock>().Tick, SystemAPI.GetSingleton<PlayerCommand>());
        }
        protected override void OnStopRunning() => Recorder?.Detach();
        protected override void OnDestroy() => Recorder?.Detach();
    }
}
#endif
