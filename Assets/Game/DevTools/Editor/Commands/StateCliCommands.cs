using System.Collections.Generic;
using System.Globalization;
using Game.App.DevRuntime;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    public static class StateCliCommands
    {
        [CliCommand("gh_inv", "List cargo, overflow, capacity and credits (play mode).")]
        public static string Inv([CliArg("max", "Maximum occupied slots to show")] int max = 80)
            => CoreDevCliCommands.Dev("inv max=" + max.ToString(CultureInfo.InvariantCulture));

        [CliCommand("gh_give", "Grant an item to cargo or add credits (play mode).")]
        public static string Give([CliArg("item", "Item definition ID")] string item = "",
            [CliArg("count", "Positive item count")] int count = 1, [CliArg("credits", "Nonnegative credit grant")] string credits = "")
        {
            var args = new List<string> { "give" };
            if (!string.IsNullOrEmpty(item)) args.Add("item=" + DevJsonWriter.Quote(item));
            if (string.IsNullOrEmpty(credits) || count != 1) args.Add("count=" + count.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(credits)) args.Add("credits=" + DevJsonWriter.Quote(credits));
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }

        [CliCommand("gh_facts", "Get, set, list or evaluate facts; no arguments lists all (play mode).")]
        public static string Facts([CliArg("get", "Fact name")] string get = "", [CliArg("set", "Fact name=Int32 value")] string set = "",
            [CliArg("list", "Ordinal name prefix; empty lists all")] string list = "", [CliArg("eval", "Canonical logic condition, e.g. $story_count >= 3")] string eval = "")
        {
            var args = new List<string> { "facts" };
            if (!string.IsNullOrEmpty(get)) args.Add("get=" + DevJsonWriter.Quote(get));
            if (!string.IsNullOrEmpty(set)) args.Add("set=" + DevJsonWriter.Quote(set));
            if (!string.IsNullOrEmpty(eval)) args.Add("eval=" + DevJsonWriter.Quote(eval));
            if (!string.IsNullOrEmpty(list) || args.Count == 1) args.Add("list=" + DevJsonWriter.Quote(list));
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }
    }
}
