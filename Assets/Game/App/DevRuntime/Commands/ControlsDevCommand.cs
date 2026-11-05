#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.DevRuntime.ControlSheet;
using UnityEngine;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>Opens and closes the control sheet (SPEC-70 DEV-029).</summary>
    public sealed class ControlsDevCommand : IDevCommand
    {
        public string Name => "controls";
        public string Usage => "controls [open|close|status] [port=" + DevControlSheet.DefaultPort + "] [browser=true]";
        public string Summary => "Show every control and its live state on a localhost page in the browser, for a second monitor";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "port", "browser");
            if (error != null || args.Positional.Count > 1) return DevCommandResult.Fail(error ?? Usage);
            string verb = args.Positional.Count == 0 ? "open" : args.Positional[0];
            var sheet = ctx.Host.ControlSheet;
            if (sheet == null) return DevCommandResult.Fail("the control sheet needs the game's input service; start Play first");
            switch (verb)
            {
                case "status": return DevCommandResult.Ok(sheet.IsOpen ? "open at " + sheet.Url : "closed");
                case "close": sheet.Close(); return DevCommandResult.Ok("closed");
                case "open":
                    long port = args.GetLong("port", DevControlSheet.DefaultPort);
                    if (port < 1024 || port > 65535) return DevCommandResult.Fail("port must be 1024..65535");
                    bool wasOpen = sheet.IsOpen;
                    // Two dev builds on one machine (co-op tests) each get a port; an explicit port is taken or refused.
                    if (!sheet.TryOpen((int)port, args.Has("port") ? 1 : 10, out error)) return DevCommandResult.Fail(error);
                    if (args.GetBool("browser", true)) Application.OpenURL(sheet.Url);
                    return DevCommandResult.Ok((wasOpen ? "already open at " : "control sheet at ") + sheet.Url);
                default: return DevCommandResult.Fail(Usage);
            }
        }
    }
}
#endif
