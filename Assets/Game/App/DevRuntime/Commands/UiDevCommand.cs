#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>List visible UI text through the runtime harness (SPEC-70 DEV-004).</summary>
    public sealed class UiDevCommand : IDevCommand
    {
        public string Name => "ui";
        public string Usage => "ui [max=60] [chars=200]";
        public string Summary => "List visible UI text";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "max", "chars");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0) return DevCommandResult.Fail("expected " + Usage);
            return DevCommandResult.Ok(ctx.Ui.Text(checked((int)args.GetLong("max", 60)), checked((int)args.GetLong("chars", 200))));
        }
    }
}
#endif
