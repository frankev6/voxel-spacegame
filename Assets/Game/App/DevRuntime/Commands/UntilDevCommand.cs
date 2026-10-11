#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Expression entry point; DEV-005 supplies the evaluator (SPEC-70 DEV-003).</summary>
    public sealed class UntilDevCommand : IDevCommand
    {
        public string Name => "until";
        public string Usage => "until cond=\"expression\"";
        public string Summary => "Evaluate an expression once";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "cond");
            return DevCommandResult.Fail(error ?? "expressions not available");
        }
    }
}
#endif
