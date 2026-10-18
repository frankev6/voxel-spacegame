using Game.App.Session;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Simulation.Hazards;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>HAZ-016/018/019 pad: the hazard set to fly through. A reactor pulsing every 4 s over 140 m (hide behind the slab
    /// between it and the pad), a row of energy canisters to collect or shoot, and one mine of each payload. One set per reset.</summary>
    internal static class DevPlaygroundHazards
    {
        internal const ushort Group = 19;
        internal const string PadDetail = "F places a reactor ahead that pulses every 4 s over 140 m: its rim brightens before each pulse, and the slab between\n" +
            "it and the pad casts a shadow you can hide in. Energy canisters line the left (fly through to top up power, shoot one and it blows);\n" +
            "on the right, one mine of each payload: repulsor, EMP, seeker and gravity. One set per reset.";

        internal static DevCommandResult Activate(DevContext ctx, double3 padPosition, ref bool spawned)
        {
            if (spawned) return DevCommandResult.Ok("The hazards are already placed ahead. playground reset clears them.");
            if (!ctx.HasGameplay || !ctx.TryGetService<GameSession>(out var session) || !ctx.TryGetService<ContentService>(out var content) || !ctx.TryGetFrame(out var frame))
                return DevCommandResult.Fail("Hazards need the isolated playground.");
            var catalog = content.Catalog;
            if (!catalog.TryGet<ZoneDefinition>(new ContentId("zone_pulse_reactor"), out var reactor) ||
                !catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_device_canister"), out var canister) ||
                !catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_track_door"), out var slab))
                return DevCommandResult.Fail("Hazard content missing: run Game/Content/Build Hazard Content and Build Mechanism Props.");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            uint tick = (uint)ctx.Tick + 2;
            float2 Sim(double3 at) => ((float3)(at - frame.OriginInStarSystem)).xz;

            var zone = HazardSpawn.Zone(em, reactor, Sim(padPosition + new double3(0, 0, 190)), 0, Group, session.Allocate());
            HazardSpawn.Schedule(em, zone, 4, 1f / 60, 2);
            // The slab: a static blast door between the pad and the core; its shadow runs back toward the pad.
            var slabAt = padPosition + new double3(0, 0, 110);
            var slabRoot = GridSpawnApi.RequestSpawnBlueprint(em, slab.Id.Hash, new GridSpawnParams
            {
                PersistentId = session.Allocate(), Pose = new RigidTransform(quaternion.identity, (float3)(slabAt - frame.OriginInStarSystem)),
                Kind = GridKind.Station, Motion = GridMotion.Static, Profile = GridProfile.Dummy, Flags = GridSpawnFlags.Transient
            });
            em.AddComponentData(slabRoot, new UniversePosition { Value = slabAt });
            for (int i = 0; i < 5; i++)
                HazardSpawn.Canister(em, canister, Sim(padPosition + new double3(-150, 0, 50 + 40 * i)), 0, Group, session.Allocate());
            string[] mines = { "mine_repulsor", "mine_emp", "mine_seeker", "mine_gravity" };
            for (int i = 0; i < mines.Length; i++)
                if (catalog.TryGet<MineDefinition>(new ContentId(mines[i]), out var mine))
                    HazardSpawn.Mine(em, mine, Sim(padPosition + new double3(150 + (i == 2 ? 60 : 0), 0, 60 + 55 * i)), 0, Group, session.Allocate(), tick);
            spawned = true;
            return DevCommandResult.Ok("Reactor ahead (pulses every 4 s; hide behind the slab), canisters on the left, mines on the right.");
        }

        internal static void Clear(DevContext ctx)
        {
            if (!ctx.HasGameplay) return;
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            HazardSpawn.DespawnGroup(em, Group);
        }
    }
}
