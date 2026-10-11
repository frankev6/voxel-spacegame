using Game.App.DevRuntime;
using Game.Core;
using UnityEditor;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Commands
{
    /// <summary>Thin CLI adapters to the runtime command registry (SPEC-70 DEV-003).</summary>
    public static class CoreDevCliCommands
    {
        [CliCommand("gh_dev", "Execute a runtime dev command (play mode).")]
        public static string Dev([CliArg("cmd", "Console command line")] string cmd) => Execute(cmd);
        [CliCommand("gh_help", "List runtime commands or show one usage (play mode).")]
        public static string Help([CliArg("cmd", "Optional command name")] string cmd = "") => Execute("help " + (string.IsNullOrEmpty(cmd) ? "" : DevJsonWriter.Quote(cmd)));
        [CliCommand("gh_op", "Poll a runtime operation without blocking.")]
        public static string Op([CliArg("id", "Operation id")] int id) => Execute("op id=" + id);
        [CliCommand("gh_until", "Evaluate a runtime expression once.")]
        public static string Until([CliArg("cond", "Expression text")] string cond) => Execute("until cond=" + DevJsonWriter.Quote(cond));
        static string Execute(string line)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            if (!Services.TryGet<DevRuntimeHost>(out var host) || host == null) return "dev runtime not available";
            var result = host.Commands.Execute(line, host.Context);
            return result.Kind == DevResultKind.Pending ? "op " + result.OperationId + ": " + result.Text :
                result.Kind == DevResultKind.Failed ? "failed: " + result.Text : result.Text;
        }
    }
}
