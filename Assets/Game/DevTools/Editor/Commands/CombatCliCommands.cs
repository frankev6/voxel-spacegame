using System.Globalization;
using Game.App.DevRuntime;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>Thin DEV-010 adapters; console, CLI and scenarios share the runtime registry.</summary>
    public static class CombatCliCommands
    {
        [CliCommand("gh_aim", "Project a target to the AgentMouse, optionally tracking it (play mode).")]
        public static string Aim([CliArg("target", "nearest_hostile, nearest, id:<PersistentId>, or point")] string target = "nearest_hostile",
            [CliArg("x", "Local point X metres")] string x = "", [CliArg("z", "Local point Z metres")] string z = "",
            [CliArg("track", "Tracking duration in seconds")] double track = 0)
        {
            string line = "aim target=" + DevJsonWriter.Quote(target) + " track=" + track.ToString("R", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(x)) line += " x=" + DevJsonWriter.Quote(x);
            if (!string.IsNullOrEmpty(z)) line += " z=" + DevJsonWriter.Quote(z);
            return CoreDevCliCommands.Dev(line);
        }
        [CliCommand("gh_fire", "Hold a real Flight fire action (play mode).")]
        public static string Fire([CliArg("group", "Weapon group 1..3")] int group = 1,
            [CliArg("seconds", "Hold duration")] double seconds = .5)
            => CoreDevCliCommands.Dev("fire group=" + group.ToString(CultureInfo.InvariantCulture) + " seconds=" + seconds.ToString("R", CultureInfo.InvariantCulture));
    }
}
