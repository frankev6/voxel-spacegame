#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Inject virtual mouse state through the runtime harness (SPEC-70 DEV-004).</summary>
    public sealed class MouseDevCommand : IDevCommand
    {
        public string Name => "mouse";
        public string Usage => "mouse [x=-1] [y=-1] [button=none] [down=true] [scroll=0] [dx=0] [dy=0]";
        public string Summary => "Inject virtual mouse state";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "x", "y", "button", "down", "scroll", "dx", "dy");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0) return DevCommandResult.Fail("expected " + Usage);
            ctx.Input.SetMouse((float)args.GetDouble("x", -1), (float)args.GetDouble("y", -1), args.GetString("button", "none"), args.GetBool("down", true), (float)args.GetDouble("scroll", 0), (float)args.GetDouble("dx", 0), (float)args.GetDouble("dy", 0));
            return DevCommandResult.Ok("mouse state queued");
        }
    }
}
#endif
