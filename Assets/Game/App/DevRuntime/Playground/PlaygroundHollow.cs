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
        readonly List<(PersistentId id, Route route, int next)> traffic = new();
        readonly List<(PersistentId id, float2 at)> guards = new();
        readonly List<string> missing = new();
        GameObject dressing;
        readonly List<Mesh> meshes = new();
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
                if (!fort) { em.AddComponent<SpatialHidden>(root); continue; }
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
                // each ship starts partway along its first leg, later ones on a shared start further on: two routes begin where the
                // player arrives, and a ship spawned on the player wrecks both
                var starts = new List<float2>();
                foreach (var route in Traffic)
                {
                    if (route.Points == null || route.Points.Length < 2) continue;
                    int earlier = 0; foreach (var s in starts) if (math.distancesq(s, route.Points[0]) < 1) earlier++;
                    starts.Add(route.Points[0]);
                    float2 leg = route.Points[1] - route.Points[0];
                    float along = math.min(.85f * math.length(leg), 160 + 140 * earlier);
                    float2 at = route.Points[0] + math.normalizesafe(leg) * along;
                    var id = Ship(spawner, catalog, clan, route.Blueprint, at, route.Points[1], "ai_hollow_traffic", frame);
                    if (!id.IsNone) traffic.Add((id, route, 1));
                }
                foreach (var g in Gunships)
                {
                    var id = Ship(spawner, catalog, clan, g.Blueprint, g.Position, g.Position + new float2(0, -1), "ai_hollow_guard", frame);
                    if (!id.IsNone) guards.Add((id, g.Position));
                }
            }
            if (catalog.TryGet<ShipBlueprintDefinition>(new ContentId("ship_device_canister"), out var canister))
                foreach (var st in Stations)
                    if (st.Kind == StationKind.Rearm) Canisters(em, canister, session, frame, st);
            BuildDressing(catalog);
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
            }
            if (!ctx.TryGetService<ShipSpawnService>(out var spawner)) return;
            for (int i = 0; i < traffic.Count; i++)
            {
                var (id, route, next) = traffic[i];
                if (!spawner.TryGetRoot(id, out var root) || !em.HasComponent<AiBrain>(root) || !em.HasComponent<LocalTransform>(root)) continue;
                var position = em.GetComponentData<LocalTransform>(root).Position;
                float3 goal = Sim(route.Points[next], frame);
                if (math.distancesq(position.xz, goal.xz) < 40 * 40)
                {
                    // a one-way ship starts again only while its first point is clear of the player
                    if (!route.Loop && next + 1 >= route.Points.Length && math.distancesq(at, route.Points[0]) < 120 * 120) { Steer(em, root, goal); continue; }
                    next++;
                    if (next >= route.Points.Length) next = route.Loop ? 0 : 1;
                    if (!route.Loop && next == 1) { Teleport(em, root, route, frame); }
                    traffic[i] = (id, route, next); goal = Sim(route.Points[next], frame);
                }
                Steer(em, root, goal);
            }
            foreach (var (id, post) in guards)
                if (spawner.TryGetRoot(id, out var root) && em.HasComponent<AiBrain>(root)) Anchor(em, root, Sim(post, frame));
        }

        /// <summary>A one-way route's ship, at its end, starts again from the first point (it left the Hollow, another arrives).</summary>
        void Teleport(EntityManager em, Entity root, Route route, FrameOrigin frame)
        {
            float2 d = route.Points[1] - route.Points[0];
            DevEcs.TeleportRoot(em, root, Sim(route.Points[0], frame), quaternion.RotateY(math.atan2(d.x, d.y)));
        }

        // Traffic flies on its own AI (owner: "should be using normal ai movement"): its patrol anchor and goal move along the route.
        static void Steer(EntityManager em, Entity root, float3 goal)
        {
            var brain = em.GetComponentData<AiBrain>(root);
            if (brain.Initialized == 0 || math.distancesq(brain.Anchor.xz, goal.xz) < 1) return;
            brain.Anchor = goal; brain.GoalPosition = goal;
            em.SetComponentData(root, brain);
        }
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
            var glow = new HoloMesh(); var solid = new HoloMesh();
            // a light's Radius is the reach of its light in the design, not its sprite: lamps are small, beacons and flashers larger
            foreach (var l in Lights)
                glow.Glow(new Vector3(l.Position.x, l.Position.y, l.Position.z), l.Kind == "beacon" ? 4.5f : l.Kind == "flash" ? 3.5f : 2.4f, l.Colour,
                    l.Kind == "beacon" ? 1 : l.Kind == "flash" ? 2 : 0, (l.Position.x + l.Position.z) * .001f);
            foreach (var n in Neons) solid.Beam(new Vector3(n.A.x, n.Y, n.A.y), new Vector3(n.B.x, n.Y, n.B.y), 1.2f, .6f, n.Colour);
            foreach (var b in Beacons) glow.Glow(new Vector3(b.Position.x, 6, b.Position.y), 5, b.Colour, 1, (b.Position.x + b.Position.y) * .001f);
            foreach (var s in Signs)
            {
                var right = new Vector3(Mathf.Cos(s.Yaw), 0, -Mathf.Sin(s.Yaw)); var forward = new Vector3(Mathf.Sin(s.Yaw), 0, Mathf.Cos(s.Yaw));
                // a sign's Size is its letter height in metres; Text takes one font pixel (letters are PixelFont.Height pixels tall)
                solid.Text(s.Text, new Vector3(s.Position.x, s.Position.y + .3f, s.Position.z), right, -forward, math.max(.4f, s.Size / PixelFont.Height), s.Colour);
            }
            foreach (var st in Stations)
            {
                var c = st.Kind == StationKind.Reset ? new Color(1f, .25f, .2f) : new Color(1f, .7f, .3f);
                var at = new Vector3(st.Position.x, -.7f, st.Position.y);
                solid.DotRing(at, StationRadius - 1, 40, 1.1f, c, 4); solid.DotRing(at, StationRadius - 4, 28, .6f, c * .55f);
                var right = new Vector3(Mathf.Cos(st.Yaw), 0, -Mathf.Sin(st.Yaw)); var forward = new Vector3(Mathf.Sin(st.Yaw), 0, Mathf.Cos(st.Yaw));
                solid.Text(st.Title, at + forward * (StationRadius + 6), right, -forward, .7f, c);
            }
            Add("hollow lights", glow, look.Glow, Color.white * 2.4f);
            Add("hollow neon and signs", solid, look.Solid, Color.white * 2f);
        }
        void Add(string name, HoloMesh b, Material material, Color tint)
        {
            if (b.Count == 0) return;
            var mesh = b.ToMesh(name); meshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(dressing.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", tint); r.SetPropertyBlock(block);
        }
        void DisposeDressing()
        {
            if (dressing != null) UnityEngine.Object.Destroy(dressing);
            foreach (var m in meshes) if (m != null) UnityEngine.Object.Destroy(m);
            meshes.Clear(); dressing = null;
        }

        public void Dispose() { DisposeDressing(); }
    }
}
#endif
