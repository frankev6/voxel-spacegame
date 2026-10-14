using System.Collections.Generic;
using System.Globalization;
using Game.App.DevRuntime;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>ENC-012: CLI, console and scenario entry points share the same runtime commands.</summary>
    public static class EncounterCliCommands
    {
        [CliCommand("gh_encounter", "Spawn/combat, toggle director rolls or list encounters; no arguments lists (play mode).")]
        public static string Encounter([CliArg("spawn", "Encounter definition id")] string spawn = null,
            [CliArg("combat", "Encounter definition id to start combat")] string combat = null,
            [CliArg("director", "on|off; explicit requests remain available")] string director = null,
            [CliArg("list", "List definitions and active instances")] bool list = false,
            [CliArg("count", "Number of encounter instances, 1..10")] int count = 1,
            [CliArg("anchor", "player or body story tag (spawn only)")] string anchor = null)
        {
            var args = new List<string> { "encounter" };
            if (spawn != null) args.Add("spawn=" + DevJsonWriter.Quote(spawn));
            if (combat != null) args.Add("combat=" + DevJsonWriter.Quote(combat));
            if (director != null) args.Add("director=" + DevJsonWriter.Quote(director));
            if (spawn != null || count != 1) args.Add("count=" + count.ToString(CultureInfo.InvariantCulture));
            if (anchor != null) args.Add("anchor=" + DevJsonWriter.Quote(anchor));
            if (list || args.Count == 1) args.Add("list");
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }
        [CliCommand("gh_relation", "Read relation/stance or queue a set/add through FactionService (play mode).")]
        public static string Relation([CliArg("faction", "Faction definition id")] string faction,
            [CliArg("set", "Int32 value; relation clamps to -100..100")] string set = null,
            [CliArg("add", "Int32 delta; relation clamps to -100..100")] string add = null)
        {
            var args = new List<string> { "relation", "faction=" + DevJsonWriter.Quote(faction) };
            if (set != null) args.Add("set=" + DevJsonWriter.Quote(set));
            if (add != null) args.Add("add=" + DevJsonWriter.Quote(add));
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }
    }
}
