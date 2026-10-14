#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Game.Core.Components;
using Game.Simulation;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-010: project a live target and inject AgentMouse position.</summary>
    public sealed class AimDevCommand : IDevCommand
    {
        readonly Func<double> now;
        public AimDevCommand() : this(() => Time.unscaledTimeAsDouble) { }
        internal AimDevCommand(Func<double> now) => this.now = now;
        public string Name => "aim";
        public string Usage => "aim [target=nearest_hostile|nearest|id:<PersistentId>|point] [x=<m> z=<m>] [track=0]";
        public string Summary => "Aim the AgentMouse at a grid or local point, optionally tracking it";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "target", "x", "z", "track");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            double seconds = args.GetDouble("track", 0);
            if (!double.IsFinite(seconds) || seconds < 0) return DevCommandResult.Fail("track expects finite non-negative seconds");
            if (!ctx.HasGameplay) return DevCommandResult.Fail("gameplay unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            Entity player = ctx.PlayerShipRoot;
            if (player == Entity.Null || !em.HasComponent<LocalTransform>(player)) return DevCommandResult.Fail("no player ship");
            string target = args.GetString("target", "nearest_hostile");
            bool pointTarget = target == "point";
            if (pointTarget != (args.Has("x") && args.Has("z")) || !pointTarget && (args.Has("x") || args.Has("z")))
                return DevCommandResult.Fail("x and z are required only for target=point");
            float3 point = default; ulong id = 0;
            if (pointTarget) point = new float3((float)args.GetDouble("x", 0), 0, (float)args.GetDouble("z", 0));
            else
            {
                Entity root;
                if (target.StartsWith("id:", StringComparison.Ordinal))
                {
                    if (!ulong.TryParse(target.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
                        return DevCommandResult.Fail("target expects id:<positive PersistentId>");
                    root = DevEcs.FindGrid(ctx, id);
                }
                else if (target == "nearest" || target == "nearest_hostile")
                {
                    bool hostile = target == "nearest_hostile";
                    if (hostile && !em.HasComponent<Allegiance>(player)) return DevCommandResult.Fail("player allegiance unavailable");
                    root = Nearest(ctx, player, hostile);
                }
                else return DevCommandResult.Fail("unknown aim target '" + target + "'");
                if (root == Entity.Null || !em.HasComponent<LocalTransform>(root)) return DevCommandResult.Fail("aim target not found");
                id = em.GetComponentData<PersistentId>(root).Value;
                point = em.GetComponentData<LocalTransform>(root).Position;
            }
            error = Move(ctx, point);
            if (error != null) return DevCommandResult.Fail(error);
            if (seconds == 0) return DevCommandResult.Ok("aim position queued");
            int operation = ctx.Host.AddOperation(new TrackOperation(ctx.World, player, id, point, seconds, now));
            return DevCommandResult.Pending(operation, "tracking aim target");
        }
        static Entity Nearest(DevContext ctx, Entity player, bool hostile)
        {
            var em = ctx.EntityManager; var position = em.GetComponentData<LocalTransform>(player).Position;
            byte team = hostile ? em.GetComponentData<Allegiance>(player).Team : (byte)0;
            Entity found = Entity.Null; double best = double.PositiveInfinity; ulong bestId = ulong.MaxValue;
            using var roots = ctx.GridEntities();
            foreach (var root in roots)
            {
                if (root == player || !em.IsEnabled(root) || !em.HasComponent<LocalTransform>(root) || !em.HasComponent<PersistentId>(root)) continue;
                if (em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root)) continue;
                if (hostile && (em.HasComponent<Hulk>(root) || !em.HasComponent<Allegiance>(root) || em.GetComponentData<Allegiance>(root).Team == team)) continue;
                double distance = math.lengthsq((double3)em.GetComponentData<LocalTransform>(root).Position - (double3)position);
                ulong id = em.GetComponentData<PersistentId>(root).Value;
                if (id != 0 && (distance < best || distance == best && id < bestId)) { found = root; best = distance; bestId = id; }
            }
            return found;
        }
        static string Move(DevContext ctx, float3 point)
        {
            if (!math.all(math.isfinite(point))) return "aim point exceeds local coordinate range";
            var camera = ctx.GameplayCamera;
            if (camera == null) return "gameplay camera unavailable";
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= 0) return "target behind camera";
            if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y) || !float.IsFinite(screen.z)) return "target projection is not finite";
            ctx.Input.MoveMouse(new Vector2(screen.x, screen.y));
            return null;
        }
        sealed class TrackOperation : IDevOperation
        {
            readonly World world;
            readonly Entity player;
            readonly ulong id;
            readonly float3 point;
            readonly Func<double> now;
            readonly double end, seconds;
            public TrackOperation(World world, Entity player, ulong id, float3 point, double seconds, Func<double> now)
            { this.world = world; this.player = player; this.id = id; this.point = point; this.seconds = seconds; this.now = now; end = now() + seconds; }
            public DevOperationStatus Status { get; private set; } = DevOperationStatus.Running;
            public string Message { get; private set; } = "tracking aim target";
            public double TimeoutSeconds => seconds + 1;
            public void Tick(DevContext ctx)
            {
                if (ctx.World != world || !world.IsCreated || !ctx.HasGameplay || ctx.PlayerShipRoot != player)
                { Fail("aim world or player changed"); return; }
                if (now() >= end) { Status = DevOperationStatus.Succeeded; Message = "aim tracking complete"; return; }
                var target = point;
                if (id != 0)
                {
                    ctx.EntityManager.CompleteAllTrackedJobs();
                    Entity root = DevEcs.FindGrid(ctx, id);
                    if (root == Entity.Null || !ctx.EntityManager.HasComponent<LocalTransform>(root)) { Fail("aim target disappeared"); return; }
                    target = ctx.EntityManager.GetComponentData<LocalTransform>(root).Position;
                }
                string error = Move(ctx, target); if (error != null) Fail(error);
            }
            void Fail(string message) { Status = DevOperationStatus.Failed; Message = message; }
        }
    }
}
#endif
