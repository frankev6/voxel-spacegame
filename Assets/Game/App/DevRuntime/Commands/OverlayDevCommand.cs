#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.DevRuntime.Overlays;
namespace Game.App.DevRuntime.Commands
{
    public sealed class OverlayDevCommand : IDevCommand
    {
        public string Name => "overlay";
        public string Usage => "overlay [name=bricks|dirty|colliders|all|none] [on=true]";
        public string Summary => "Inspect live voxel bounds, dirty flags and collider boxes within 300 m; reports truncation";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "name", "on");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            var renderer = ctx.Host.Overlays;
            if (!args.TryGetString("name", out string name))
                return args.Has("on") ? DevCommandResult.Fail(Usage) : DevCommandResult.Ok(renderer.Status);
            DevOverlayFlags flags;
            switch (name)
            {
                case "bricks": flags = DevOverlayFlags.Bricks; break;
                case "dirty": flags = DevOverlayFlags.Dirty; break;
                case "colliders": flags = DevOverlayFlags.Colliders; break;
                case "all": flags = DevOverlayFlags.All; break;
                case "none": flags = DevOverlayFlags.None; break;
                default: return DevCommandResult.Fail("Unknown or not implemented overlay '" + name + "'; " + Usage);
            }
            bool on = args.GetBool("on", true);
            renderer.Enabled = name == "none" ? DevOverlayFlags.None : on ? renderer.Enabled | flags : renderer.Enabled & ~flags;
            return DevCommandResult.Ok(renderer.Status);
        }
    }
}
#endif
