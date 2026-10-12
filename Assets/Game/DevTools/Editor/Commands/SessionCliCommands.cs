using System;
using System.Threading.Tasks;
using Game.App.DevRuntime;
using Game.DevTools.Editor.Scenarios;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>DEV-008: the shared Editor driver enters Play; all game work remains in the runtime registry.</summary>
    public static class SessionCliCommands
    {
        [CliCommand("gh_newgame", "Start a campaign, entering Play from Boot if needed.")]
        public static Task<string> NewGame([CliArg("seed", "Unsigned campaign seed")] string seed,
            [CliArg("ship", "Start scenario id (empty uses configured starter)")] string ship = "",
            [CliArg("intro", "Allow intro screens on a boot started by this command")] bool intro = false,
            [CliArg("profile", "Isolated save profile display name")] string profile = "harness")
            => Start("newgame seed=" + DevJsonWriter.Quote(seed) + " ship=" + DevJsonWriter.Quote(ship)
                + " intro=" + (intro ? "true" : "false") + " profile=" + DevJsonWriter.Quote(profile), intro);
        [CliCommand("gh_sandbox", "Enter an empty or player sandbox, starting Play if needed.")]
        public static Task<string> Sandbox([CliArg("player", "Spawn the player ship")] bool player = true,
            [CliArg("seed", "Unsigned sandbox seed")] string seed = "1")
            => Start("sandbox player=" + (player ? "true" : "false") + " seed=" + DevJsonWriter.Quote(seed), false);
        static async Task<string> Start(string line, bool intro)
        {
            try
            {
                if (!DevArgs.TryParse(line, out _, out var args, out var error)) return "failed: " + error;
                args.GetULong("seed", 1); // Reject malformed CLI seeds before entering Play.
                var result = await EditorScenarioDriver.StartCommand(line, intro);
                return result.Kind == DevResultKind.Pending ? "op " + result.OperationId + ": " + result.Text
                    : result.Kind == DevResultKind.Failed ? "failed: " + result.Text : result.Text;
            }
            catch (Exception e) { return "failed: " + e.Message; }
        }
    }
}
