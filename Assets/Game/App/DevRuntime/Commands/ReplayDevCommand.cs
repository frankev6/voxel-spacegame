#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-016: playback stays Pending until SPEC-71 reports its exclusive end.</summary>
    public sealed class ReplayDevCommand : IDevCommand
    {
        public string Name => "replay";
        public string Usage => "replay name=<file>";
        public string Summary => "Replay saved flight input from the current simulation tick";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "name");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0 || !args.Has("name")) return DevCommandResult.Fail("expected " + Usage);
            return ctx.Host.Replay.Play(ctx, args.GetString("name", ""));
        }
    }
}
#endif
