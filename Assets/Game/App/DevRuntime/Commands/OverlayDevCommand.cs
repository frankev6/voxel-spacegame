#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.DevRuntime.Overlays;
namespace Game.App.DevRuntime.Commands
{
    public sealed class OverlayDevCommand : IDevCommand
    {
        public string Name => "overlay";
        public string Usage => "overlay [name=bricks|dirty|colliders|islands|spatial|interaction|all|none] [on=true] [id=<PersistentId>]";
        public string Summary => "Inspect voxel bounds within 300 m, cached target islands, spatial hash cells within 1 km and live interaction ranges; reports skips and truncation";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "name", "on", "id");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            var renderer = ctx.Host.Overlays;
            if (!args.TryGetString("name", out string name))
                return args.Has("on") || args.Has("id") ? DevCommandResult.Fail(Usage) : DevCommandResult.Ok(renderer.Status);
            if (args.Has("id") && name != "islands" && name != "all") return DevCommandResult.Fail("id selects the islands target; " + Usage);
            DevOverlayFlags flags;
            switch (name)
            {
                case "bricks": flags = DevOverlayFlags.Bricks; break;
                case "dirty": flags = DevOverlayFlags.Dirty; break;
                case "colliders": flags = DevOverlayFlags.Colliders; break;
                case "islands": flags = DevOverlayFlags.Islands; break;
                case "spatial": flags = DevOverlayFlags.Spatial; break;
                case "interaction": flags = DevOverlayFlags.Interaction; break;
                case "all": flags = DevOverlayFlags.All; break;
                case "none": flags = DevOverlayFlags.None; break;
                default: return DevCommandResult.Fail("Unknown or not implemented overlay '" + name + "'; " + Usage);
            }
            bool on = args.GetBool("on", true);
            if (name == "islands" || name == "all") renderer.IslandTargetId = args.GetULong("id", 0);
            renderer.Enabled = name == "none" ? DevOverlayFlags.None : on ? renderer.Enabled | flags : renderer.Enabled & ~flags;
            return DevCommandResult.Ok(renderer.Status);
        }
    }
}
#endif
