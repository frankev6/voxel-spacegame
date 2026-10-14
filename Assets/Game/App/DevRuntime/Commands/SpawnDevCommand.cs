#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Factions;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.DevRuntime.Commands
{
    public sealed class SpawnDevCommand : IDevCommand
    {
        public string Name => "spawn";
        public string Usage => "spawn blueprint=<id> [x=<m> z=<m> | dx=<m> dz=<m>] [yaw=<deg> | face=player] [faction=<id>] [team=1] [count=1] [spacing=60] [ai=true]";
        public string Summary => "Request ships through ShipSpawnService and report their persistent IDs";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "blueprint", "x", "z", "dx", "dz", "yaw", "face", "faction", "team", "count", "spacing", "ai");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            if (!ctx.HasGameplay || !ctx.TryGetService<ShipSpawnService>(out var spawner) || !ctx.TryGetService<ContentService>(out var content)) return DevCommandResult.Fail("ship spawning unavailable");
            if (!args.TryGetString("blueprint", out string blueprintId) || !content.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId(blueprintId), out var blueprint)) return DevCommandResult.Fail("unknown or missing blueprint");
            if (!ctx.TryGetService<GalaxyService>(out var galaxy) || !galaxy.State.CurrentStarSystem.IsValid) return DevCommandResult.Fail("ship spawning requires a current star system");
            long count = args.GetLong("count", 1), team = args.GetLong("team", 1);
            double spacing = args.GetDouble("spacing", 60);
            if (count < 1 || count > int.MaxValue || team < 0 || team > byte.MaxValue || spacing < 0) return DevCommandResult.Fail("count must be positive, team in 0..255, spacing nonnegative");
            bool absolute = args.Has("x") || args.Has("z");
            if ((absolute && !(args.Has("x") && args.Has("z"))) || (absolute && (args.Has("dx") || args.Has("dz")))) return DevCommandResult.Fail(Usage);
            string face = args.GetString("face", "");
            if ((args.Has("face") && face != "player") || (args.Has("face") && args.Has("yaw"))) return DevCommandResult.Fail("use yaw or face=player");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            var player = ctx.PlayerShipRoot; var playerPosition = double3.zero;
            if (!absolute || face == "player")
            {
                if (player == Entity.Null || !em.HasComponent<LocalTransform>(player)) return DevCommandResult.Fail("relative placement requires a player transform");
                playerPosition = em.GetComponentData<LocalTransform>(player).Position;
            }
            var start = absolute ? new double3(args.GetDouble("x", 0), 0, args.GetDouble("z", 0)) :
                playerPosition + new double3(args.GetDouble("dx", 0), 0, args.GetDouble("dz", 80));
            var last = start + new double3((count - 1) * spacing, 0, 0);
            float yaw = (float)math.radians(args.GetDouble("yaw", 0));
            if (!math.all(math.isfinite((float3)start)) || !math.all(math.isfinite((float3)last)) || !float.IsFinite(yaw)) return DevCommandResult.Fail("spawn pose exceeds local coordinate range");
            var allegiance = new Allegiance { Faction = 255, Team = (byte)team }; ContentId aiProfile = default;
            if (args.TryGetString("faction", out string factionId))
            {
                if (!content.Catalog.TryGet<FactionDefinition>(new ContentId(factionId), out var faction)) return DevCommandResult.Fail("unknown faction");
                FactionIndexMap indices;
                if (ctx.TryGetService<FactionService>(out var factions)) indices = factions.Indices;
                else if (!ctx.TryGetService(out indices)) return DevCommandResult.Fail("faction index map unavailable");
                allegiance.Faction = indices.IndexOf(faction);
                if (allegiance.Faction == 255) return DevCommandResult.Fail("faction has no runtime index");
                if (faction.DefaultAiProfile != null) aiProfile = faction.DefaultAiProfile.Id;
            }
            bool withAi = args.GetBool("ai", true); var ids = new PersistentId[(int)count];
            for (int i = 0; i < ids.Length; i++)
            {
                var position = start + new double3(i * spacing, 0, 0);
                var rotation = face == "player" ? Planar.YawRotation((float)math.atan2(playerPosition.x - position.x, playerPosition.z - position.z)) : Planar.YawRotation(yaw);
                ids[i] = spawner.Spawn(new ShipSpawnArgs { Blueprint = blueprint.Id, Position = (float3)position, Rotation = rotation,
                    Allegiance = allegiance, WithAi = withAi, AiProfile = aiProfile });
                if (ids[i].IsNone) return DevCommandResult.Fail("ship request failed");
            }
            int operation = ctx.Host.AddOperation(new SpawnOperation(ctx.World, spawner, ids));
            return DevCommandResult.Pending(operation, "requested " + ids.Length.ToString(CultureInfo.InvariantCulture) + " ships");
        }
        sealed class SpawnOperation : IDevOperation
        {
            readonly World world; readonly ShipSpawnService spawner; readonly PersistentId[] ids;
            public SpawnOperation(World world, ShipSpawnService spawner, PersistentId[] ids) { this.world = world; this.spawner = spawner; this.ids = ids; }
            public DevOperationStatus Status { get; private set; } = DevOperationStatus.Running;
            public string Message { get; private set; } = "waiting for grid roots";
            public double TimeoutSeconds => 10;
            public void Tick(DevContext ctx)
            {
                if (ctx.World != world || !world.IsCreated) { Status = DevOperationStatus.Failed; Message = "spawn world changed"; return; }
                var em = world.EntityManager; em.CompleteAllTrackedJobs();
                foreach (var id in ids)
                {
                    if (!spawner.TryGetRoot(id, out var root)) { Status = DevOperationStatus.Failed; Message = "spawn request disappeared"; return; }
                    if (!em.HasComponent<VoxelGrid>(root) || (em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root))) return;
                }
                var values = new string[ids.Length]; for (int i = 0; i < ids.Length; i++) values[i] = ids[i].Value.ToString(CultureInfo.InvariantCulture);
                Message = "spawned " + ids.Length + ": ids " + string.Join(", ", values); Status = DevOperationStatus.Succeeded;
            }
        }
    }
}
#endif
