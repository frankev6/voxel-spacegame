#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core;
using Game.Galaxy.Services;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.DevRuntime.Commands
{
    public sealed class TeleportDevCommand : IDevCommand
    {
        public string Name => "teleport";
        public string Usage => "teleport x=<m> z=<m> | sx=<m> sz=<m> | near=<body> [offset=40] [yaw=<degrees>]";
        public string Summary => "Move the player in local/star-system coordinates or near a loaded body and stop its motion";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "x", "z", "sx", "sz", "yaw", "near", "offset");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            bool local = args.Has("x") || args.Has("z"), universal = args.Has("sx") || args.Has("sz");
            bool near = args.Has("near");
            if ((local ? 1 : 0) + (universal ? 1 : 0) + (near ? 1 : 0) != 1 || args.Has("offset") && !near || (local && !(args.Has("x") && args.Has("z"))) || (universal && !(args.Has("sx") && args.Has("sz")))) return DevCommandResult.Fail(Usage);
            if (!ctx.HasGameplay || ctx.PlayerShipRoot == Entity.Null) return DevCommandResult.Fail("player unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs(); var root = ctx.PlayerShipRoot;
            if (!em.HasComponent<LocalTransform>(root) || !ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("player transform or frame origin unavailable");
            var pose = em.GetComponentData<LocalTransform>(root);
            var position = local ? new double3(args.GetDouble("x", 0), 0, args.GetDouble("z", 0)) : universal ?
                new double3(args.GetDouble("sx", 0), 0, args.GetDouble("sz", 0)) - frame.OriginInStarSystem : default;
            if (near)
            {
                double offset = args.GetDouble("offset", 40);
                if (offset < 0) return DevCommandResult.Fail("argument 'offset' expects finite non-negative metres");
                if (!ctx.TryGetService<GalaxyService>(out var galaxy) || !DevBodyQuery.TryFind(em, galaxy, args.GetString("near", ""), out var body, out float radius, out _))
                    return DevCommandResult.Fail("loaded body not found: '" + args.GetString("near", "") + "'");
                position = (double3)em.GetComponentData<LocalTransform>(body).Position - new double3(0, 0, radius + offset);
            }
            var rotation = args.Has("yaw") ? Planar.YawRotation((float)math.radians(args.GetDouble("yaw", 0))) : pose.Rotation;
            DevEcs.TeleportRoot(em, root, (float3)position, rotation);
            DevEcs.TryGetUniversePosition(ctx, root, out var up);
            return DevCommandResult.Ok("local=" + DevEcs.Format((float3)position) + " UniversePosition=" + DevEcs.Format(up));
        }
    }
}
#endif
