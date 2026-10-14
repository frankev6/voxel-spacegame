using Game.App.DevRuntime;
using Unity.Pipeline.Commands;
namespace Game.DevTools.Editor.Commands
{
    public static class OverlayCliCommands
    {
        [CliCommand("gh_overlay", "Toggle live voxel/island/spatial/interaction overlays or report their status (play mode).")]
        public static string Overlay([CliArg("name", "bricks|dirty|colliders|islands|spatial|interaction|all|none")] string name = null,
            [CliArg("on", "Enable the overlay")] bool on = true,
            [CliArg("id", "Islands target PersistentId; zero uses the player")] ulong id = 0)
            => CoreDevCliCommands.Dev(name == null ? "overlay" : "overlay name=" + DevJsonWriter.Quote(name) + " on=" + (on ? "true" : "false")
                + (id == 0 ? "" : " id=" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
