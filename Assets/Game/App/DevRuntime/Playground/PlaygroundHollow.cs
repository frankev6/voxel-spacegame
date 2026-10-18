#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Presentation.Presenters;
using Game.App.Session;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Economy;
using Game.Galaxy.Components;
using Game.Presentation.Racing;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Simulation.Spatial;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Game.App.DevRuntime.Playground.HollowLayout;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-023 option E, the Hollow, in the playground (owner, 2026-10-18: "Lets build this latest version into the game"):
    /// the asteroid and its structures as static station grids, the four outlying forts turning on their rocks with their guns, the
    /// Maw's batteries and the edge guns as Clan turrets, moored ships, traffic on normal AI flight through the tunnels, the Clan's
    /// gunships off the Maw, and its stations (the Market and the dens trade, the Docks rearm, the Quartermaster resets). While the
    /// player is under the rock, the rock above the flight plane is hidden (the cutaway). Built the first time a pilot goes there.</summary>
    internal sealed class PlaygroundHollow : IDisposable
    {
        const ushort Group = 60;
        readonly DevContext ctx;
        readonly double3 origin;           // the Hollow's origin in the star system
        readonly List<Entity> grids = new(), high = new();
        readonly List<PersistentId> ships = new();
        readonly List<Flight> traffic = new();
        /// <summary>A traffic ship on its route: metres flown along it, and the progress last checked (for a ship stuck on rock).</summary>
        sealed class Flight { internal PersistentId Id; internal Route Route; internal float Progress, Checked; internal double CheckedAt; internal float2 Written; internal bool Steered; }
        readonly List<(PersistentId id, float2 at)> guards = new();
        readonly List<string> missing = new();
        GameObject dressing, roof;   // roof: the dressing above the rock's +24 m split, hidden with the high rock while the player is inside
        readonly List<Mesh> meshes = new();
        readonly HollowAtmosphere air = new();
        readonly HollowHolograms holograms = new();
        Texture2D shelter;   // the roofed area (InsideRows) for the brick shader's shelter: no sun, dim ambient, haze (BrickSurface.hlsl)
        static readonly int ShelterMask = Shader.PropertyToID("_ShelterMask"), ShelterRect = Shader.PropertyToID("_ShelterRect"),
            ShelterParams = Shader.PropertyToID("_ShelterParams"), ShelterHaze = Shader.PropertyToID("_ShelterHaze"), ShelterFill = Shader.PropertyToID("_ShelterFill");
        bool built, hidden, tradeGranted;

        internal PlaygroundHollow(DevContext ctx, double3 playgroundOrigin)
        { this.ctx = ctx; origin = playgroundOrigin + new double3(Offset.x, 0, Offset.y); }

        internal bool Built => built;
        internal double3 Origin => origin;
        internal string Missing => missing.Count == 0 ? null : "Hollow content missing (" + string.Join(", ", missing) + "): run Game/Content/Build Hollow.";
        internal int GridCount => grids.Count;
        internal bool CutawayShown => hidden;

        bool Ready(out GameSession session, out ContentCatalog catalog, out FrameOrigin frame)
        {
            session = null; catalog = null; frame = default;
            if (!ctx.HasGameplay || !ctx.TryGetService(out session) || !ctx.TryGetService<ContentService>(out var content) || !ctx.TryGetFrame(out frame)) return false;
            catalog = content.Catalog; return true;
        }
        float3 Sim(float2 p, FrameOrigin frame) { var v = (float3)(origin + new double3(p.x, 0, p.y) - frame.OriginInStarSystem); v.y = 0; return v; }
        double3 Universe(float2 p) => origin + new double3(p.x, 0, p.y);

        Allegiance Clan(ContentCatalog catalog)
        {
            byte index = 255;
            if (catalog.TryGet<FactionDefinition>(new ContentId(Faction), out var faction) && ctx.TryGetService<Game.Galaxy.Services.FactionService>(out var factions))
                index = factions.Indices.IndexOf(faction);
            return new Allegiance { Faction = index, Team = 3 };
        }

        Entity Grid(ContentCatalog catalog, GameSession session, FrameOrigin frame, string id, float2 at, float yaw, GridKind kind, GridMotion motion,
            GridProfile profile = GridProfile.Dummy)
        {
            if (!catalog.TryGet<ShipBlueprintDefinition>(new ContentId(id), out var blueprint)) { if (!missing.Contains(id)) missing.Add(id); return Entity.Null; }
            var em = ctx.EntityManager;
            var root = GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new GridSpawnParams
            {
                PersistentId = session.Allocate(), Pose = new RigidTransform(quaternion.RotateY(yaw), Sim(at, frame)),
                Kind = kind, Motion = motion, Profile = profile, Flags = GridSpawnFlags.Transient
            });
            em.AddComponentData(root, new UniversePosition { Value = Universe(at) });
            grids.Add(root);
            return root;
        }

        /// <summary>Spawns the whole Hollow. Safe to call again after <see cref="Clear"/> (the Quartermaster's reset).</summary>
        internal void Build()
        {
            if (built || !Ready(out var session, out var catalog, out var frame)) return;
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            missing.Clear(); built = true; hidden = false;
            var clan = Clan(catalog);
            ushort turret = AiSetup.ProfileIndexOf(catalog, new ContentId("ai_turret"));
            uint tick = (uint)ctx.Tick + 2;
            foreach (var piece in Pieces)
            {
                bool fort = piece.Layer == Layer.Fort && piece.Fort >= 0 && piece.Fort < Forts.Length;
                // armed grids (the forts, the guns) take the Station profile so their weapons and power come up
                var root = Grid(catalog, session, frame, piece.Id, piece.Origin, 0, GridKind.Station, fort ? GridMotion.Kinematic : GridMotion.Static,
                    fort ? GridProfile.Station : GridProfile.Dummy);
                if (root == Entity.Null) continue;
                em.AddComponent<CameraSeeThrough>(root);
                if (piece.Layer == Layer.High) high.Add(root);
                // the rock and the districts' structures are scenery: shots stop on them but break nothing (a tile is several lumps, and
                // the first stray hit split them off as debris: owner, "a bunch of their structures were just braking off on their own")
                if (!fort) { em.AddComponent<SpatialScenery>(root); em.AddComponent<Game.Voxel.Damage.DamageImmune>(root); continue; }
                // the rock turns about its centre, its fort and guns with it
                HazardSpawn.AddMover(em, root, new Mover { Path = MoverPath.Rotate, Origin = Sim(piece.Origin, frame), Rate = Forts[piece.Fort].Rate,
                    Yaw = 0, StartTick = tick, Playing = 1, Group = Group });
                em.AddComponentData(root, clan);
                em.AddComponentData(root, new AiBrain { ProfileIndex = turret });
            }
            foreach (var gun in Guns)
            {
                var root = Grid(catalog, session, frame, gun.Big ? "ship_hollow_gun_heavy" : "ship_hollow_gun_light", gun.Position, gun.Yaw, GridKind.Station, GridMotion.Static, GridProfile.Station);
                if (root == Entity.Null) continue;
                em.AddComponentData(root, clan);
                em.AddComponentData(root, new AiBrain { ProfileIndex = turret });
            }
            foreach (var m in Moorings) Grid(catalog, session, frame, m.Blueprint, m.Position, m.Yaw, GridKind.Station, GridMotion.Static);
            foreach (var b in Boulders) { var rock = Grid(catalog, session, frame, "ship_hollow_boulder_" + b.Size, b.Position, b.Yaw, GridKind.Asteroid, GridMotion.Static); if (rock != Entity.Null) em.AddComponent<CameraSeeThrough>(rock); }
            if (ctx.TryGetService<ShipSpawnService>(out var spawner))
            {
                // the gunships first; then each traffic ship some way along its route (by distance along the whole route: a first leg
                // can be shorter than a ship), clear of the player's arrival point, the moored ships and every ship placed before it
                var taken = new List<(float2 at, float clear)>();
                foreach (var g in Gunships)
                {
                    var id = Ship(spawner, catalog, clan, g.Blueprint, g.Position, g.Position + new float2(0, -1), "ai_hollow_guard", frame);
                    if (!id.IsNone) guards.Add((id, g.Position));
                    taken.Add((g.Position, 80));
                }
                taken.Add((Spawn, 140)); taken.Add((Entrance, 140));
                foreach (var m in Moorings) taken.Add((m.Position, 60));
                foreach (var route in Traffic)
                {
                    if (route.Points == null || route.Points.Length < 2) continue;
                    float length = Length(route), along = 120;
                    for (; along < length; along += 20)
                    {
                        float2 p0 = Along(route, along);
                        bool clear = true; foreach (var (p, r) in taken) if (math.distancesq(p, p0) < r * r) { clear = false; break; }
                        if (clear) break;
                    }
                    along = math.min(along, length);
                    float2 at = Along(route, along);
                    taken.Add((at, 80));
                    var id = Ship(spawner, catalog, clan, route.Blueprint, at, Along(route, along + 40), "ai_hollow_traffic", frame);
                    if (!id.IsNone) traffic.Add(new Flight { Id = id, Route = route, Progress = along, Checked = along, CheckedAt = Time.timeAsDouble });
                }
            }
            if (catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_device_canister"), out var canister))
                foreach (var st in Stations)
                    if (st.Kind == StationKind.Rearm) Canisters(em, canister, session, frame, st);
            // the race portal (owner: "inside the hollow there should be a portal to the race"): out by Track 01, where the race opens
            if (catalog.TryGet<ZoneDefinition>(new ContentId("zone_gate"), out var gate))
                HazardSpawn.Portal(em, gate, Sim(RacePortal, frame).xz, RacePortalYaw, Sim(RaceExit, frame).xz, math.PI, RacePortalWidth, 0, Group,
                    session.Allocate(), session.Allocate());
            else missing.Add("zone_gate");
            BuildDressing(catalog);
        }

        /// <summary>The point `distance` metres along a route from its first point (a loop closes back to it and wraps; a one-way
        /// route stops at its last point).</summary>
        static float2 Along(Route route, float distance)
        {
            var p = route.Points; int n = p.Length, legs = route.Loop ? n : n - 1;
            if (route.Loop) { float total = Length(route); distance = total > 0 ? distance - math.floor(distance / total) * total : 0; }
            distance = math.max(0, distance);
            for (int k = 0; k < legs; k++)
            {
                float2 a = p[k], b = p[(k + 1) % n]; float leg = math.distance(a, b);
                if (distance <= leg) return leg > 0 ? math.lerp(a, b, distance / leg) : a;
                distance -= leg;
            }
            return p[n - 1];
        }
        static float Length(Route route)
        {
            var p = route.Points; int n = p.Length; float total = 0;
            for (int k = 0; k < (route.Loop ? n : n - 1); k++) total += math.distance(p[k], p[(k + 1) % n]);
            return total;
        }
        /// <summary>A ship's progress along its route: the nearest route point from 20 m behind to 300 m ahead of where it was, if the ship
        /// is within 80 m of it (a ship pushed off its route keeps its progress rather than jumping to some other stretch).</summary>
        static float Progress(Route route, float2 position, float progress)
        {
            float best = progress, bestSq = 80 * 80;
            for (float d = progress - 20; d <= progress + 300; d += 10)
            {
                float sq = math.distancesq(Along(route, d), position);
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return math.max(progress, best);
        }

        PersistentId Ship(ShipSpawnService spawner, ContentCatalog catalog, Allegiance clan, string blueprint, float2 at, float2 toward, string profile, FrameOrigin frame)
        {
            if (!catalog.TryGet<ShipBlueprintDefinition>(new ContentId(blueprint), out _)) { if (!missing.Contains(blueprint)) missing.Add(blueprint); return default; }
            float2 d = toward - at; float yaw = math.atan2(d.x, d.y);
            var id = spawner.Spawn(new ShipSpawnArgs { Blueprint = new ContentId(blueprint), Position = Sim(at, frame), Rotation = quaternion.RotateY(yaw),
                Allegiance = clan, WithAi = true, AiProfile = new ContentId(profile) });
            if (!id.IsNone) ships.Add(id);
            return id;
        }

        void Canisters(EntityManager em, ShipBlueprintDefinition canister, GameSession session, FrameOrigin frame, Station st)
        {
            var right = new float2(math.cos(st.Yaw), -math.sin(st.Yaw)); var forward = new float2(math.sin(st.Yaw), math.cos(st.Yaw));
            for (int i = 0; i < 4; i++)
                HazardSpawn.Canister(em, canister, Sim(st.Position + forward * 40 + right * (i - 1.5f) * 22, frame).xz, 0, Group, session.Allocate());
        }

        /// <summary>Removes everything the Hollow spawned.</summary>
        internal void Clear()
        {
            if (!built) return;
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            foreach (var e in grids) if (em.Exists(e)) GridSpawnApi.RequestDespawn(em, e);
            if (ctx.TryGetService<ShipSpawnService>(out var spawner)) foreach (var id in ships) spawner.Despawn(id);
            HazardSpawn.DespawnGroup(em, Group);
            grids.Clear(); high.Clear(); ships.Clear(); traffic.Clear(); guards.Clear();
            DisposeDressing(); built = false; hidden = false;
        }

        /// <summary>Per frame while the playground runs: dressing follows the floating origin, the cutaway follows the player, traffic
        /// steers to its next waypoint and the guards hold their stations.</summary>
        internal void Tick(bool located, double3 player)
        {
            if (!built || !ctx.TryGetFrame(out var frame)) return;
            if (dressing != null) dressing.transform.position = (float3)(origin - frame.OriginInStarSystem);
            if (shelter != null)
            {
                float3 min = Sim(new float2(InsideX0, InsideZ0), frame);
                Shader.SetGlobalVector(ShelterRect, new Vector4(min.x, min.z, 1 / (InsideW * InsideCell), 1 / (InsideH * InsideCell)));
            }
            var em = ctx.EntityManager;
            float2 at = located ? (float2)(player.xz - origin.xz) : new float2(float.MaxValue, float.MaxValue);
            bool inside = located && Inside(at);
            if (inside != hidden)
            {
                hidden = inside;
                foreach (var e in high)
                {
                    if (!em.Exists(e)) continue;
                    if (inside) { if (!em.HasComponent<GridRenderHidden>(e)) em.AddComponent<GridRenderHidden>(e); }
                    else if (em.HasComponent<GridRenderHidden>(e)) em.RemoveComponent<GridRenderHidden>(e);
                }
                if (roof != null) roof.SetActive(!inside);
            }
            using (AirMarker.Auto()) { air.Tick(Time.time, hidden, new Vector3(at.x, 0, at.y)); holograms.Tick(Time.time, Camera.main != null ? Camera.main.transform.forward : Vector3.forward); }
            if (!ctx.TryGetService<ShipSpawnService>(out var spawner)) return;
            // the AI steers to a goal 60 m ahead, so four updates a second are plenty; each reads and writes ECS components on the main
            // thread, which waits on the jobs that write them (about 1 ms a frame when it ran every frame)
            double now = Time.timeAsDouble;
            if (now - steeredAt < .25) return;
            steeredAt = now;
            using var timing = TrafficMarker.Auto();
            // each ship aims well ahead of itself along its route (pure pursuit), so it never arrives and orbits mid-route; a one-way
            // ship at its end starts again from the first point once that is clear of the player and the other traffic
            foreach (var f in traffic)
            {
                if (!spawner.TryGetRoot(f.Id, out var root) || !em.HasComponent<AiBrain>(root) || !em.HasComponent<LocalTransform>(root)) continue;
                var route = f.Route;
                float2 position = em.GetComponentData<LocalTransform>(root).Position.xz - Sim(float2.zero, frame).xz;
                f.Progress = Progress(route, position, f.Progress);
                float total = Length(route);
                if (route.Loop && f.Progress > total) { f.Progress -= total; f.Checked -= total; }
                else if (!route.Loop && f.Progress > total - 60 && StartClear(em, spawner, frame, route.Points[0], at, f.Id))
                {
                    Teleport(em, root, route, 0, frame); f.Progress = 0; f.Checked = 0; f.CheckedAt = now; f.Steered = false;
                }
                if (f.Progress - f.Checked > 10) { f.Checked = f.Progress; f.CheckedAt = now; }
                else if (now - f.CheckedAt > 8 && math.distancesq(position, at) > 250 * 250)
                {
                    // stuck on rock (pushed off its route): back onto the route a little ahead, out of the player's sight
                    f.Progress += 40; Teleport(em, root, route, f.Progress, frame); f.Checked = f.Progress; f.CheckedAt = now; f.Steered = false;
                }
                Steer(em, root, f, Along(route, f.Progress + LookAhead), frame);
            }
            foreach (var (id, post) in guards)
                if (spawner.TryGetRoot(id, out var root) && em.HasComponent<AiBrain>(root)) Anchor(em, root, Sim(post, frame));
        }

        /// <summary>Whether a one-way ship may start again at `start`: the player (at `player`) is 140 m away and no other traffic ship within 80 m.</summary>
        bool StartClear(EntityManager em, ShipSpawnService spawner, FrameOrigin frame, float2 start, float2 player, PersistentId self)
        {
            if (math.distancesq(player, start) < 140 * 140) return false;
            float3 at = Sim(start, frame);
            foreach (var f in traffic)
                if (!f.Id.Equals(self) && spawner.TryGetRoot(f.Id, out var root) && em.HasComponent<LocalTransform>(root)
                    && math.distancesq(em.GetComponentData<LocalTransform>(root).Position.xz, at.xz) < 80 * 80) return false;
            return true;
        }

        static readonly Unity.Profiling.ProfilerMarker AirMarker = new("Hollow.Air"), TrafficMarker = new("Hollow.Traffic");
        /// <summary>The race portal's exit, Hollow-relative: north of Track 01, clear of it (the track is laid when the race opens).</summary>
        internal static float2 RaceExit => (float2)DevPlaygroundRace.TrackOffset.xz + new float2(0, 600) - Offset;
        /// <summary>A pilot this close to the track after the race portal arrives there: the race opens.</summary>
        internal const float RaceArrivalRadius = 1500;
        double steeredAt;
        const float LookAhead = 60;   // far enough that a ship never arrives, near enough not to cut a tunnel's bends into the rock

        /// <summary>Puts a ship on its route `along` metres from the start, facing along it: a one-way ship at its end starts again
        /// (it left the Hollow, another arrives), a stuck one goes back onto its route.</summary>
        void Teleport(EntityManager em, Entity root, Route route, float along, FrameOrigin frame)
        {
            float2 at = Along(route, along), d = Along(route, along + 40) - at;
            DevEcs.TeleportRoot(em, root, Sim(at, frame), quaternion.RotateY(math.atan2(d.x, d.y)));
        }

        // Traffic flies on its own AI (owner: "should be using normal ai movement"): its patrol anchor and goal move along the route.
        // The AI dodges a ship in its way by shifting its goal aside (AiThinkJob.Avoid); writing the route's goal over it every update
        // threw the dodge away (playtest: a cruiser rammed the player instead of avoiding it), so the shift is carried onto the new
        // goal, halving each update once nothing is in the way any more.
        void Steer(EntityManager em, Entity root, Flight f, float2 goal, FrameOrigin frame)
        {
            var brain = em.GetComponentData<AiBrain>(root);
            if (brain.Initialized == 0) return;
            float2 shift = f.Steered ? brain.GoalPosition.xz - Sim(f.Written, frame).xz : float2.zero;
            if (math.lengthsq(shift) > MaxDodge * MaxDodge) shift = math.normalize(shift) * MaxDodge;
            f.Written = goal; f.Steered = true;
            brain.Anchor = Sim(goal, frame); brain.GoalPosition = Sim(goal + shift * .5f, frame);
            em.SetComponentData(root, brain);
        }
        const float MaxDodge = 120;
        static void Anchor(EntityManager em, Entity root, float3 post)
        {
            var brain = em.GetComponentData<AiBrain>(root);
            if (brain.Initialized == 0 || math.distancesq(brain.Anchor.xz, post.xz) < 1) return;
            brain.Anchor = post; em.SetComponentData(root, brain);
        }

        // ------------------------------------------------------------------------------------------------ stations
        /// <summary>The station within reach of `at` (Hollow-relative), or −1.</summary>
        internal int Nearest(float2 at)
        {
            int found = -1; float best = StationRadius * StationRadius;
            for (int i = 0; i < Stations.Length; i++)
            {
                float d = math.distancesq(at, Stations[i].Position);
                if (d <= best) { best = d; found = i; }
            }
            return found;
        }
        internal float2 Relative(double3 p) => (float2)(p.xz - origin.xz);

        internal DevCommandResult Activate(int index)
        {
            var st = Stations[index];
            switch (st.Kind)
            {
                case StationKind.Reset:
                    Clear(); Build();
                    return Missing != null ? DevCommandResult.Fail(Missing) : DevCommandResult.Ok("The Quartermaster sets the Hollow back as it was.");
                case StationKind.Rearm:
                    if (ctx.TryGetService<ContentService>(out var content) && content.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_device_canister"), out var canister)
                        && Ready(out var session, out _, out var frame))
                    {
                        Canisters(ctx.EntityManager, canister, session, frame, st);
                        return DevCommandResult.Ok("The Docks put out fresh canisters ahead: fly through them to refill.");
                    }
                    return DevCommandResult.Fail("Canister content missing: run Game/Content/Build Hazard Content.");
                default:
                    if (ctx.TryGetService<EconomyService>(out var economy) && ctx.TryGetService<ContentService>(out var c)
                        && ctx.TryGetService<MerchantPresenter>(out var merchant)
                        && c.Catalog.TryGet<MerchantDefinition>(new ContentId(string.IsNullOrEmpty(st.Merchant) ? "merchant_shady_trader" : st.Merchant), out var def))
                    {
                        if (!tradeGranted) { economy.Wallet.Add(2000); tradeGranted = true; }
                        merchant.Show(economy.Merchants.OpenSession(def, 0, false, null), null);
                        return DevCommandResult.Ok(st.Title + ": no questions asked.");
                    }
                    return DevCommandResult.Fail("Merchant services unavailable");
            }
        }

        // ------------------------------------------------------------------------------------------------ dressing
        void BuildDressing(ContentCatalog catalog)
        {
            if (!catalog.TryGet<TrackDefinition>(new ContentId("track_01"), out var track) || track.Look.Solid == null || track.Look.Glow == null) return;
            var look = track.Look;
            dressing = new GameObject("Hollow dressing");
            roof = new GameObject("Hollow roof dressing"); roof.transform.SetParent(dressing.transform, false);
            // the dressing above the roof (lamps and beacons on the outer rock) would draw between the camera and the tunnels while the
            // player is inside (owner: "lights from the outside of the asteroid is showing in front of the camera"): its own meshes
            var glow = new HoloMesh(); var solid = new HoloMesh(); var glowRoof = new HoloMesh(); var solidRoof = new HoloMesh();
            // a light's Radius is the reach of its light in the design, not its sprite: lamps are small, beacons and flashers larger
            // only beacons and flashers get a sprite: a lamp shows as its light pool and soft halo (owner, of the lamp sprites: "wtf is
            // all the dots")
            foreach (var l in Lights)
                if (l.Kind != "lamp") (l.Position.y > RoofY ? glowRoof : glow).Glow(new Vector3(l.Position.x, l.Position.y, l.Position.z), l.Kind == "beacon" ? 4.5f : l.Kind == "flash" ? 3.5f : 2.4f, l.Kind == "lamp" ? LedWhite : l.Colour,
                    l.Kind == "beacon" ? 1 : l.Kind == "flash" ? 2 : 0, (l.Position.x + l.Position.z) * .001f);
            holograms.Plan();   // the wall strips along the routes are drawn as wayfinding guides, in their stations' colours
            for (int i = 0; i < Neons.Length; i++)
            {
                if (holograms.Guided(i)) continue;
                var n = Neons[i];
                (n.Y > RoofY ? solidRoof : solid).Beam(new Vector3(n.A.x, n.Y, n.A.y), new Vector3(n.B.x, n.Y, n.B.y), 1.2f, .6f, n.Colour);
            }
            foreach (var b in Beacons) glow.Glow(new Vector3(b.Position.x, 6, b.Position.y), 5, b.Colour, 1, (b.Position.x + b.Position.y) * .001f);
            // the flight camera turns with the ship, so a sign reads upright to a ship heading along its Yaw (its approach): text runs
            // to the ship's right, its lines toward the ship (owner: the text was floating, one read mirrored)
            // the floor names are now holo boards (HollowHolograms); only THE MAW keeps its floor letters at the entrance
            foreach (var s in Signs)
            {
                if (s.Text != "THE MAW") continue;
                var (right, toward) = Facing(s.Yaw);
                // a sign's Size is its letter height in metres; Text takes one font pixel (letters are PixelFont.Height pixels tall)
                (s.Position.y > RoofY ? solidRoof : solid).Text(s.Text, new Vector3(s.Position.x, s.Position.y + .3f, s.Position.z), right, toward,
                    math.max(.4f, s.Size / PixelFont.Height), s.Colour);
            }
            // a station's circle is its floor projector (HollowHolograms): no dot rings at the flight plane over it, which read as loose
            // dots against the floor (owner: "wtf is all the dots"); a station no sign names gets a holo board there, not letters
            Add(dressing, "hollow lights", glow, look.Glow, Color.white * 2.4f);
            Add(roof, "hollow roof lights", glowRoof, look.Glow, Color.white * 2.4f);
            // real lamps: under the roof they are the only light (owner: "inside the asteroid the source of light seems to still be the sun")
            foreach (var l in Lights)
            {
                var lamp = new GameObject("lamp").AddComponent<UnityEngine.Light>(); lamp.transform.SetParent((l.Position.y > RoofY ? roof : dressing).transform, false);
                lamp.transform.localPosition = new Vector3(l.Position.x, l.Position.y, l.Position.z);
                // plain lamps are the station's cool LED white; beacons and flashers keep their signal colours
                lamp.type = LightType.Point; lamp.shadows = LightShadows.None;
                lamp.color = l.Kind == "lamp" ? HollowHolograms.LampColour(new float2(l.Position.x, l.Position.z), LedWhite) : l.Colour;
                // lamps hang 30-40 m over the tunnel floor: the reach must get there (inverse-square falloff, bright to half the reach)
                lamp.range = math.max(8, l.Radius) * 2.6f;
                lamp.intensity = (l.Kind == "beacon" ? .5f : l.Kind == "flash" ? .6f : .45f) * lamp.range * lamp.range;
                air.AddLamp(lamp, l.Kind, (l.Position.x + l.Position.z) * .01f);
            }
            // the race portal's two posts, lit amber
            foreach (float side in new[] { -1f, 1f })
            {
                var post = new GameObject("race portal lamp").AddComponent<UnityEngine.Light>(); post.transform.SetParent(dressing.transform, false);
                var across = new float2(math.cos(RacePortalYaw), -math.sin(RacePortalYaw)) * side * (RacePortalWidth * .5f + 3);
                post.transform.localPosition = new Vector3(RacePortal.x + across.x, 6, RacePortal.y + across.y);
                post.type = LightType.Point; post.shadows = LightShadows.None; post.color = new Color(1f, .66f, .16f);
                post.range = 70; post.intensity = .5f * post.range * post.range;
                air.AddLamp(post, "beacon", side);
            }
            air.Build(dressing.transform, roof.transform);
            holograms.Build(dressing.transform, look, FloorY);
            BuildShelter();
            Add(dressing, "hollow neon and signs", solid, look.Solid, Color.white * 2f);
            Add(roof, "hollow roof neon and signs", solidRoof, look.Solid, Color.white * 2f);
        }
        /// <summary>For a ship heading `yaw` (0 north, +x east): its right, and the way back toward it ("down" for text read upright).</summary>
        static (Vector3 right, Vector3 toward) Facing(float yaw) =>
            (new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw)), new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw)));
        const float RoofY = 24, FloorY = -24;   // the rock's split (the high pieces above it hide inside); the tunnels' floor
        static readonly Color LedWhite = new(.86f, .93f, 1f);   // the rock's split: the high pieces above it hide while the player is inside
        void Add(GameObject parent, string name, HoloMesh b, Material material, Color tint)
        {
            if (b.Count == 0) return;
            var mesh = b.ToMesh(name); meshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", tint); r.SetPropertyBlock(block);
        }
        void BuildShelter()
        {
            shelter = new Texture2D(InsideW, InsideH, TextureFormat.R8, false, true) { name = "Hollow shelter", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[InsideW * InsideH];
            for (int r = 0; r < InsideH && r < InsideRows.Length; r++)
                for (int c = 0; c < InsideW && c < InsideRows[r].Length; c++) pixels[r * InsideW + c] = InsideRows[r][c] == '1' ? new Color32(255, 0, 0, 0) : default;
            shelter.SetPixels32(pixels); shelter.Apply(false, true);
            Shader.SetGlobalTexture(ShelterMask, shelter);
            // under the roof a future station, lit, not a cave (owner: "atmosphere so it doesn't feel dark and empty", "the station inside
            // needs to be better lit, its in the future and this feels like an ugly dark cave"): no sun, but the sky's ambient kept and a
            // bright cool fill from the station's own lighting, and a faint pale haze for depth
            // the shelter reaches 8 m over the split, so the cut tops of the low rock (at +24) are roofed too, not left in the sun
            Shader.SetGlobalVector(ShelterParams, new Vector4(RoofY + 8, 1, 1, .16f));
            Shader.SetGlobalVector(ShelterHaze, new Vector4(.32f, .36f, .42f, 0));       // linear: lit air
            Shader.SetGlobalVector(ShelterFill, new Vector4(.22f, .24f, .27f, 0));
        }
        void DisposeDressing()
        {
            air.Dispose(); holograms.Dispose();
            Shader.SetGlobalVector(ShelterParams, Vector4.zero);
            if (shelter != null) UnityEngine.Object.Destroy(shelter);
            shelter = null;
            if (dressing != null) UnityEngine.Object.Destroy(dressing);
            foreach (var m in meshes) if (m != null) UnityEngine.Object.Destroy(m);
            meshes.Clear(); dressing = null; roof = null;
        }

        public void Dispose() { DisposeDressing(); }
    }
}
#endif
