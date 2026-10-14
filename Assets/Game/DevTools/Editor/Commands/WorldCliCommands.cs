using System.Collections.Generic;
using System.Globalization;
using Game.App.DevRuntime;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    public static class WorldCliCommands
    {
        static void Add(List<string> args, string key, string value)
        { if (!string.IsNullOrEmpty(value)) args.Add(key + "=" + DevJsonWriter.Quote(value)); }

        [CliCommand("gh_teleport", "Teleport the player in local or star-system coordinates (play mode).")]
        public static string Teleport([CliArg("x", "Local X metres")] string x = "", [CliArg("z", "Local Z metres")] string z = "",
            [CliArg("sx", "Star-system X metres")] string sx = "", [CliArg("sz", "Star-system Z metres")] string sz = "",
            [CliArg("yaw", "Yaw degrees; omitted keeps current rotation")] string yaw = "",
            [CliArg("near", "Loaded body key, display name or story tag")] string near = "",
            [CliArg("offset", "Non-negative surface offset in metres; default 40")] string offset = "")
        {
            var args = new List<string> { "teleport" }; Add(args, "x", x); Add(args, "z", z); Add(args, "sx", sx); Add(args, "sz", sz); Add(args, "yaw", yaw); Add(args, "near", near); Add(args, "offset", offset);
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }
        [CliCommand("gh_interact", "Press the real Interact action, optionally after moving inside a body's legal surface range (play mode).")]
        public static string Interact([CliArg("body", "Optional loaded body key, display name or story tag")] string body = null)
            => CoreDevCliCommands.Dev(body == null ? "interact" : "interact body=" + DevJsonWriter.Quote(body));
        [CliCommand("gh_spawn", "Request ships through the runtime command registry (play mode).")]
        public static string Spawn([CliArg("blueprint", "Ship blueprint ID")] string blueprint,
            [CliArg("x", "Local X metres")] string x = "", [CliArg("z", "Local Z metres")] string z = "",
            [CliArg("dx", "Player-relative X metres")] string dx = "", [CliArg("dz", "Player-relative Z metres; default 80")] string dz = "",
            [CliArg("yaw", "Yaw degrees")] string yaw = "", [CliArg("face", "player")] string face = "",
            [CliArg("faction", "Faction definition ID")] string faction = "", [CliArg("team", "Team byte")] int team = 1,
            [CliArg("count", "Number of ships")] int count = 1, [CliArg("spacing", "Spacing along +X in metres")] double spacing = 60,
            [CliArg("ai", "Attach AI")] bool ai = true)
        {
            var args = new List<string> { "spawn", "blueprint=" + DevJsonWriter.Quote(blueprint), "team=" + team.ToString(CultureInfo.InvariantCulture),
                "count=" + count.ToString(CultureInfo.InvariantCulture), "spacing=" + spacing.ToString("R", CultureInfo.InvariantCulture), "ai=" + (ai ? "true" : "false") };
            Add(args, "x", x); Add(args, "z", z); Add(args, "dx", dx); Add(args, "dz", dz); Add(args, "yaw", yaw); Add(args, "face", face); Add(args, "faction", faction);
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }
        [CliCommand("gh_grid", "Inspect a voxel grid and optional brick details (play mode).")]
        public static string Grid([CliArg("id", "Exact persistent ID")] string id = "", [CliArg("player", "Select player")] bool player = false,
            [CliArg("nearest", "Select nearest other grid")] bool nearest = false, [CliArg("bricks", "Up to 200 brick detail rows")] bool bricks = false)
        {
            var args = new List<string> { "grid", "bricks=" + (bricks ? "true" : "false") }; Add(args, "id", id);
            if (player) args.Add("player=true"); if (nearest) args.Add("nearest=true");
            return CoreDevCliCommands.Dev(string.Join(" ", args));
        }
    }
}
