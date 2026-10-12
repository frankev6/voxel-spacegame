#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.Session;
using Unity.Entities;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>FND-020: opt-in stationary test station beside the real player ship.</summary>
    public sealed class RangeDevCommand : IDevCommand
    {
        public string Name => "range";
        public string Usage => "range";
        public string Summary => "Spawn the stationary test station; Space or left mouse fires";
        public DevCommandResult Execute(DevArgs args, DevContext context)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name);
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? "expected range");
            if (context.PlayerShipRoot == Entity.Null || !context.TryGetService<FiringRangeService>(out var range)) return DevCommandResult.Fail("start a new game first");
            range.Spawn(context.PlayerShipRoot);
            return DevCommandResult.Ok("Test station requested. Aim with mouse; Space or left mouse fires. Restart with newgame to reset.");
        }
    }
}
#endif
