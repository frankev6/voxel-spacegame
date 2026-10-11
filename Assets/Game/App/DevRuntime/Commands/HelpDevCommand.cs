#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
namespace Game.App.DevRuntime.Commands
{
    /// <summary>Lists every explicitly registered command or its usage (SPEC-70 §6.3).</summary>
    public sealed class HelpDevCommand : IDevCommand
    {
        public string Name => "help";
        public string Usage => "help [cmd]";
        public string Summary => "List commands or show one usage";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name);
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count > 1) return DevCommandResult.Fail("expected " + Usage);
            if (args.Positional.Count == 1)
                return ctx.Host.Commands.TryGet(args.Positional[0], out var command) ? DevCommandResult.Ok(command.Usage + " — " + command.Summary) : DevCommandResult.Fail("unknown command '" + args.Positional[0] + "'; see help");
            var text = new StringBuilder();
            foreach (var command in ctx.Host.Commands.Commands) text.Append(command.Usage).Append(" — ").AppendLine(command.Summary);
            return DevCommandResult.Ok(text.ToString().TrimEnd());
        }
    }
}
#endif
