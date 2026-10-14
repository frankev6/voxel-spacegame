using Game.App.DevRuntime;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>SPEC-70 DEV-018: shared-registry save, load and jump CLI entry points.</summary>
    public static class TravelCliCommands
    {
        [CliCommand("gh_save", "Save through game flow in the active profile (play mode).")]
        public static string Save([CliArg("slot", "Active-profile slot folder name")] string slot)
            => CoreDevCliCommands.Dev("save slot=" + DevJsonWriter.Quote(slot));
        [CliCommand("gh_load", "Load through game flow and wait for player readiness (play mode).")]
        public static string Load([CliArg("slot", "Active-profile slot folder name")] string slot)
            => CoreDevCliCommands.Dev("load slot=" + DevJsonWriter.Quote(slot));
        [CliCommand("gh_jump", "Request travel through TravelService (play mode).")]
        public static string Jump([CliArg("to", "StarSystemId number or authored id")] string to)
            => CoreDevCliCommands.Dev("jump to=" + DevJsonWriter.Quote(to));
    }
}
