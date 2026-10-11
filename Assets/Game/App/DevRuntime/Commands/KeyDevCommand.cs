#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Inject keyboard state through the runtime harness (SPEC-70 DEV-004).</summary>
    public sealed class KeyDevCommand : IDevCommand
    {
        public string Name => "key";
        public string Usage => "key keys=W [hold=0.12] [release=false]";
        public string Summary => "Inject keyboard state";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "keys", "hold", "release");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0 || !args.Has("keys")) return DevCommandResult.Fail("expected " + Usage);
            ctx.Input.SetKeys(args.GetString("keys", ""), args.GetDouble("hold", .12), args.GetBool("release", false));
            return DevCommandResult.Ok("keyboard state queued");
        }
    }
}
#endif
