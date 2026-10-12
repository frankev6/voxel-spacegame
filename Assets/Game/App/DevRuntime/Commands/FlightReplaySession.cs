#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using Game.App.Testing;
using Game.Core.Events;
using Game.Core.Flow;
using Unity.Entities;
using UnityEngine;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-016: host-owned recording, bounded file names and session-bound playback.</summary>
    internal sealed class FlightReplaySession : IDisposable
    {
        readonly PlayerCommandRecorder recorder = new PlayerCommandRecorder();
        PlayerCommandScript recorded;
        PlayerCommandReplaySystem replay;
        Playback playback;
        uint generation;
        public FlightReplaySession() => EventBus<SessionReset>.Subscribe(Reset);
        static string Ready(DevContext ctx)
        {
            if (!ctx.HasGameplay || ctx.PlayerShipRoot == Entity.Null || ctx.Tick < 0) return "flight requires a player and simulation clock";
            if (!ctx.TryGetService<IGameFlow>(out var flow) || flow.IsTransitioning) return "game flow is not ready";
            return null;
        }
        public DevCommandResult StartRecording(DevContext ctx)
        {
            string error = Ready(ctx);
            if (error != null) return DevCommandResult.Fail(error);
            if (recorder.IsRecording) return DevCommandResult.Fail("recording is already active");
            if (replay != null && replay.IsPlaying) return DevCommandResult.Fail("replay is active");
            recorder.Start(ctx.World); recorded = null;
            return DevCommandResult.Ok("recording from tick " + ctx.Tick);
        }
        public DevCommandResult StopRecording()
        {
            if (!recorder.IsRecording) return DevCommandResult.Fail("recording is not active");
            recorded = recorder.Stop();
            return DevCommandResult.Ok("recorded " + (recorded.EndTick - recorded.StartTick) + " ticks");
        }
        public DevCommandResult Save(string name)
        {
            if (recorder.IsRecording) return DevCommandResult.Fail("stop recording before saving");
            if (recorded == null) return DevCommandResult.Fail("no completed recording");
            string path = PathFor(name); Directory.CreateDirectory(Path.GetDirectoryName(path)); recorded.Save(path);
            return DevCommandResult.Ok("saved " + path);
        }
        static string PathFor(string name)
        {
            if (name.Length == 0 || name.Length > 80) throw new ArgumentException("name expects 1 to 80 letters, digits, underscores or hyphens (without extension)");
            foreach (char c in name)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new ArgumentException("name expects letters, digits, underscores or hyphens (without extension)");
            return Path.GetFullPath(Path.Combine("TestData", "Replays", name + ".pcr"));
        }
        public DevCommandResult Play(DevContext ctx, string name)
        {
            string error = Ready(ctx);
            if (error != null) return DevCommandResult.Fail(error);
            if (recorder.IsRecording) return DevCommandResult.Fail("recording is active");
            if (playback != null && playback.Status == DevOperationStatus.Running) return DevCommandResult.Fail("replay is already active");
            var source = PlayerCommandScript.Load(PathFor(name));
            if (source.EndTick == source.StartTick) return DevCommandResult.Fail("recording contains zero ticks");
            var script = source.AtStart(checked((uint)ctx.Tick));
            replay = ctx.World.GetExistingSystemManaged<PlayerCommandReplaySystem>();
            if (replay == null) return DevCommandResult.Fail("replay system is unavailable");
            replay.Play(script);
            playback = new Playback(this, ctx, replay, script.EndTick - script.StartTick);
            int id = ctx.Host.AddOperation(playback);
            return DevCommandResult.Pending(id, "replaying " + name + ": " + (script.EndTick - script.StartTick) + " ticks");
        }
        void Reset(SessionReset value)
        {
            if (recorder.IsRecording) recorded = recorder.Stop();
            playback?.Cancel("replay interrupted by session change");
            replay?.Stop(); replay = null; generation++;
        }
        public void Dispose() { EventBus<SessionReset>.Unsubscribe(Reset); Reset(default); }
        sealed class Playback : IDevOperation
        {
            readonly FlightReplaySession owner;
            readonly PlayerCommandReplaySystem system;
            readonly World world;
            readonly Entity player;
            readonly uint generation, ticks;
            readonly double started;
            public DevOperationStatus Status { get; private set; }
            public string Message { get; private set; } = "replaying";
            public double TimeoutSeconds { get; }
            public Playback(FlightReplaySession owner, DevContext ctx, PlayerCommandReplaySystem system, uint ticks)
            {
                this.owner = owner; this.system = system; this.ticks = ticks; generation = owner.generation;
                world = ctx.World; player = ctx.PlayerShipRoot; started = Time.realtimeSinceStartupAsDouble;
                TimeoutSeconds = ticks / 60.0 + 60;
            }
            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                if (generation != owner.generation || ctx.World != world || !world.IsCreated || ctx.PlayerShipRoot != player || !ctx.HasGameplay)
                { Fail("replay interrupted by session change"); return; }
                if (system.Finished) { Status = DevOperationStatus.Succeeded; Message = "replayed " + ticks + " ticks"; return; }
                if (!system.IsPlaying) { Fail("replay stopped"); return; }
                if (Time.realtimeSinceStartupAsDouble - started > TimeoutSeconds) Fail("replay timed out");
            }
            void Fail(string reason) { system.Stop(); Status = DevOperationStatus.Failed; Message = reason; }
            public void Cancel(string reason) { if (Status == DevOperationStatus.Running) Fail(reason); }
        }
    }
}
#endif
