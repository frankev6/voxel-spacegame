#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Polls an operation without blocking the Editor (SPEC-70 §6.3).</summary>
    public sealed class OpDevCommand : IDevCommand
    {
        public string Name => "op";
        public string Usage => "op id=<number>";
        public string Summary => "Read operation status";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "id");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0) return DevCommandResult.Fail("expected " + Usage);
            long id = args.GetLong("id", 0);
            if (id <= 0 || id > int.MaxValue) return DevCommandResult.Fail("argument 'id' expects a positive operation id");
            if (!ctx.Host.TryGetOperation((int)id, out var op)) return DevCommandResult.Fail("unknown operation " + id);
            return DevCommandResult.Ok(op.Status.ToString().ToLowerInvariant() + ": " + op.Message);
        }
    }
}
#endif
