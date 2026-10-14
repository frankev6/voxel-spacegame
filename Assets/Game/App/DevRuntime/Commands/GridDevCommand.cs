#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Entities;

namespace Game.App.DevRuntime.Commands
{
    public sealed class GridDevCommand : IDevCommand
    {
        public string Name => "grid";
        public string Usage => "grid id=<PersistentId> | player=true | nearest=true [bricks=false]";
        public string Summary => "Inspect grid geometry, rendering, collider and physics state";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "id", "player", "nearest", "bricks");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            bool player = args.GetBool("player", false), nearest = args.GetBool("nearest", false);
            if ((args.Has("id") ? 1 : 0) + (player ? 1 : 0) + (nearest ? 1 : 0) != 1) return DevCommandResult.Fail(Usage);
            if (ctx.World == null || !ctx.World.IsCreated) return DevCommandResult.Fail("world unavailable");
            ctx.EntityManager.CompleteAllTrackedJobs();
            Entity root = player ? ctx.PlayerShipRoot : nearest ? DevEcs.NearestGrid(ctx) : DevEcs.FindGrid(ctx, args.GetULong("id", 0));
            if (root == Entity.Null) return DevCommandResult.Fail("grid not found");
            return DevCommandResult.Ok(DevEcs.DescribeGrid(ctx, root, args.GetBool("bricks", false)));
        }
    }
}
#endif
