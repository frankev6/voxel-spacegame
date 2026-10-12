using Game.App.DevRuntime;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>SPEC-70 DEV-016: the CLI uses the same commands as the console and scenarios.</summary>
    public static class ReplayCliCommands
    {
        [CliCommand("gh_record", "Start, stop or save a flight recording (play mode).")]
        public static string Record([CliArg("action", "start, stop or save")] string action,
            [CliArg("name", "Recording file stem for save")] string name = "")
            => CoreDevCliCommands.Dev("record " + DevJsonWriter.Quote(action) + (string.IsNullOrEmpty(name) ? "" : " name=" + DevJsonWriter.Quote(name)));
        [CliCommand("gh_replay", "Replay a saved flight recording (play mode).")]
        public static string Replay([CliArg("name", "Recording file stem")] string name)
            => CoreDevCliCommands.Dev("replay name=" + DevJsonWriter.Quote(name));
    }
}
