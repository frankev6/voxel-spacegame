using Game.App.DevRuntime;
using Unity.Pipeline.Commands;
namespace Game.DevTools.Editor.Commands
{
    public static class OverlayCliCommands
    {
        [CliCommand("gh_overlay", "Toggle live voxel overlays or report their status (play mode).")]
        public static string Overlay([CliArg("name", "bricks|dirty|colliders|all|none")] string name = null,
            [CliArg("on", "Enable the overlay")] bool on = true)
            => CoreDevCliCommands.Dev(name == null ? "overlay" : "overlay name=" + DevJsonWriter.Quote(name) + " on=" + (on ? "true" : "false"));
    }
}
