#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.DevRuntime.Commands;

namespace Game.App.DevRuntime
{
    /// <summary>Explicit command registration shared by CLI, console and scenarios (SPEC-70 §3.1).</summary>
    public sealed class DevCommandRegistry
    {
        readonly Dictionary<string, IDevCommand> byName = new Dictionary<string, IDevCommand>(StringComparer.Ordinal);
        readonly List<IDevCommand> commands = new List<IDevCommand>();
        public IReadOnlyList<IDevCommand> Commands => commands;
        public DevCommandRegistry(params IDevCommand[] entries)
        {
            foreach (var command in entries)
            {
                if (command == null || string.IsNullOrWhiteSpace(command.Name) || command.Name != command.Name.ToLowerInvariant() || command.Name.Contains(" "))
                    throw new ArgumentException("command names must be lowercase words");
                if (!byName.TryAdd(command.Name, command)) throw new ArgumentException("duplicate command '" + command.Name + "'");
                commands.Add(command);
            }
        }
        public static DevCommandRegistry CreateDefault() => new DevCommandRegistry(new HelpDevCommand(), new OpDevCommand(), new UntilDevCommand(), new KeyDevCommand(), new MouseDevCommand(), new ClickDevCommand(), new UiDevCommand(), new NewGameDevCommand(), new SandboxDevCommand());
        public bool TryGet(string name, out IDevCommand command) => byName.TryGetValue(name, out command);
        public DevCommandResult Execute(string line, DevContext ctx)
        {
            if (!DevArgs.TryParse(line, out string name, out var args, out string error)) return DevCommandResult.Fail(error);
            if (!byName.TryGetValue(name, out var command)) return DevCommandResult.Fail("unknown command '" + name + "'; see help");
            try { return command.Execute(args, ctx); }
            catch (Exception e) { return DevCommandResult.Fail(e.GetType().Name + ": " + e.Message); }
        }
        internal static string UnknownArgument(DevArgs args, string command, params string[] accepted)
        {
            foreach (string key in args.Keys)
                if (Array.IndexOf(accepted, key) < 0) return "unknown argument '" + key + "' for " + command;
            return null;
        }
    }
}
#endif
