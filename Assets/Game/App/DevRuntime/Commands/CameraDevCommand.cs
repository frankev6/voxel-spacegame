#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using Game.Presentation.Input;
using Game.Presentation.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>Feel prototype (owner, 2026-10-21): switch the flight camera preset live, and turn auto-aim on or off.</summary>
    public sealed class CameraDevCommand : IDevCommand
    {
        public string Name => "camera";
        public string Usage => "camera [<topdown|chase|cursor|orbit>|ping] [assist=on|off]";
        public string Summary => "Switch the flight camera preset; ping sends a sensor ping (V); assist= turns auto-aim on or off";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "assist");
            if (error != null || args.Positional.Count > 1) return DevCommandResult.Fail(error ?? Usage);
            if (args.Positional.Count == 1 && args.Positional[0] == "ping")
            {
                var overlay = Unity.Entities.World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Presentation.Systems.WorldOverlaySystem>();
                if (overlay == null) return DevCommandResult.Fail("no world overlay");
                overlay.Ping(); return DevCommandResult.Ok("ping");
            }
            if (args.Positional.Count == 1 && !FlightCameraPreset.TrySet(args.Positional[0])) return DevCommandResult.Fail(Usage);
            if (args.TryGetString("assist", out string assist))
            {
                if (assist != "on" && assist != "off") return DevCommandResult.Fail(Usage);
                PlayerInputSampleSystem.AimAssist = assist == "on";
            }
            var text = new StringBuilder();
            foreach (var preset in FlightCameraPreset.All)
                text.Append(preset.Name == FlightCameraPreset.Current.Name ? "> " : "  ").Append(preset.Name).Append(": ").Append(preset.Description).Append('\n');
            text.Append("auto-aim ").Append(PlayerInputSampleSystem.AimAssist ? "on" : "off");
            return DevCommandResult.Ok(text.ToString());
        }
    }
}
#endif
