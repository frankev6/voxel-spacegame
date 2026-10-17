#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>RACE-017 (owner: "increase timescale ... to test faster while staying accurate"): runs the simulation faster than real
    /// time. Ticks stay 1/60 s, at most six a frame, so the result is the same simulation; when frames cannot keep up it runs less fast,
    /// never coarser. Solo only.</summary>
    public sealed class TimeScaleDevCommand : IDevCommand
    {
        public string Name => "timescale";
        public string Usage => "timescale [<1..6>]";
        public string Summary => "Run the simulation up to 6x real time (same 1/60 s ticks); solo only";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            if (!ctx.TryGetService<Game.Core.IPauseService>(out var service) || service is not PauseService pause) return DevCommandResult.Fail("pause service unavailable");
            if (args.Positional.Count == 0) return DevCommandResult.Ok($"timescale {pause.RunScale:0.##}");
            if (!float.TryParse(args.Positional[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale))
                return DevCommandResult.Fail(Usage);
            if (scale > 1 && ctx.TryGetService<Game.Core.Net.ICoopSession>(out var coop) && coop.Role != Game.Core.Net.SimRoleKind.Solo)
                return DevCommandResult.Fail("not in a co-op session: guests would fall behind");
            pause.RunScale = scale;
            return DevCommandResult.Ok($"timescale {pause.RunScale:0.##}: up to {pause.RunScale:0.##}x real time, same 1/60 s ticks");
        }
    }
}
#endif
