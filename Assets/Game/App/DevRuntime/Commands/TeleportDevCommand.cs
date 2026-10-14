#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.DevRuntime.Commands
{
    public sealed class TeleportDevCommand : IDevCommand
    {
        public string Name => "teleport";
        public string Usage => "teleport x=<m> z=<m> | sx=<m> sz=<m> [yaw=<degrees>]";
        public string Summary => "Move the player in local or star-system coordinates and stop its motion";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "x", "z", "sx", "sz", "yaw");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            bool local = args.Has("x") || args.Has("z"), universal = args.Has("sx") || args.Has("sz");
            if (local == universal || (local && !(args.Has("x") && args.Has("z"))) || (universal && !(args.Has("sx") && args.Has("sz")))) return DevCommandResult.Fail(Usage);
            if (!ctx.HasGameplay || ctx.PlayerShipRoot == Entity.Null) return DevCommandResult.Fail("player unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs(); var root = ctx.PlayerShipRoot;
            if (!em.HasComponent<LocalTransform>(root) || !ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("player transform or frame origin unavailable");
            var pose = em.GetComponentData<LocalTransform>(root);
            var position = local ? new double3(args.GetDouble("x", 0), 0, args.GetDouble("z", 0)) :
                new double3(args.GetDouble("sx", 0), 0, args.GetDouble("sz", 0)) - frame.OriginInStarSystem;
            var rotation = args.Has("yaw") ? Planar.YawRotation((float)math.radians(args.GetDouble("yaw", 0))) : pose.Rotation;
            DevEcs.TeleportRoot(em, root, (float3)position, rotation);
            DevEcs.TryGetUniversePosition(ctx, root, out var up);
            return DevCommandResult.Ok("local=" + DevEcs.Format((float3)position) + " UniversePosition=" + DevEcs.Format(up));
        }
    }
}
#endif
