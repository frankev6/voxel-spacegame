#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Submit a visible UI element through the runtime harness (SPEC-70 DEV-004).</summary>
    public sealed class ClickDevCommand : IDevCommand
    {
        public string Name => "click";
        public string Usage => "click target=<name or text> [index=0]";
        public string Summary => "Submit a visible UI element";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "target", "index");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0) return DevCommandResult.Fail("expected " + Usage);
            return ctx.Ui.Click(args.GetString("target", ""), checked((int)args.GetLong("index", 0)));
        }
    }
}
#endif
