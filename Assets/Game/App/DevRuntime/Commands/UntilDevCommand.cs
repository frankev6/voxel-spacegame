#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.DevRuntime.Scenarios;
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Evaluate a scenario expression once (SPEC-70 DEV-005).</summary>
    public sealed class UntilDevCommand : IDevCommand
    {
        public string Name => "until";
        public string Usage => "until cond=\"expression\"";
        public string Summary => "Evaluate an expression once";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "cond");
            if (error != null) return DevCommandResult.Fail(error);
            if (!args.Has("cond") || args.Positional.Count != 0) return DevCommandResult.Fail("expected " + Usage);
            var expression = ScenarioParser.ParseExpression(args.GetString("cond", ""), ctx.Host.Probes);
            return DevCommandResult.Ok(ScenarioExpression.RequireBool(expression.Evaluate(ctx)) ? "true" : "false");
        }
    }
}
#endif
