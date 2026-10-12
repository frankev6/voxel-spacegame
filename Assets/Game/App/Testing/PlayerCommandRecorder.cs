#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Unity.Entities;

namespace Game.App.Testing
{
    /// <summary>SPEC-71 §5.4 per-world recorder; observes the effective post-replay frame input.</summary>
    public sealed class PlayerCommandRecorder
    {
        World world;
        PlayerCommandRecordSystem hook;
        PlayerCommandScript script;
        uint lastTick;
        public bool IsRecording { get; private set; }
        public void Start(World world)
        {
            if (IsRecording) throw new InvalidOperationException("Recording is already active.");
            if (world == null || !world.IsCreated) throw new ArgumentException("Recording requires a live world.", nameof(world));
            using var clock = world.EntityManager.CreateEntityQuery(typeof(SimClock));
            using var command = world.EntityManager.CreateEntityQuery(typeof(PlayerCommand));
            if (clock.CalculateEntityCount() != 1 || command.CalculateEntityCount() != 1) throw new InvalidOperationException("Recording requires SimClock and PlayerCommand singletons.");
            hook = world.GetOrCreateSystemManaged<PlayerCommandRecordSystem>();
            if (hook.Recorder != null) throw new InvalidOperationException("This world already has an active recorder.");
            var group = world.GetOrCreateSystemManaged<GameInitGroup>(); group.AddSystemToUpdateList(hook); group.SortSystems();
            this.world = world; script = new PlayerCommandScript();
            lastTick = clock.GetSingleton<SimClock>().Tick; script.Begin(lastTick);
            Sample(lastTick, command.GetSingleton<PlayerCommand>());
            hook.Recorder = this; IsRecording = true;
        }
        internal void Sample(uint tick, in PlayerCommand command) { lastTick = tick; script.Set(tick, command); }
        public PlayerCommandScript Stop()
        {
            if (!IsRecording) throw new InvalidOperationException("Recording is not active.");
            if (world != null && world.IsCreated)
            {
                using var clock = world.EntityManager.CreateEntityQuery(typeof(SimClock));
                if (!clock.IsEmptyIgnoreFilter) lastTick = clock.GetSingleton<SimClock>().Tick;
            }
            var result = script; Detach(); return result;
        }
        internal void Detach()
        {
            script?.Finish(lastTick);
            if (hook != null) hook.Recorder = null;
            hook = null; world = null; IsRecording = false;
        }
    }
}
#endif
