using Game.App.Session;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>HAZ-013 pad: two moving structures to fly against (ADR-0119 decision 1). A 45 m sweeper arm turning about its hub, and a
    /// blast door that slides open and shut on a ping-pong rail. Both are kinematic voxel grids: they bat ships aside, ram them, and
    /// can be shot apart. One set per reset.</summary>
    internal static class DevPlaygroundMovers
    {
        internal const string PadDetail = "F parks a 45 m sweeper arm turning about its hub (one turn in 21 s) and a blast door that slides open and shut every 4 s.\n" +
            "Both are kinematic voxel structures: fly into their path to get batted aside and take ram damage; shoot them apart. One set per reset.";

        internal static DevCommandResult Activate(DevContext ctx, double3 padPosition, ref bool spawned)
        {
            if (spawned) return DevCommandResult.Ok("The arm and the door are already moving ahead. playground reset clears them.");
            if (!ctx.HasGameplay || !ctx.TryGetService<GameSession>(out var session) || !ctx.TryGetService<ContentService>(out var content) || !ctx.TryGetFrame(out var frame))
                return DevCommandResult.Fail("Moving structures need the isolated playground.");
            if (!content.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_track_arm"), out var arm) ||
                !content.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_track_door"), out var door))
                return DevCommandResult.Fail("Mechanism props missing: run Game/Content/Build Mechanism Props.");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            uint tick = (uint)ctx.Tick + 2;

            double3 hub = padPosition + new double3(0, 0, 110);
            var armRoot = Spawn(em, session, arm, hub, frame);
            HazardSpawn.AddMover(em, armRoot, new Mover { Path = MoverPath.Rotate, Origin = (float3)(hub - frame.OriginInStarSystem), Rate = .3f, StartTick = tick, Playing = 1 });

            double3 doorAt = padPosition + new double3(-90, 0, 60);
            var doorRoot = Spawn(em, session, door, doorAt, frame);
            float3 shut = (float3)(doorAt - frame.OriginInStarSystem), open = shut + new float3(-23, 0, 0);
            HazardSpawn.AddMover(em, doorRoot, new Mover { Path = MoverPath.Rail, Speed = 11.5f, StartTick = tick, Playing = 1, PingPong = 1 },
                new[] { new MoverPoint { Pose = shut }, new MoverPoint { Pose = open } });
            spawned = true;
            return DevCommandResult.Ok("Sweeper arm ahead (turning about its hub) and a blast door to the left (sliding every 4 s). Fly through them.");
        }

        static Entity Spawn(EntityManager em, GameSession session, ShipBlueprintDefinition blueprint, double3 at, FrameOrigin frame)
        {
            var root = GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new GridSpawnParams
            {
                PersistentId = session.Allocate(), Pose = new Unity.Mathematics.RigidTransform(quaternion.identity, (float3)(at - frame.OriginInStarSystem)),
                Kind = GridKind.Station, Motion = GridMotion.Kinematic, Profile = GridProfile.Dummy, Flags = GridSpawnFlags.Transient
            });
            em.AddComponentData(root, new UniversePosition { Value = at });
            return root;
        }
    }
}
