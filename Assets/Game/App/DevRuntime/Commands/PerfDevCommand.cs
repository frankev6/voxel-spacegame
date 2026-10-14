#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    public sealed class PerfDevCommand : IDevCommand
    {
        public string Name => "perf";
        public string Usage => "perf [voxel=false] [combat=false] [markers=false] [json=false]";
        public string Summary => "Read profiler, voxel, combat and last-frame marker counters";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "voxel", "combat", "markers", "json");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            if (ctx.Host == null || ctx.Host.Perf == null) return DevCommandResult.Fail("dev perf counters unavailable");
            return DevCommandResult.Ok(ctx.Host.Perf.Format(ctx, args.GetBool("voxel", false), args.GetBool("combat", false),
                args.GetBool("markers", false), args.GetBool("json", false)));
        }
    }
}
#endif
