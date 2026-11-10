#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Galaxy.Components;
using Game.Data.Components;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Extraction;
using Game.Simulation.Mechanisms;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using static Game.App.DevRuntime.Extraction.LanternLayout;

namespace Game.App.DevRuntime.Extraction
{
    /// <summary>SPEC-93 EXT-007: Tollhouse Nine, "the Lantern", in the playground's system (owner, 2026-11-10: "a bustling station ... more
    /// like a hitman map with multitude of options"). The town's five levels as static station grids; its doors as Toggle movers; the
    /// station core in the Cradle on six tethers, one per district; the districts' lamps; watchers (searchlights in the Well, crowds on
    /// the galleries, the arch's toll scanner, cameras, the garrison post); access zones (traders in the market, work in the Powerhouse
    /// and the Yards, the garrison in the Crown); guns; a garrison cutter on patrol; traffic on the galleries, at the docks and in the
    /// toll queue; the fuel depot; and the town's machinery on a compressed clock: the outfall flush, the mass driver, Lantern Night's
    /// Dimming. The alarm's timeline is the HeistDirector's (doors 0:10, driver 0:30, cutters 1:00, lane scans 2:00, the cruiser 6:00).</summary>
    internal sealed class LanternPlace : IDisposable
    {
        internal const string Alliance = "faction_tlaxcala", Civil = "faction_vestari";
        internal const float FlushPeriod = 60, FlushSeconds = 8, FlushAccel = 26, DriverPeriod = 30, DriverSpeed = 240, PodCatch = 1600;
        internal const ushort DoorBig = 41, DoorGrate = 42, DoorHatch = 43, DoorStaff = 44, DoorCrown = 45, Group = 70;
        readonly ExtractionKit kit;
        internal readonly double3 Anchor;
        readonly List<Entity> grids = new(), entities = new(), posts = new();
        readonly List<PersistentId> ships = new();
        readonly List<(PersistentId id, int route, float along)> traffic = new();
        readonly List<Entity> tanks = new();
        readonly Dictionary<ushort, List<Light>> lamps = new();
        GameObject dressing; Light coreLight;
        internal Entity Powerhouse, Core, Heist;
        internal PersistentId Patrol, Hauler, Cruiser;
        readonly List<PersistentId> cutters = new();
        internal byte AllianceIndex = 255, CivilIndex = 255;
        internal bool Built, Shroud, DimmingNight, DriverLocked, ScansOn, InsideMan, HaulerLeaving;
        internal int DepotBlown;
        double builtAt, dimmingAt = -1, cuttersHomeAt = -1, steeredAt;
        float3 cuttersPost;
        internal Action<string> Toast = _ => { };

        internal LanternPlace(ExtractionKit kit, double3 anchor) { this.kit = kit; Anchor = anchor; }

        internal float3 Sim(float3 local) => kit.Ready(out _, out _, out var frame) ? ExtractionKit.Sim(Anchor, local, frame) : local;

        internal void Build()
        {
            if (Built || !kit.Ready(out var session, out var catalog, out var frame)) return;
            var em = kit.Ctx.EntityManager; em.CompleteAllTrackedJobs();
            Built = true; builtAt = Time.timeAsDouble;
            AllianceIndex = kit.Faction(catalog, Alliance); CivilIndex = kit.Faction(catalog, Civil);
            kit.Relation(catalog, Alliance, 0);   // the Alliance tolls you; it does not shoot traders
            var alliance = new Allegiance { Faction = AllianceIndex, Team = 4 };
            uint tick = kit.Tick;

            // the town: scenery (shots stop on it, nothing breaks off)
            foreach (var id in Pieces)
            {
                var root = kit.Grid(session, catalog, frame, Anchor, id, float3.zero, quaternion.identity, GridKind.Station, GridMotion.Static);
                if (root == Entity.Null) continue;
                em.AddComponent<SpatialScenery>(root); em.AddComponent<DamageImmune>(root); em.AddComponent<CameraSeeThrough>(root);
                grids.Add(root);
                if (id == "ship_lantern_powerhouse") Powerhouse = root;
            }
            // doors: Toggle movers between closed and open; the big door and the floor hatch break under fire
            foreach (var d in Doors)
            {
                var root = kit.Grid(session, catalog, frame, Anchor, d.Blueprint, d.Closed, quaternion.identity, GridKind.Station, GridMotion.Kinematic);
                if (root == Entity.Null) continue;
                grids.Add(root);
                if (d.Id != "bigdoor" && d.Id != "hatch") em.AddComponent<DamageImmune>(root);
                em.AddComponent<CameraSeeThrough>(root);
                Span<MoverPoint> points = stackalloc MoverPoint[] { new MoverPoint { Pose = Sim(d.Closed) }, new MoverPoint { Pose = Sim(d.Closed + d.Open) } };
                Game.Simulation.Hazards.HazardSpawn.AddMover(em, root, new Mover { Path = MoverPath.Toggle, Origin = Sim(d.Closed), Rate = 1 / 5f, StartTick = tick, Group = d.Group }, points);
            }
            // districts and their lamps
            dressing = new GameObject("Tollhouse Nine dressing");
            foreach (var (id, _) in LanternLayout.Districts)
            {
                var e = em.CreateEntity(typeof(District)); em.SetComponentData(e, new District { Id = id, Powered = 1 }); entities.Add(e);
                lamps[id] = new List<Light>();
            }
            Lamps();
            // the core in the Cradle, held by six tethers on posts
            Core = kit.Grid(session, catalog, frame, Anchor, ExtractionIds.Core, LanternLayout.Core, quaternion.identity, GridKind.Asteroid, GridMotion.Dynamic);
            if (Core != Entity.Null)
            {
                em.AddComponent<DamageImmune>(Core);
                em.AddComponentData(Core, new StationCore { Holder = Powerhouse, HoldLocal = LanternLayout.Core, HoldRotation = quaternion.identity, Tethers = (byte)Tethers.Length });
                grids.Add(Core);
                coreLight = ExtractionKit.Lamp(dressing.transform, Vector3.zero, new Color(.55f, 1f, 1f), 160, 9000);
            }
            foreach (var t in Tethers)
            {
                var post = kit.Grid(session, catalog, frame, Anchor, ExtractionIds.Post, t.Post, quaternion.identity, GridKind.Station, GridMotion.Static);
                if (post != Entity.Null) { grids.Add(post); posts.Add(post); }
                var e = em.CreateEntity(typeof(CoreTether));
                em.SetComponentData(e, new CoreTether { Core = Core, Holder = Powerhouse, Anchor = t.Post, District = t.District, Post = post });
                entities.Add(e);
            }
            // watchers and access zones
            foreach (var w in Watchers)
            {
                var e = em.CreateEntity(typeof(LocalTransform), typeof(Watcher));
                em.SetComponentData(e, LocalTransform.FromPosition(Sim(w.At)));
                var kind = w.Kind switch { "Light" => WatcherKind.Light, "Sensor" => WatcherKind.Sensor, "Crowd" => WatcherKind.Crowd, _ => WatcherKind.Ship };
                em.SetComponentData(e, new Watcher { Kind = kind, Faction = AllianceIndex, Facing = math.normalizesafe(w.Facing, new float3(0, 0, 1)), Range = w.Range,
                    CosHalfArc = math.cos(math.radians(w.HalfArcDeg)), SweepRadians = math.radians(w.SweepDeg), SweepSeconds = w.SweepSeconds, SweepPhase = entities.Count * .17f,
                    District = w.District });
                entities.Add(e);
                if (kind == WatcherKind.Light && lamps.TryGetValue(w.District, out var list))
                {
                    var spot = ExtractionKit.Lamp(dressing.transform, (Vector3)w.At, new Color(1f, .95f, .8f), w.Range, 1.2f * w.Range * w.Range, LightType.Spot);
                    spot.spotAngle = w.HalfArcDeg * 2; spot.transform.rotation = Quaternion.LookRotation((Vector3)math.normalizesafe(w.Facing));
                    list.Add(spot);
                }
            }
            foreach (var z in Zones)
            {
                var e = em.CreateEntity(typeof(LocalTransform), typeof(AccessZone));
                em.SetComponentData(e, LocalTransform.FromPosition(Sim(z.Centre)));
                var needs = z.Needs switch { "Garrison" => AccessCode.Garrison, "Work" => AccessCode.Work, "Trader" => AccessCode.Trader, _ => AccessCode.None };
                em.SetComponentData(e, new AccessZone { HalfExtents = z.Half, Needs = needs });
                entities.Add(e);
            }
            // guns on the Crown and the docks: Alliance turrets, quiet unless someone is wanted
            ushort turret = kit.Profile(catalog, "ai_turret");
            foreach (var at in Turrets)
            {
                var root = kit.Grid(session, catalog, frame, Anchor, "ship_hollow_gun_light", at, quaternion.identity, GridKind.Station, GridMotion.Static, GridProfile.Station);
                if (root == Entity.Null) continue;
                em.AddComponentData(root, alliance); em.AddComponentData(root, new AiBrain { ProfileIndex = turret });
                grids.Add(root);
            }
            // the fuel depot: three tanks that burn
            for (int i = 0; i < Depot.Length; i++)
            {
                var root = kit.Grid(session, catalog, frame, Anchor, ExtractionIds.Tank + i, Depot[i].At, quaternion.identity, GridKind.Station, GridMotion.Static);
                if (root != Entity.Null) { grids.Add(root); tanks.Add(root); }
            }
            // the heist: the alarm's timeline (SPEC-93 decision 8; lantern.py "When the last tether is cut")
            Heist = em.CreateEntity(typeof(HeistState), typeof(AlarmStep));
            em.SetComponentData(Heist, new HeistState { Faction = AllianceIndex, Core = Core, EscapeRadius = 2400 });
            var steps = em.GetBuffer<AlarmStep>(Heist);
            steps.Add(new AlarmStep { AtSeconds = 0, Kind = AlarmStepKind.Message, Group = 0 });
            steps.Add(new AlarmStep { AtSeconds = 10, Kind = AlarmStepKind.Mechanism, Action = MechanismActionKind.MoverReverse, Group = DoorBig, Value = 1 });
            steps.Add(new AlarmStep { AtSeconds = 10, Kind = AlarmStepKind.Mechanism, Action = MechanismActionKind.MoverPlay, Group = DoorBig });
            steps.Add(new AlarmStep { AtSeconds = 10, Kind = AlarmStepKind.Mechanism, Action = MechanismActionKind.MoverReverse, Group = DoorCrown, Value = -1 });
            steps.Add(new AlarmStep { AtSeconds = 10, Kind = AlarmStepKind.Mechanism, Action = MechanismActionKind.MoverPlay, Group = DoorCrown });
            steps.Add(new AlarmStep { AtSeconds = 30, Kind = AlarmStepKind.Message, Group = 1 });
            steps.Add(new AlarmStep { AtSeconds = 60, Kind = AlarmStepKind.Wave, Group = 1 });
            steps.Add(new AlarmStep { AtSeconds = 120, Kind = AlarmStepKind.Message, Group = 2 });
            steps.Add(new AlarmStep { AtSeconds = 360, Kind = AlarmStepKind.Cruiser });
            entities.Add(Heist);
            // the garrison cutter's loop round the town (a ship watcher), and the traffic
            Patrol = kit.Ship(catalog, frame, Anchor, "ship_nocturne_wraith", new float3(0, 30, -320), 0, alliance, "ai_default");
            ships.Add(Patrol);
            var civil = new Allegiance { Faction = CivilIndex, Team = 5 };
            for (int r = 0; r < Routes.Length; r++)
            {
                var route = Routes[r];
                float length = Length(r);
                for (int n = 0; n < route.Ships; n++)
                {
                    float along = length * (n + .3f) / route.Ships;
                    float2 p = Along(r, along), q = Along(r, along + 30);
                    var id = kit.Ship(catalog, frame, Anchor, n % 3 == 0 ? "ship_nocturne_wraith" : "ship_nocturne_vesper", new float3(p.x, route.Y, p.y),
                        math.atan2(q.x - p.x, q.y - p.y), civil, "ai_hollow_traffic");
                    if (id.IsNone) continue;
                    ships.Add(id); traffic.Add((id, r, along));
                }
            }
            foreach (var m in Moorings)
            {
                var root = kit.Grid(session, catalog, frame, Anchor, "ship_nocturne_vesper", m.At, quaternion.RotateY(math.radians(m.YawDeg)), GridKind.Station, GridMotion.Static);
                if (root != Entity.Null) { em.AddComponent<DamageImmune>(root); grids.Add(root); }
            }
        }

        void Lamps()
        {
            // a warm lamp in every gallery mouth, cold work lights in the Powerhouse and the Yards, sodium in Undertown
            for (int f = 0; f < 6; f++)
            {
                float y = MarketBottom + (f + .6f) * (CrownDeck - MarketBottom) / 6;
                for (int a = 0; a < 360; a += 45)
                {
                    var p = new float3(math.cos(math.radians(a)) * 120, y, math.sin(math.radians(a)) * 120);
                    lamps[2].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)p, new Color(1f, .78f, .45f), 90, 2600));
                }
            }
            foreach (var p in new[] { Poi("turbine_hall"), Poi("core_road"), Poi("engineers") })
                lamps[4].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)p, new Color(.8f, .9f, 1f), 120, 5200));
            lamps[4].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)Poi("yard_apron"), new Color(.85f, .95f, 1f), 200, 9000));
            lamps[3].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)Poi("pier"), new Color(1f, .9f, .7f), 220, 9000));
            foreach (var p in new[] { Poi("winch_square"), Poi("sump"), Poi("trunk") })
                lamps[5].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)p, new Color(1f, .62f, .3f), 110, 3800));
            lamps[1].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)Poi("long_wait"), new Color(1f, .8f, .5f), 120, 5000));
            lamps[1].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)Poi("garrison"), new Color(.9f, 1f, .95f), 120, 5000));
            lamps[6].Add(ExtractionKit.Lamp(dressing.transform, (Vector3)Poi("queue"), new Color(.4f, .7f, 1f), 260, 9000));
        }

        // ---------------------------------------------------------------------------------------------------------- per frame
        internal void Tick(double now)
        {
            if (!Built || !kit.Ready(out _, out var catalog, out var frame)) return;
            var em = kit.Ctx.EntityManager;
            if (dressing != null) dressing.transform.position = (Vector3)(float3)(Anchor - frame.OriginInStarSystem);
            // a free body: the core must fly in 3D (spawned grids are planar by default)
            if (Core != Entity.Null && em.Exists(Core) && em.HasComponent<PlanarConstraint>(Core)) em.RemoveComponent<PlanarConstraint>(Core);
            var core = Core != Entity.Null && em.Exists(Core) && em.HasComponent<StationCore>(Core) ? em.GetComponentData<StationCore>(Core) : default;
            if (coreLight != null && Core != Entity.Null && em.Exists(Core) && em.HasComponent<LocalToWorld>(Core))
            {
                coreLight.transform.position = em.GetComponentData<LocalToWorld>(Core).Position;
                coreLight.enabled = !em.HasComponent<Stowed>(Core);
                coreLight.intensity = em.HasComponent<Shrouded>(Core) ? 600 : 9000;
            }
            UpdateLamps(em);
            Signals(em, catalog, frame);
            Dimming(em, now);
            Machinery(em, frame, now);
            DepotCheck(em, catalog, frame, now);
            Scans(em);
            if (now - steeredAt > .25) { steeredAt = now; Steer(em, frame); }
        }

        void UpdateLamps(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<District>());
            using var all = q.ToComponentDataArray<District>(Allocator.Temp);
            foreach (var d in all)
                if (lamps.TryGetValue(d.Id, out var list))
                    foreach (var l in list) if (l != null) l.enabled = d.Lit;
        }

        void Signals(EntityManager em, Game.Data.Services.ContentCatalog catalog, in FrameOrigin frame)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<HeistSignals>());
            if (q.CalculateEntityCount() != 1) return;
            em.CompleteDependencyBeforeRW<HeistSignals>();
            var pending = q.GetSingleton<HeistSignals>().Pending;
            for (int i = 0; i < pending.Length; i++)
            {
                var s = pending[i];
                if (s.Place != Heist) continue;
                if (s.Index == 255)
                {
                    var h = em.GetComponentData<HeistState>(Heist);
                    Toast(s.Phase switch
                    {
                        HeistPhase.Alarm => h.Cause == AlarmCause.Identified ? "THE ALARM: the garrison knows someone is here. Doors lock in 10 s, the driver in 30 s, cutters in a minute."
                            : "THE LANTERN IS GONE. Blackout. Doors lock in 10 s, the driver in 30 s, cutters in a minute. They do not know who has it.",
                        HeistPhase.Lockdown => "Lockdown: the cruiser is here. Nothing leaves through the lanes now.",
                        HeistPhase.Over => s.Outcome switch
                        {
                            HeistOutcome.Escaped => "The core is out of Tollhouse Nine. Take it home through the gate.",
                            HeistOutcome.Caught => "Caught. The crew is down and the core stays in the Lantern.",
                            HeistOutcome.Installed => "The core is in the den's socket. The Rock has power.",
                            _ => "The heist is over.",
                        },
                        _ => "",
                    });
                    continue;
                }
                switch (s.Kind)
                {
                    case AlarmStepKind.Message when s.Group == 0: break;
                    case AlarmStepKind.Message when s.Group == 1: DriverLocked = true; Toast("0:30 the mass driver's safeties lock."); break;
                    case AlarmStepKind.Message when s.Group == 2: ScansOn = true; Toast("2:00 the lanes: outbound ships are stopped and scanned by hand."); break;
                    case AlarmStepKind.Wave: LaunchCutters(catalog, frame, Poi("well_mouth") + new float3(0, 60, 0)); Toast("1:00 three cutters launch from the Crown."); break;
                    case AlarmStepKind.Cruiser:
                        Cruiser = kit.Ship(catalog, frame, Anchor, "ship_nocturne_dirge", new float3(700, 20, 260), math.PI * 1.5f, new Allegiance { Faction = AllianceIndex, Team = 4 }, "ai_default");
                        ships.Add(Cruiser);
                        kit.Request(MechanismActionKind.PortalJam, ExtractionRuntime.GateGroup, 900);
                        Toast("6:00 the Alliance cruiser arrives at the gate. The lane gate is jammed.");
                        break;
                }
            }
        }

        internal void LaunchCutters(Game.Data.Services.ContentCatalog catalog, in FrameOrigin frame, float3 post)
        {
            if (cutters.Count > 0) { cuttersPost = post; return; }
            var alliance = new Allegiance { Faction = AllianceIndex, Team = 4 };
            for (int i = 0; i < 3; i++)
            {
                var id = kit.Ship(catalog, frame, Anchor, "ship_nocturne_wraith", Poi("garrison") + new float3(-60 - i * 30, 10, i * 30 - 30), -math.PI / 2, alliance, "ai_default");
                if (id.IsNone) continue;
                cutters.Add(id); ships.Add(id);
                if (kit.Root(id, out var root)) kit.Ctx.EntityManager.AddComponentData(root, new Watcher { Kind = WatcherKind.Ship, Faction = AllianceIndex, Range = 300, CosHalfArc = -1, Rate = .7f });
            }
            cuttersPost = post;
        }

        /// <summary>Lantern Night (the Dimming job): at 21:00 town time, a minute after the crane lifts the Grate's panel, every light in
        /// town goes out for sixty seconds. The town's clock is compressed: the night comes three minutes after the job is set.</summary>
        internal void ScheduleDimming(double now) { DimmingNight = true; dimmingAt = now + 180; Toast("Lantern Night: the Dimming at 21:00, in three minutes. The crane lifts the Grate's panel a minute before."); }

        void Dimming(EntityManager em, double now)
        {
            if (!DimmingNight || dimmingAt < 0) return;
            double t = now - dimmingAt;
            if (t > -60 && t < -59.5) { kit.Door(DoorGrate, true); Toast("The yard crane lifts the Grate's panel: the Lantern shines up the Well."); }
            bool dark = t >= 0 && t < 60;
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<District>());
            using var all = q.ToEntityArray(Allocator.Temp);
            foreach (var e in all)
            {
                var d = em.GetComponentData<District>(e);
                byte want = (byte)(dark ? 1 : 0);
                if (d.Dimmed == want) continue;
                d.Dimmed = want; em.SetComponentData(e, d);
            }
            if (t >= 0 && t < .5) Toast("21:00 THE DIMMING: one minute of dark.");
            if (t >= 60 && t < 60.5) { Toast("The lights come back."); kit.Door(DoorGrate, false); }
            if (t > 61) dimmingAt = -1;
        }

        /// <summary>The outfall flush (every minute, eight seconds of water down the trunk; the engineer can hold it) and the mass driver
        /// (a pod every thirty seconds out along the pod line, until the alarm's safeties lock it).</summary>
        void Machinery(EntityManager em, in FrameOrigin frame, double now)
        {
            double t = now - builtAt;
            bool flushing = !InsideMan && (t % FlushPeriod) < FlushSeconds;
            bool firing = !DriverLocked && (t % DriverPeriod) < Time.unscaledDeltaTime * 1.5f;
            if (!flushing && !firing) { Catch(em, frame); return; }
            float3 trunk = Sim(new float3(0, 0, 0)), breech = Sim(Poi("breech")), line = math.normalizesafe(Poi("pod_line") - Poi("breech"));
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsVelocity>(), ComponentType.ReadOnly<PhysicsMass>(), ComponentType.ReadOnly<LocalTransform>());
            using var bodies = q.ToEntityArray(Allocator.Temp);
            foreach (var b in bodies)
            {
                float mass = ExtractionKit.Mass(em, b);
                if (mass <= 0) continue;
                var p = em.GetComponentData<LocalTransform>(b).Position;
                if (flushing)
                {
                    float3 d = p - trunk;
                    if (math.length(d.xz) < TrunkRadius && d.y < PowerBottom && d.y > UnderBottom - 10)
                        kit.Impulse(b, new float3(0, -FlushAccel * mass * Time.deltaTime, 0), p, true);
                }
                if (firing && math.distance(p, breech) < 34 && !em.HasComponent<Game.Simulation.Commands.PlayerShip>(b))
                {
                    var v = em.GetComponentData<PhysicsVelocity>(b).Linear;
                    kit.Impulse(b, (line * DriverSpeed - v) * mass, p, true);
                    if (em.HasComponent<TowLoad>(b)) TowApi.Release(em, em.GetComponentData<TowLoad>(b).Tower);
                    Toast(b == Core ? "POD SEVEN: the driver throws the core down the pod line." : "The mass driver fires a pod.");
                }
            }
            Catch(em, frame);
        }

        /// <summary>The crewmate down the pod line with a tow rig (Pod Seven's stand-in): a core thrown down the line is caught and held
        /// still at PodCatch metres from the breech, for the crew to collect.</summary>
        void Catch(EntityManager em, in FrameOrigin frame)
        {
            if (Core == Entity.Null || !em.Exists(Core) || em.HasComponent<TowLoad>(Core)) return;
            var core = em.GetComponentData<StationCore>(Core);
            if (core.Freed == 0 || core.Socket != Entity.Null) return;
            float3 p = em.GetComponentData<LocalTransform>(Core).Position, breech = Sim(Poi("breech"));
            var v = em.GetComponentData<PhysicsVelocity>(Core).Linear;
            if (math.distance(p, breech) > PodCatch && math.length(v) > 5)
            {
                kit.Impulse(Core, -v * ExtractionKit.Mass(em, Core), p, true);
                Toast("Pod line: the catch rig has the core, 1.6 km out. Collect it.");
            }
        }

        /// <summary>The fuel depot: when a tank is broken, every cutter in town (and the fire tugs) goes east for two minutes.</summary>
        void DepotCheck(EntityManager em, Game.Data.Services.ContentCatalog catalog, in FrameOrigin frame, double now)
        {
            int broken = 0;
            foreach (var t in tanks) if (!em.Exists(t) || Game.Simulation.Extraction.CoreTetherSystem.SolidCells(em, t) < 600) broken++;
            if (broken > DepotBlown)
            {
                DepotBlown = broken;
                LaunchCutters(catalog, frame, Poi("depot") + new float3(0, 40, 0));
                cuttersHomeAt = now + 120;
                Toast("THE DEPOT IS BURNING. Every cutter and fire tug in town goes east.");
            }
            if (cuttersHomeAt > 0 && now > cuttersHomeAt) { cuttersHomeAt = -1; cuttersPost = Poi("well_mouth") + new float3(0, 60, 0); }
        }

        /// <summary>2:00 into the alarm, the lanes: a crew ship near the lane gate with its transponder off or a core on its cable is
        /// stopped and identified.</summary>
        void Scans(EntityManager em)
        {
            if (!ScansOn) return;
            var ship = kit.Ctx.PlayerShipRoot;
            if (ship == Entity.Null || !em.Exists(ship) || !em.HasComponent<Transponder>(ship)) return;
            float3 gate = Sim(new float3(ExtractionRuntime.LanternGateLocal.x, 0, ExtractionRuntime.LanternGateLocal.y));
            float3 at = em.GetComponentData<LocalTransform>(ship).Position;
            if (math.distance(at.xz, gate.xz) > 450) return;
            var t = em.GetComponentData<Transponder>(ship);
            var load = TowApi.Load(em, ship);
            bool core = load != Entity.Null && em.HasComponent<StationCore>(load);
            if ((t.On != 0 && !core) || TransponderApi.IsWantedBy(em, ship, AllianceIndex)) return;
            em.AddComponentData(ship, new Wanted { FactionMask = (em.HasComponent<Wanted>(ship) ? em.GetComponentData<Wanted>(ship).FactionMask : 0) | (1ul << AllianceIndex) });
            Toast(core ? "Scanned at the lane: a core on your cable. WANTED." : "Scanned at the lane: no transponder. WANTED.");
        }

        // ---------------------------------------------------------------------------------------------------------- traffic
        float Length(int r)
        {
            var p = Routes[r].Points; int n = p.Length; float total = 0;
            for (int k = 0; k < (Routes[r].Loop ? n : n - 1); k++) total += math.distance(p[k], p[(k + 1) % n]);
            return total;
        }
        float2 Along(int r, float d)
        {
            var route = Routes[r]; var p = route.Points; int n = p.Length, legs = route.Loop ? n : n - 1;
            float total = Length(r);
            if (route.Loop) d = total > 0 ? d - math.floor(d / total) * total : 0; else d = math.clamp(d, 0, total);
            for (int k = 0; k < legs; k++)
            {
                float2 a = p[k], b = p[(k + 1) % n]; float leg = math.distance(a, b);
                if (d <= leg) return leg > 0 ? math.lerp(a, b, d / leg) : a;
                d -= leg;
            }
            return p[n - 1];
        }

        void Steer(EntityManager em, in FrameOrigin frame)
        {
            for (int i = 0; i < traffic.Count; i++)
            {
                var (id, r, along) = traffic[i];
                if (!kit.Root(id, out var root) || !em.HasComponent<AiBrain>(root)) continue;
                var route = Routes[r];
                float3 pos = ExtractionKit.Local(Anchor, em.GetComponentData<LocalTransform>(root).Position, frame);
                // progress: the nearest route point ahead of the last one
                float best = along, bestSq = 90 * 90;
                for (float d = along - 20; d <= along + 260; d += 10) { float sq = math.distancesq(Along(r, d), pos.xz); if (sq < bestSq) { bestSq = sq; best = d; } }
                along = math.max(along, best);
                float total = Length(r);
                if (!route.Loop && along > total - 40)
                {   // the queue: back to the start, out of sight
                    along = 0; var s = Along(r, 0);
                    DevEcs.TeleportRoot(em, root, Sim(new float3(s.x, route.Y, s.y)), quaternion.RotateY(0));
                }
                traffic[i] = (id, r, along);
                var goal = Along(r, along + 70);
                var brain = em.GetComponentData<AiBrain>(root);
                if (brain.Initialized == 0) continue;
                brain.Anchor = Sim(new float3(goal.x, route.Y, goal.y)); brain.GoalPosition = brain.Anchor;
                em.SetComponentData(root, brain);
            }
            // the patrol cutter: round the town above the Crown; the alarm's cutters hold their post
            if (kit.Root(Patrol, out var patrol) && em.HasComponent<AiBrain>(patrol))
            {
                if (!em.HasComponent<Watcher>(patrol)) em.AddComponentData(patrol, new Watcher { Kind = WatcherKind.Ship, Faction = AllianceIndex, Range = 260, CosHalfArc = -1, Rate = .5f });
                float a = (float)(Time.timeAsDouble * .05 % (2 * math.PI));
                var brain = em.GetComponentData<AiBrain>(patrol);
                if (brain.Initialized != 0) { brain.Anchor = Sim(new float3(math.cos(a) * 330, 30, math.sin(a) * 330)); em.SetComponentData(patrol, brain); }
            }
            foreach (var c in cutters)
                if (kit.Root(c, out var root) && em.HasComponent<AiBrain>(root))
                {
                    var brain = em.GetComponentData<AiBrain>(root);
                    if (brain.Initialized != 0) { brain.Anchor = Sim(cuttersPost); em.SetComponentData(root, brain); }
                }
        }

        internal string Status(EntityManager em)
        {
            if (!Built) return "Tollhouse Nine: not built";
            var h = Heist != Entity.Null && em.Exists(Heist) ? em.GetComponentData<HeistState>(Heist) : default;
            var core = Core != Entity.Null && em.Exists(Core) && em.HasComponent<StationCore>(Core) ? em.GetComponentData<StationCore>(Core) : default;
            int dark = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<District>()))
            using (var all = q.ToComponentDataArray<District>(Allocator.Temp))
                foreach (var d in all) if (!d.Lit) dark++;
            return $"heist={h.Phase} outcome={h.Outcome} cause={h.Cause} tethers={core.Tethers}/6 freed={core.Freed} dark={dark}/6 driverLocked={DriverLocked} scans={ScansOn} " +
                   $"cutters={cutters.Count} depot={DepotBlown} dimming={(DimmingNight ? "night" : "no")} shroud={Shroud} inside={InsideMan} missing={kit.Missing.Count}";
        }

        internal void OpenHatch() => kit.Door(DoorHatch, true);
        internal void OpenBigDoor() => kit.Door(DoorBig, true);
        internal void OpenStaffDoor() => kit.Door(DoorStaff, true);

        /// <summary>The Trojan's stand-in: a crewmate's hauler books a freight berth with a real load and is moved to the yard apron "to
        /// load a hull section". It is a crew ship (work code), with a hold that takes a core.</summary>
        internal string BookHauler()
        {
            if (!Hauler.IsNone && kit.Root(Hauler, out _)) return "The hauler is already at the yard apron.";
            if (!kit.Ready(out _, out var catalog, out var frame)) return "not ready";
            Hauler = kit.Ship(catalog, frame, Anchor, "ship_nocturne_wraith", Poi("yard_apron") + new float3(40, 0, 60), math.PI, Allegiance.Player, "ai_hollow_traffic");
            if (Hauler.IsNone) return "no hauler could be spawned";
            ships.Add(Hauler);
            if (kit.Root(Hauler, out var root))
            {
                var em = kit.Ctx.EntityManager;
                em.AddComponentData(root, new CargoHold { CapacityT = 100000 });
                TransponderApi.Set(em, root, true, AccessCode.Work);
            }
            OpenStaffDoor();
            return "A freight berth is booked. The hauler waits at the yard apron with an empty hold; the dock lift into the turbine hall and the staff door are open for the shift change.";
        }

        internal Entity Post(int i) => i >= 0 && i < posts.Count ? posts[i] : Entity.Null;

        /// <summary>The tether nearest a point (sim frame) within `radius`, or Null.</summary>
        internal Entity NearestTether(EntityManager em, float3 at, float radius)
        {
            Entity best = Entity.Null; float bestD = radius;
            foreach (var e in entities)
            {
                if (!em.Exists(e) || !em.HasComponent<CoreTether>(e)) continue;
                var t = em.GetComponentData<CoreTether>(e);
                if (t.Cut != 0) continue;
                float d = math.distance(at, Sim(t.Anchor));
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        internal IEnumerable<Entity> TetherEntities() { foreach (var e in entities) if (kit.Ctx.EntityManager.Exists(e) && kit.Ctx.EntityManager.HasComponent<CoreTether>(e)) yield return e; }

        internal void Clear()
        {
            if (!Built) return;
            var em = kit.Ctx.EntityManager; em.CompleteAllTrackedJobs();
            foreach (var g in grids) if (em.Exists(g)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, g);
            foreach (var e in entities) if (em.Exists(e)) em.DestroyEntity(e);
            foreach (var id in ships) kit.Despawn(id);
            grids.Clear(); entities.Clear(); ships.Clear(); traffic.Clear(); posts.Clear(); tanks.Clear(); cutters.Clear(); lamps.Clear();
            if (dressing != null) UnityEngine.Object.Destroy(dressing);
            dressing = null; coreLight = null; Built = false; Core = Powerhouse = Heist = Entity.Null;
            DriverLocked = ScansOn = DimmingNight = InsideMan = Shroud = HaulerLeaving = false; DepotBlown = 0; dimmingAt = -1; cuttersHomeAt = -1;
        }

        public void Dispose() => Clear();
    }
}
#endif
