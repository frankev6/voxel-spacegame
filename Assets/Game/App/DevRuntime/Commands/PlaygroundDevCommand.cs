#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>One entry point shared by the console, CLI and playground acceptance.</summary>
    public sealed class PlaygroundDevCommand : IDevCommand
    {
        public string Name => "playground";
        public string Usage => "playground [reset|status|activate] | playground goto=<pad>";
        public string Summary => "Enter/reset the isolated interactive playground; fly to a pad and press F";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "goto");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Has("goto")) return args.Positional.Count == 0
                ? ctx.Host.Playground.GoTo(args.GetString("goto", "")) : DevCommandResult.Fail(Usage);
            if (args.Positional.Count > 1) return DevCommandResult.Fail(Usage);
            string action = args.Positional.Count == 0 ? "enter" : args.Positional[0];
            return action switch
            {
                "enter" or "reset" => ctx.Host.Playground.Enter(),
                "status" => ctx.Host.Playground.Status(),
                "activate" => ctx.Host.Playground.Activate(),
                _ => DevCommandResult.Fail(Usage)
            };
        }
    }
}
#endif
