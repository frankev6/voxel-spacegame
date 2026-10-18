#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Game.App.Session;
using Game.App.Spawning;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Game.App.DevRuntime.Playground.PlaygroundHub;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-022: the simulated half of the ARK Proving Grounds. At start: the hub's structures, the lane-gate portals (HAZ-021)
    /// between the plaza and every district, and the traffic on the two loops (HAZ-013 rail movers). Each district's exhibits spawn
    /// the first time a pilot comes near it, and its reset pad despawns and respawns them. All through the simulation owners' spawn
    /// APIs; entities are transient sandbox state that the session reset clears.</summary>
    internal sealed class PlaygroundHubWorld
    {
        const ushort PortalGroup = 38, FirstGroup = 40;
        readonly DevContext ctx;
        readonly double3 origin;
        readonly HashSet<District> populated = new();
        readonly Dictionary<District, List<Entity>> roots = new();
        readonly List<string> missing = new();
        double3 rangeAt; quaternion rangeRotation; bool hasRange;

        internal PlaygroundHubWorld(DevContext ctx, double3 origin) { this.ctx = ctx; this.origin = origin; }

        internal static ushort GroupOf(District d) => (ushort)(FirstGroup + System.Array.IndexOf(Districts, d));
        internal bool IsPopulated(District d) => populated.Contains(d);
        internal string Missing => missing.Count == 0 ? null : "Hub content missing (" + string.Join(", ", missing) + "): run Game/Content/Build Playground Hub.";

        // ------------------------------------------------------------------------------------------------ helpers
        bool Ready(out GameSession session, out ContentCatalog catalog, out FrameOrigin frame)
        {
            session = null; catalog = null; frame = default;
            if (!ctx.HasGameplay || !ctx.TryGetService(out session) || !ctx.TryGetService<ContentService>(out var content) || !ctx.TryGetFrame(out frame)) return false;
            catalog = content.Catalog; return true;
        }
        float2 Sim(float2 p, FrameOrigin frame) => ((float3)(origin + new double3(p.x, 0, p.y) - frame.OriginInStarSystem)).xz;
        double3 Universe(float2 p) => origin + new double3(p.x, 0, p.y);

        /// <summary>A voxel blueprint whose local point `pivot` (metres, x and z) should stand at `p` (playground-relative), turned to `yaw`.</summary>
        Entity Structure(ContentCatalog catalog, GameSession session, FrameOrigin frame, string id, float2 p, float yaw, float2 pivot = default,
            GridKind kind = GridKind.Station, GridMotion motion = GridMotion.Static, District owner = null)
        {
            if (!catalog.TryGet<ShipBlueprintDefinition>(new ContentId(id), out var blueprint)) { if (!missing.Contains(id)) missing.Add(id); return Entity.Null; }
            var em = ctx.EntityManager;
            var rotation = quaternion.RotateY(yaw);
            float2 at = p - math.mul(rotation, new float3(pivot.x, 0, pivot.y)).xz;
            var root = GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new GridSpawnParams
            {
                PersistentId = session.Allocate(), Pose = new RigidTransform(rotation, new float3(Sim(at, frame).x, 0, Sim(at, frame).y)),
                Kind = kind, Motion = motion, Profile = GridProfile.Dummy, Flags = GridSpawnFlags.Transient
            });
            em.AddComponentData(root, new UniversePosition { Value = Universe(at) });
            if (kind == GridKind.Hulk)
            {
                em.AddComponent<Game.Voxel.Damage.Hulk>(root);
                em.AddComponentData(root, new Game.Voxel.Damage.HulkInfo { Cause = Game.Voxel.Damage.HulkCause.Split, Tick = (uint)ctx.Tick, FormerKind = GridKind.Ship });
            }
            if (owner != null) Own(owner, root);
            return root;
        }
        void Own(District d, Entity e) { if (!roots.TryGetValue(d, out var list)) roots[d] = list = new List<Entity>(); list.Add(e); }

        static float2 PivotOf(string id) => id switch
        {
            Pylon => new float2(.5f, .5f),
            Kiosk => new float2(.5f, 0),
            _ => default,
        };

        // ------------------------------------------------------------------------------------------------ the hub
        /// <summary>The permanent hub: structures, portals and traffic.</summary>
        internal void Build()
        {
            if (!Ready(out var session, out var catalog, out var frame)) return;
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            missing.Clear();
            if (ctx.TryGetService<FiringRangeService>(out var range) && em.Exists(range.Target) && em.HasComponent<Unity.Transforms.LocalTransform>(range.Target))
            {
                var pose = em.GetComponentData<Unity.Transforms.LocalTransform>(range.Target);
                rangeAt = frame.OriginInStarSystem + (double3)pose.Position; rangeRotation = pose.Rotation; hasRange = true;
            }
            foreach (var piece in Pieces()) Structure(catalog, session, frame, piece.Blueprint, piece.Position, piece.Yaw, PivotOf(piece.Blueprint));
            if (catalog.TryGet<ZoneDefinition>(new ContentId("zone_gate"), out var gate))
                foreach (var d in Districts)
                {
                    HazardSpawn.Portal(em, gate, Sim(d.PlazaPortal, frame), d.PortalBearing, Sim(d.Arrival, frame), d.Yaw, PortalWidth, 0, PortalGroup,
                        session.Allocate(), session.Allocate());
                    float back = d.PortalBearing + ReturnOffset + math.PI;
                    HazardSpawn.Portal(em, gate, Sim(d.ReturnPortal, frame), d.Yaw + math.PI, Sim(d.PlazaReturn, frame), back, 44, 0, PortalGroup,
                        session.Allocate(), session.Allocate());
                }
            else missing.Add("zone_gate");
            Traffic(catalog, session, frame, "ship_shuttle", InnerLoop, 4, 28, false);
            Traffic(catalog, session, frame, "ship_freighter", OuterLoop, 3, 36, true);
        }

        /// <summary>`count` ships of `id` round a closed rail loop about the plaza, evenly spaced; clockwise unless `reverse`.</summary>
        void Traffic(ContentCatalog catalog, GameSession session, FrameOrigin frame, string id, float radius, int count, float speed, bool reverse)
        {
            const int points = 72;
            var loop = Loop(radius, points);
            uint tick = (uint)ctx.Tick + 2;
            for (int k = 0; k < count; k++)
            {
                int first = k * points / count;
                var path = new MoverPoint[points];
                for (int i = 0; i < points; i++)
                {
                    int j = reverse ? (first - i + points) % points : (first + i) % points;
                    float bearing = j * math.PI * 2 / points;
                    path[i] = new MoverPoint { Pose = new float3(Sim(loop[j], frame).x, 0, Sim(loop[j], frame).y), Yaw = MoverMath.Wrap(bearing + (reverse ? -math.PI / 2 : math.PI / 2)) };
                }
                var root = Structure(catalog, session, frame, id, loop[first], path[0].Yaw, default, GridKind.Station, GridMotion.Kinematic);
                if (root == Entity.Null) return;
                HazardSpawn.AddMover(ctx.EntityManager, root, new Mover { Path = MoverPath.Rail, Speed = speed, StartTick = tick, Playing = 1, Group = PortalGroup }, path);
            }
        }

        // ------------------------------------------------------------------------------------------------ districts
        /// <summary>Spawns a district's exhibits once (a pilot came near).</summary>
        readonly Dictionary<District, string> intros = new();
        /// <summary>What the district's exhibits are, once they have spawned (shown when a pilot arrives).</summary>
        internal string Intro(District d) => intros.TryGetValue(d, out var text) ? text : null;

        internal string Populate(District d)
        {
            if (populated.Contains(d) || !Ready(out _, out _, out _)) return null;
            var text = Spawn(d);
            if (text != null) intros[d] = text;
            return text;
        }

        string Spawn(District d)
        {
            if (!Ready(out var session, out var catalog, out var frame)) return null;
            populated.Add(d);
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            ushort group = GroupOf(d);
            uint tick = (uint)ctx.Tick + 2;
            switch (d.Id)
            {
                case "flight":
                    if (catalog.TryGet<ZoneDefinition>(new ContentId("zone_boost_strip"), out var boost))
                    {
                        var strip = HazardSpawn.Zone(em, boost, Sim(Flight.At(175, -40), frame), Flight.Yaw, group, session.Allocate());
                        var w = em.GetComponentData<WorldZone>(strip); w.HalfExtents = new float2(20, 88); em.SetComponentData(strip, w);
                    }
                    return "Flight school: slalom gates, the boost strip on the right and the brake box on the left.";
                case "weapons":
                    foreach (float range in GalleryRanges)
                        foreach (float side in new[] { -170f, 170f })
                            Structure(catalog, session, frame, "ship_fighter", Weapons.At(side, RangePadZ + range), Weapons.Yaw + math.PI, default, GridKind.Station, GridMotion.Static, d);
                    return "Weapons range: the target station ahead and six target fighters, 100 to 260 m from the pad.";
                case "hazards": return Hazard(catalog, session, frame, group, tick);
                case "mechanisms": return Mechanism(catalog, session, frame, group, tick);
                case "shipyard":
                    for (int i = 0; i < Bays.Length; i++)
                        Structure(catalog, session, frame, Bays[i].blueprint, BayAt(i), Shipyard.Yaw + math.PI, default, GridKind.Station, GridMotion.Static, d);
                    return "Shipyard: ten ships on display. Fly onto a pad in front of one and press F to fly it.";
                case "market":
                    Structure(catalog, session, frame, "ship_freighter", Market.At(95, -40), Market.Yaw, default, GridKind.Station, GridMotion.Static, d);
                    Structure(catalog, session, frame, "ship_shuttle", Market.At(-150, 60), Market.Yaw + 2.4f, default, GridKind.Station, GridMotion.Static, d);
                    Structure(catalog, session, frame, "ship_shuttle", Market.At(-110, 120), Market.Yaw + 2.1f, default, GridKind.Station, GridMotion.Static, d);
                    return "Market docks: a merchant freighter at the pier. The TRADE pad hails it.";
                case "salvage":
                    // a scrapyard: drifting hull chunks and two gutted ships (the SALVAGE pad prepares the one you can pull in)
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 2.39996f, r = 90 + 18 * i;
                        Structure(catalog, session, frame, "ship_track_debris_" + (i % 8), Salvage.At(math.sin(a) * r, 60 + math.cos(a) * r * .7f), a * 3, default, GridKind.Hulk, GridMotion.Static, d);
                    }
                    Structure(catalog, session, frame, "ship_frigate", Salvage.At(-170, 40), Salvage.Yaw + 1.2f, default, GridKind.Hulk, GridMotion.Static, d);
                    Structure(catalog, session, frame, "ship_striker", Salvage.At(150, 150), Salvage.Yaw - 2.2f, default, GridKind.Hulk, GridMotion.Static, d);
                    return "Salvage yard: hull chunks and gutted ships. The SALVAGE pad prepares a wreck you can pull into your hold.";
            }
            return null;
        }

        /// <summary>The district's exhibits go and come back as new (combat: every hostile AI ship is despawned).</summary>
        internal void Reset(District d)
        {
            if (!ctx.HasGameplay) return;
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            HazardSpawn.DespawnGroup(em, GroupOf(d));
            if (roots.TryGetValue(d, out var list))
            {
                foreach (var e in list) if (em.Exists(e) && em.HasComponent<GridDespawnRequest>(e)) GridSpawnApi.RequestDespawn(em, e); else if (em.Exists(e)) em.DestroyEntity(e);
                list.Clear();
            }
            if (d == Combat) DespawnHostiles();
            if (d == Weapons)
            {
                // the pieces shot off the station and the targets become hulks: clear every hulk in the range, then a new station
                DespawnHulks(d);
                if (hasRange && ctx.TryGetService<FiringRangeService>(out var range) && ctx.TryGetFrame(out var frame))
                    range.Restore(new RigidTransform(rangeRotation, (float3)(rangeAt - frame.OriginInStarSystem)));
            }
            populated.Remove(d);
            Populate(d);
        }

        void DespawnHulks(District d)
        {
            var em = ctx.EntityManager;
            if (!ctx.TryGetFrame(out var frame)) return;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<Unity.Transforms.LocalTransform>());
            using var grids = q.ToEntityArray(Allocator.Temp);
            float2 centre = Sim(d.Centre, frame);
            foreach (var e in grids)
            {
                if (em.GetComponentData<VoxelGrid>(e).Kind != GridKind.Hulk || !em.HasComponent<GridDespawnRequest>(e)) continue;
                if (math.distancesq(em.GetComponentData<Unity.Transforms.LocalTransform>(e).Position.xz, centre) < (d.Radius + 150) * (d.Radius + 150))
                    GridSpawnApi.RequestDespawn(em, e);
            }
        }

        void DespawnHostiles()
        {
            if (!ctx.TryGetService<ShipSpawnService>(out var spawner)) return;
            var em = ctx.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<AiBrain>(), ComponentType.ReadOnly<Game.Core.Components.PersistentId>());
            using var ids = q.ToComponentDataArray<Game.Core.Components.PersistentId>(Allocator.Temp);
            foreach (var id in ids) spawner.Despawn(id);
        }

        /// <summary>HAZ-016/018/019: a reactor pulsing every 4 s over 140 m with a slab to hide behind, a row of energy canisters on
        /// the left and one mine of each payload on the right.</summary>
        string Hazard(ContentCatalog catalog, GameSession session, FrameOrigin frame, ushort group, uint tick)
        {
            var em = ctx.EntityManager;
            if (!catalog.TryGet<ZoneDefinition>(new ContentId("zone_pulse_reactor"), out var reactor) ||
                !catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_device_canister"), out var canister))
                return "Hazard content missing: run Game/Content/Build Hazard Content.";
            var zone = HazardSpawn.Zone(em, reactor, Sim(Hazards.At(0, 60), frame), 0, group, session.Allocate());
            HazardSpawn.Schedule(em, zone, 4, 1f / 60, 2);
            Structure(catalog, session, frame, "ship_track_door", Hazards.At(0, -20), Hazards.Yaw, default, GridKind.Station, GridMotion.Static, Hazards);
            for (int i = 0; i < 5; i++) HazardSpawn.Canister(em, canister, Sim(Hazards.At(-180, -110 + 45 * i), frame), 0, group, session.Allocate());
            string[] mines = { "mine_repulsor", "mine_emp", "mine_seeker", "mine_gravity" };
            for (int i = 0; i < mines.Length; i++)
                if (catalog.TryGet<MineDefinition>(new ContentId(mines[i]), out var mine))
                    HazardSpawn.Mine(em, mine, Sim(Hazards.At(185, MineZ(i)), frame), 0, group, session.Allocate(), tick);
            return "Hazard yard: the reactor pulses every 4 s (hide behind the slab), canisters on the left, a mine of each payload on the right.";
        }

        /// <summary>HAZ-013/014/021: a 45 m sweeper arm, a sliding blast door, a switch door with its gold switch post, and a portal
        /// that jams for 6 s after each ship.</summary>
        string Mechanism(ContentCatalog catalog, GameSession session, FrameOrigin frame, ushort group, uint tick)
        {
            var em = ctx.EntityManager; var d = Mechanisms;
            float2 hub = d.At(-150, 40);
            var arm = Structure(catalog, session, frame, "ship_track_arm", hub, d.Yaw, default, GridKind.Station, GridMotion.Kinematic, d);
            if (arm == Entity.Null) return Missing;
            HazardSpawn.AddMover(em, arm, new Mover { Path = MoverPath.Rotate, Origin = new float3(Sim(hub, frame).x, 0, Sim(hub, frame).y), Rate = .3f, Yaw = d.Yaw, StartTick = tick, Playing = 1 });
            float3 P(float2 p) => new float3(Sim(p, frame).x, 0, Sim(p, frame).y);
            // the sliding door: open and shut every 2 s along the district's x
            var slide = Structure(catalog, session, frame, "ship_track_door", d.At(110, -110), d.Yaw, default, GridKind.Station, GridMotion.Kinematic, d);
            HazardSpawn.AddMover(em, slide, new Mover { Path = MoverPath.Rail, Speed = 11.5f, StartTick = tick, Playing = 1, PingPong = 1 },
                new[] { new MoverPoint { Pose = P(d.At(110, -110)), Yaw = d.Yaw }, new MoverPoint { Pose = P(d.At(87, -110)), Yaw = d.Yaw } });
            // the switch door, held open; shooting the gold post slams it for 5 s (HAZ-014)
            if (catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_track_switch"), out _) &&
                catalog.TryGet<MechanismDefinition>(new ContentId("mechanism_blast_door"), out var mechanism))
            {
                var slam = Structure(catalog, session, frame, "ship_track_door", d.At(110, 10), d.Yaw, default, GridKind.Station, GridMotion.Kinematic, d);
                HazardSpawn.AddMover(em, slam, new Mover { Path = MoverPath.Toggle, Rate = 1f, Playing = 1, Reverse = 1, StartTick = 0, Group = group },
                    new[] { new MoverPoint { Pose = P(d.At(110, 10)), Yaw = d.Yaw }, new MoverPoint { Pose = P(d.At(87, 10)), Yaw = d.Yaw } });
                var post = Structure(catalog, session, frame, "ship_track_switch", d.At(70, -20), d.Yaw, default, GridKind.Station, GridMotion.Static, d);
                if (post != Entity.Null) em.AddComponentData(post, new MechanismGroup { Group = group });
                HazardSpawn.Mechanism(em, mechanism, group);
            }
            // the portal: GATE IN ahead on the right, GATE OUT back near the entrance on the left, jammed 6 s after each ship
            if (catalog.TryGet<ZoneDefinition>(new ContentId("zone_gate"), out var gate))
                HazardSpawn.Portal(em, gate, Sim(d.At(40, 140), frame), d.Yaw, Sim(d.At(-60, -150), frame), d.Yaw, 50, 6, group, session.Allocate(), session.Allocate());
            return "Mechanism works: the sweeper arm on the left, the sliding and switch doors on the right, the portal ahead.";
        }
    }
}
#endif
