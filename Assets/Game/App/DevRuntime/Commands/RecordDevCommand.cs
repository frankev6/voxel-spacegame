#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-016: front-end to the SPEC-71 recorder owned by this host.</summary>
    public sealed class RecordDevCommand : IDevCommand
    {
        public string Name => "record";
        public string Usage => "record start|stop|save [name=<file>]";
        public string Summary => "Record flight input and save it under TestData/Replays";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "name");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 1) return DevCommandResult.Fail("expected " + Usage);
            string action = args.Positional[0];
            if (action != "save" && args.Has("name")) return DevCommandResult.Fail("name is only used by record save");
            switch (action)
            {
                case "start": return ctx.Host.Replay.StartRecording(ctx);
                case "stop": return ctx.Host.Replay.StopRecording();
                case "save": return ctx.Host.Replay.Save(args.GetString("name", ""));
                default: return DevCommandResult.Fail("expected " + Usage);
            }
        }
    }
}
#endif
