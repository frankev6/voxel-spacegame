#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;
using Game.App.DevRuntime.Scenarios;
using Game.Data.Logic;

namespace Game.App.DevRuntime.Commands
{
    public sealed class FactsDevCommand : IDevCommand
    {
        public string Name => "facts";
        public string Usage => "facts get=<name> | set=<name>=<int> | list=<prefix> | eval=\"$story_count >= 3\"";
        public string Summary => "Read, list or set facts, or compile and evaluate a canonical condition";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "get", "set", "list", "eval");
            if (error != null || args.Positional.Count != 0 || args.Keys.Count != 1) return DevCommandResult.Fail(error ?? Usage);
            var store = DevState.Facts(ctx);
            if (args.TryGetString("eval", out string source))
            {
                var condition = DevState.Compile(source, store.Catalog, DevState.Content(ctx));
                return DevCommandResult.Ok(DevState.Evaluate(condition, ctx, store) ? "true" : "false");
            }
            if (args.TryGetString("list", out string prefix))
            {
                var text = new StringBuilder();
                for (int slot = 0; slot < store.Catalog.Count; slot++)
                {
                    string name = store.Catalog.IdAt(slot).Name;
                    if (name.StartsWith(prefix, System.StringComparison.Ordinal))
                        text.Append(name).Append('=').Append(store.GetBySlot(slot).ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
                return DevCommandResult.Ok(text.ToString().TrimEnd('\n'));
            }
            string requested;
            if (args.TryGetString("set", out string assignment))
            {
                int equals = assignment.IndexOf('=');
                if (equals <= 0 || !int.TryParse(assignment.Substring(equals + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    return DevCommandResult.Fail("set expects <name>=<Int32 value>");
                requested = assignment.Substring(0, equals);
                int slot = DevState.FactSlot(store, requested);
                DevState.CompleteWrites(ctx); store.SetBySlot(slot, value, FactWriteSource.Debugger);
            }
            else requested = args.GetString("get", "");
            int readSlot = DevState.FactSlot(store, requested);
            return DevCommandResult.Ok(store.Catalog.IdAt(readSlot).Name + "=" + store.GetBySlot(readSlot).ToString(CultureInfo.InvariantCulture));
        }
    }
}
#endif
