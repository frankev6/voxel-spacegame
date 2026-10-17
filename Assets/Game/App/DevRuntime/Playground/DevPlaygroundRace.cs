#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Galaxy.Racing;
using Game.Simulation.Commands;
using Game.Simulation.Hazards;
using Game.UI.Data;
using Game.UI.Services;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>SPEC-92 §5.1 lobby, countdown and results in the playground (RACE-004/005/007, first playable). Pad 18 RACE (or `race
    /// open`) spawns Track 01 south of the pads and puts every pilot on the start grid, held; F (or `race start`) runs the countdown;
    /// `race end` despawns the track. Draws the field walls, gates and boost strips, and a race HUD: countdown, lap, place, times,
    /// wrong way and the results board.</summary>
    internal sealed class DevPlaygroundRace : IDisposable
    {
        internal const ushort Group = 18;
        internal static readonly double3 TrackOffset = new double3(0, 0, -3300);   // the track's north straight ~2.2 km south of the pads
        readonly DevRuntimeHost host;
        UIRoot ui;
        Entity race = Entity.Null;
        GameObject visuals; Material material;
        readonly List<LineRenderer> gateLines = new();
        readonly List<LineRenderer> mineRings = new(); GameObject mineVisuals;
        double3 trackOrigin;
        VisualElement hud; Label banner, status, warning, results;
        readonly Dictionary<byte, ContentHash> blueprints = new();   // each racer's ship, for respawns

        internal DevPlaygroundRace(DevRuntimeHost host) { this.host = host; }
        int syncFrame; RacePhase sentPhase = (RacePhase)255;
        Game.Core.Net.SimRoleKind Role => host.Context.TryGetService<Game.Core.Net.ICoopSession>(out var coop) ? coop.Role : Game.Core.Net.SimRoleKind.Solo;
        CoopSession Coop => host.Context.TryGetService<Game.Core.Net.ICoopSession>(out var coop) ? coop as CoopSession : null;
        /// <summary>This machine's pilot slot (0 for solo and the host).</summary>
        byte LocalSlot
        {
            get
            {
                var em = host.Context.EntityManager; var ship = host.Context.PlayerShipRoot;
                return ship != Entity.Null && em.HasComponent<Pilot>(ship) ? em.GetComponentData<Pilot>(ship).Slot : (byte)0;
            }
        }
        static string F(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        internal void Bind(UIRoot root) { ui = root; }
        internal bool IsOpen => race != Entity.Null && host.Context.World != null && host.Context.World.IsCreated && host.Context.EntityManager.Exists(race);
        internal bool InLobby => IsOpen && State.Phase == RacePhase.Lobby;
        internal bool IsFinished => IsOpen && State.Phase == RacePhase.Finished;
        RaceState State => host.Context.EntityManager.GetComponentData<RaceState>(race);

        internal DevCommandResult Open(double3 padsOrigin, int laps = 0)
        {
            var ctx = host.Context; var em = ctx.EntityManager;
            if (!ctx.TryGetService<Game.Data.Services.ContentService>(out var content) || !content.Catalog.TryGet<TrackDefinition>(new ContentId("track_01"), out var track))
                return DevCommandResult.Fail("track_01 is not in the content database (Game/Content/Build Race Track)");
            if (!ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("frame origin unavailable");
            ctx.TryGetService<GameSession>(out var session);
            End();
            em.CompleteAllTrackedJobs();
            blueprints.Clear();
            trackOrigin = padsOrigin + TrackOffset;
            float2 local = (float2)(trackOrigin - frame.OriginInStarSystem).xz;
            var frameOrigin = frame.OriginInStarSystem;
            race = TrackSpawnService.Spawn(em, track, local, Group, session, (blueprint, at, yaw, id) =>
            {
                // Wrecks and walls are static station grids; UniversePosition keeps them in place through origin rebases.
                var root = Game.Voxel.Grid.GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new Game.Voxel.Grid.GridSpawnParams
                {
                    PersistentId = id, Pose = new RigidTransform(quaternion.RotateY(yaw), new float3(at.x, 0, at.y)),
                    Kind = Game.Voxel.Model.GridKind.Station, Motion = Game.Voxel.Model.GridMotion.Static, Profile = Game.Voxel.Model.GridProfile.Station,
                    Flags = Game.Voxel.Model.GridSpawnFlags.Transient
                });
                em.AddComponentData(root, new Game.Galaxy.Components.UniversePosition { Value = frameOrigin + new double3(at.x, 0, at.y) });
                return root;
            }, spawnMines: true);
            var rs = em.GetComponentData<RaceState>(race);
            if (laps > 0) { rs.Laps = (byte)math.clamp(laps, 1, 20); em.SetComponentData(race, rs); }

            // Every pilot (host slot 0, guests 1+) to a grid slot in slot order, held until GO.
            var pilots = new List<(byte slot, Entity ship)>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<Hulk>()))
            using (var ships = q.ToEntityArray(Allocator.Temp))
                foreach (var s in ships) pilots.Add((em.GetComponentData<Pilot>(s).Slot, s));
            pilots.Sort((a, b) => a.slot.CompareTo(b.slot));
            var slots = em.GetBuffer<RaceSlot>(race).ToNativeArray(Allocator.Temp);
            var racers = new List<RacerProgress>();
            for (int i = 0; i < pilots.Count && i < slots.Length; i++)
            {
                var (slot, ship) = pilots[i];
                if (!em.HasComponent<RaceHold>(ship)) em.AddComponent<RaceHold>(ship);
                em.SetComponentEnabled<RaceHold>(ship, true);
                var p = local + slots[i].Position;
                DevEcs.TeleportRoot(em, ship, new float3(p.x, 0, p.y), quaternion.RotateY(slots[i].Yaw));
                racers.Add(new RacerProgress { Slot = slot, LastGate = -1, Ship = ship });
                var source = em.HasComponent<Game.Voxel.Model.GridSource>(ship) ? em.GetComponentData<Game.Voxel.Model.GridSource>(ship).Blueprint : default;
                if (source.Equals(default(ContentHash)) && slot == 0 && session?.Fleet?.Flagship != null) source = session.Fleet.Flagship.SourceBlueprint.Hash;
                blueprints[slot] = source;
            }
            slots.Dispose();
            var buffer = em.GetBuffer<RacerProgress>(race);
            foreach (var r in racers) buffer.Add(r);
            BuildVisuals(track); BuildHud();
            if (Role == Game.Core.Net.SimRoleKind.Host) { Coop?.SendAllGuests($"race view x={F(trackOrigin.x)} z={F(trackOrigin.z)} laps={State.Laps}"); sentPhase = (RacePhase)255; }
            return DevCommandResult.Ok($"{track.DisplayName}: {racers.Count} pilot(s) on the grid, {State.Laps} laps. Press F (or race start) for the countdown.");
        }

        internal DevCommandResult Start()
        {
            if (!IsOpen) return DevCommandResult.Fail("no race open: race open, or pad 18 RACE");
            var em = host.Context.EntityManager; var rs = State;
            if (rs.Phase != RacePhase.Lobby && rs.Phase != RacePhase.Finished) return DevCommandResult.Fail("the race is already running");
            if (rs.Phase == RacePhase.Finished) return Open(trackOrigin - TrackOffset, rs.Laps);
            rs.Phase = RacePhase.Countdown; rs.PhaseTick = (uint)host.Context.Tick;
            em.SetComponentData(race, rs);
            return DevCommandResult.Ok("3… 2… 1…");
        }

        internal void End()
        {
            var ctx = host.Context;
            if (race != Entity.Null && Role == Game.Core.Net.SimRoleKind.Host) Coop?.SendAllGuests("race end");
            if (ctx.World != null && ctx.World.IsCreated)
            {
                var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
                using (var q = em.CreateEntityQuery(typeof(RaceHold)))
                using (var ships = q.ToEntityArray(Allocator.Temp))
                    foreach (var s in ships) em.RemoveComponent<RaceHold>(s);
                TrackSpawnService.Despawn(em, Group);
                using (var q = em.CreateEntityQuery(typeof(RaceRespawnRequest))) em.DestroyEntity(q);
            }
            race = Entity.Null;
            if (visuals != null) UnityEngine.Object.Destroy(visuals);
            if (mineVisuals != null) UnityEngine.Object.Destroy(mineVisuals);
            mineVisuals = null; mineRings.Clear();
            if (material != null) UnityEngine.Object.Destroy(material);
            visuals = null; material = null; gateLines.Clear();
            hud?.RemoveFromHierarchy(); hud = null; banner = status = warning = results = null;
        }

        /// <summary>Puts this machine's pilot back at its last gate (a manual reset: no lap penalty).</summary>
        internal DevCommandResult Respawn()
        {
            if (!IsOpen) return DevCommandResult.Fail("no race open");
            var em = host.Context.EntityManager; var racers = em.GetBuffer<RacerProgress>(race);
            for (int i = 0; i < racers.Length; i++)
            {
                if (racers[i].Ship != host.Context.PlayerShipRoot) continue;
                var gates = em.GetBuffer<RaceGate>(race); var g = gates[math.max(0, (int)racers[i].LastGate)];
                float2 origin = em.GetComponentData<LocalTransform>(race).Position.xz, p = origin + g.RespawnPosition;
                DevEcs.TeleportRoot(em, racers[i].Ship, new float3(p.x, 0, p.y), quaternion.RotateY(g.RespawnYaw));
                return DevCommandResult.Ok("Back on the line at gate " + math.max(0, (int)racers[i].LastGate));
            }
            return DevCommandResult.Fail("you are not in this race");
        }

        /// <summary>Dev and practice: the player `back` metres behind gate `id`, facing through it, at rest.</summary>
        internal DevCommandResult ToGate(int id, float back)
        {
            if (!IsOpen) return DevCommandResult.Fail("no race open");
            var em = host.Context.EntityManager; var gates = em.GetBuffer<RaceGate>(race);
            if (id < 0 || id >= gates.Length) return DevCommandResult.Fail("gate 0.." + (gates.Length - 1));
            var g = gates[id]; float2 origin = em.GetComponentData<LocalTransform>(race).Position.xz;
            float2 p = origin + g.Position - new float2(math.sin(g.Yaw), math.cos(g.Yaw)) * back;
            DevEcs.TeleportRoot(em, host.Context.PlayerShipRoot, new float3(p.x, 0, p.y), quaternion.RotateY(g.Yaw));
            return DevCommandResult.Ok($"{back:0} m behind gate {id}");
        }

        internal string Info()
        {
            if (!IsOpen) return "no race open";
            var em = host.Context.EntityManager; var rs = State; var text = new System.Text.StringBuilder();
            text.Append(rs.Phase).Append(" laps ").Append(rs.Laps).Append(" tick ").Append(host.Context.Tick).Append('\n');
            foreach (var r in em.GetBuffer<RacerProgress>(race))
                text.Append($"slot {r.Slot}: place {r.Place} lap {r.Lap} last gate {r.LastGate} gates {r.GatesThisLap} best {r.BestLapTicks / 60f:0.00}s flags {r.Flags}\n");
            return text.ToString();
        }

        internal void Tick()
        {
            if (race == Entity.Null) return;
            if (!IsOpen) { End(); return; }
            var ctx = host.Context; var em = ctx.EntityManager;
            // Respawn requests (RACE-006 lands the fresh-ship path; a racer whose ship still flies is put back on the line).
            using (var q = em.CreateEntityQuery(typeof(RaceRespawnRequest)))
            using (var requests = q.ToEntityArray(Allocator.Temp))
                foreach (var e in requests)
                {
                    var request = em.GetComponentData<RaceRespawnRequest>(e);
                    em.DestroyEntity(e);
                    var racers = em.GetBuffer<RacerProgress>(race);
                    bool live = false;
                    for (int i = 0; i < racers.Length; i++)
                        if (racers[i].Slot == request.Slot && racers[i].Ship != Entity.Null && em.Exists(racers[i].Ship) && !em.HasComponent<Hulk>(racers[i].Ship))
                        { DevEcs.TeleportRoot(em, racers[i].Ship, new float3(request.Position.x, 0, request.Position.y), quaternion.RotateY(request.Yaw)); live = true; }
                    if (!live) SpawnFresh(request);
                }
            if (Role == Game.Core.Net.SimRoleKind.Host && (++syncFrame % 12 == 0 || State.Phase != sentPhase)) { sentPhase = State.Phase; Coop?.SendAllGuests(SyncText()); }
            if (Role == Game.Core.Net.SimRoleKind.Guest) ApplyLocalHold();
            UpdateVisuals(); UpdateHud();
        }

        // ------------------------------------------------------------------------------------------------ co-op (RACE-009)
        // The host runs the race; guests mirror it. `race view` builds the same zones, gates and visuals on a guest (wrecks arrive as
        // replicated grids), and `race sync` carries the phase and every racer's progress, with times as ticks before now, so they
        // need no shared clock. A guest holds its own predicted ship through the countdown like the host holds it.
        string SyncText()
        {
            var em = host.Context.EntityManager; var rs = State; uint now = (uint)host.Context.Tick;
            var text = new System.Text.StringBuilder("race sync phase=").Append((int)rs.Phase).Append(" ago=").Append(now - rs.PhaseTick)
                .Append(" start=").Append(rs.StartTick == 0 ? 0 : now - rs.StartTick).Append(" laps=").Append(rs.Laps).Append(" racers=");
            bool first = true;
            foreach (var r in em.GetBuffer<RacerProgress>(race))
            {
                if (!first) text.Append(';'); first = false;
                text.Append(r.Slot).Append(':').Append(r.Lap).Append(':').Append(r.LastGate).Append(':').Append(r.GatesThisLap).Append(':').Append(r.Place)
                    .Append(':').Append((int)r.Flags).Append(':').Append(r.BestLapTicks).Append(':').Append(r.LastLapTicks)
                    .Append(':').Append(now - r.LapStartTick).Append(':').Append(r.FinishTick == 0 ? 0 : now - r.FinishTick);
            }
            return text.ToString();
        }

        /// <summary>Guest: build the host's track locally around its universe origin.</summary>
        internal DevCommandResult View(double3 origin, int laps)
        {
            var ctx = host.Context; var em = ctx.EntityManager;
            if (!ctx.TryGetService<Game.Data.Services.ContentService>(out var content) || !content.Catalog.TryGet<TrackDefinition>(new ContentId("track_01"), out var track))
                return DevCommandResult.Fail("track_01 missing");
            if (!ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("frame origin unavailable");
            End();
            em.CompleteAllTrackedJobs();
            trackOrigin = origin;
            race = TrackSpawnService.Spawn(em, track, (float2)(origin - frame.OriginInStarSystem).xz, Group, new LocalIds());
            if (laps > 0) { var rs = State; rs.Laps = (byte)laps; em.SetComponentData(race, rs); }
            BuildVisuals(track); BuildHud();
            return DevCommandResult.Ok("showing the host's race");
        }
        sealed class LocalIds : IPersistentIdAllocator { ulong next = 1UL << 48; public ulong Allocate() => next++; }

        /// <summary>Guest: apply the host's race state.</summary>
        internal DevCommandResult Sync(DevArgs args)
        {
            if (!IsOpen) return DevCommandResult.Fail("no race view");
            var em = host.Context.EntityManager; uint now = (uint)host.Context.Tick;
            uint Ago(long ago) => ago <= 0 ? 0 : now - (uint)math.min(ago, now);
            var rs = State;
            rs.Phase = (RacePhase)args.GetLong("phase", 0); rs.PhaseTick = now - (uint)math.min(args.GetLong("ago", 0), now);
            rs.StartTick = Ago(args.GetLong("start", 0)); rs.Laps = (byte)args.GetLong("laps", rs.Laps);
            em.SetComponentData(race, rs);
            var racers = em.GetBuffer<RacerProgress>(race); racers.Clear();
            foreach (var row in args.GetString("racers", "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var f = row.Split(':'); if (f.Length < 10) continue;
                long L(int i) => long.Parse(f[i], System.Globalization.CultureInfo.InvariantCulture);
                racers.Add(new RacerProgress
                {
                    Slot = (byte)L(0), Lap = (byte)L(1), LastGate = (short)L(2), GatesThisLap = (byte)L(3), Place = (byte)L(4), Flags = (RacerFlags)L(5),
                    BestLapTicks = (uint)L(6), LastLapTicks = (uint)L(7), LapStartTick = now - (uint)math.min(L(8), now), FinishTick = Ago(L(9))
                });
            }
            return DevCommandResult.Ok("synced");
        }

        void ApplyLocalHold()
        {
            var em = host.Context.EntityManager; var ship = host.Context.PlayerShipRoot;
            if (ship == Entity.Null) return;
            bool hold = State.Phase == RacePhase.Lobby || State.Phase == RacePhase.Countdown;
            if (!em.HasComponent<RaceHold>(ship)) { if (!hold) return; em.AddComponent<RaceHold>(ship); }
            if (em.IsComponentEnabled<RaceHold>(ship) != hold) em.SetComponentEnabled<RaceHold>(ship, hold);
        }

        /// <summary>RACE-006: a destroyed racer flies again in a fresh ship of its blueprint. Its hulk stays on the track as an obstacle
        /// but gives up the pilot's tags, so the camera, HUD and controls follow the new ship.</summary>
        void SpawnFresh(in RaceRespawnRequest request)
        {
            var ctx = host.Context; var em = ctx.EntityManager;
            if (!blueprints.TryGetValue(request.Slot, out var blueprint) || blueprint.Equals(default(ContentHash)))
            { GameLog.Warn(LogCategories.Dev, "Race: no blueprint to respawn pilot " + request.Slot); return; }
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<Hulk>()))
            using (var hulks = q.ToEntityArray(Allocator.Temp))
                foreach (var h in hulks)
                {
                    if (em.GetComponentData<Pilot>(h).Slot != request.Slot) continue;
                    em.RemoveComponent<Pilot>(h);
                    if (em.HasComponent<PlayerShip>(h)) em.RemoveComponent<PlayerShip>(h);
                }
            if (request.Slot != 0)
            {
                // RACE-009: a guest's fresh ship replicates as theirs (RemotePilot), so their camera and HUD follow it.
                if (Coop == null || !Coop.RespawnGuestShip(request.Slot, new float3(request.Position.x, 0, request.Position.y), quaternion.RotateY(request.Yaw)))
                    GameLog.Warn(LogCategories.Dev, "Race: could not respawn pilot " + request.Slot);
                return;
            }
            if (!ctx.TryGetService<GameSession>(out var session)) return;
            var root = Game.Voxel.Grid.GridSpawnApi.RequestSpawnBlueprint(em, blueprint, new Game.Voxel.Grid.GridSpawnParams
            {
                PersistentId = session.Allocate(), Pose = new RigidTransform(quaternion.RotateY(request.Yaw), new float3(request.Position.x, 0, request.Position.y)),
                Kind = Game.Voxel.Model.GridKind.Ship, Motion = Game.Voxel.Model.GridMotion.Dynamic, Profile = Game.Voxel.Model.GridProfile.PlayerShip
            });
            em.AddComponentData(root, Game.Simulation.Allegiance.Player);
        }

        // ------------------------------------------------------------------------------------------------ visuals
        void BuildVisuals(TrackDefinition track)
        {
            visuals = new GameObject("Race track visuals"); visuals.transform.SetParent(host.transform, false);
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_ZWrite", 0); material.SetInt("_Cull", 0);
            var wall = new Color(.25f, .9f, 1f, .9f); var boost = new Color(.35f, 1f, .5f, .9f);
            foreach (var z in track.Zones)
            {
                if (z.Zone == null) continue;
                if (z.Zone.Shape == Game.Data.Hazards.ZoneShape.Path)
                {
                    var pts = new Vector3[z.Path.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(z.Path[i].x, 0, z.Path[i].y);
                    Line("wall", pts, (z.PathRadius > 0 ? z.PathRadius : z.Zone.PathRadius) * 2, wall, false);
                }
                else
                {
                    // boost chevrons along the strip
                    float2 c = z.Position, f = new float2(math.sin(z.Yaw), math.cos(z.Yaw)), r = new float2(f.y, -f.x);
                    float half = z.HalfExtents.y > 0 ? z.HalfExtents.y : z.Zone.HalfExtents.y, w = z.HalfExtents.x > 0 ? z.HalfExtents.x : z.Zone.HalfExtents.x;
                    for (float s = -half + 20; s < half; s += 40)
                    {
                        float2 tip = c + f * (s + 14), a = c + f * s + r * w * .5f, b = c + f * s - r * w * .5f;
                        Line("boost", new[] { new Vector3(a.x, -1, a.y), new Vector3(tip.x, -1, tip.y), new Vector3(b.x, -1, b.y) }, 5, boost, false);
                    }
                }
            }
            foreach (var g in track.Gates)
            {
                float2 r = new float2(math.cos(g.Yaw), -math.sin(g.Yaw)) * g.Width * .5f;
                var line = Line("gate", new[] { new Vector3(g.Position.x - r.x, 0, g.Position.y - r.y), new Vector3(g.Position.x + r.x, 0, g.Position.y + r.y) }, 3,
                    g.Required ? new Color(1, .85f, .2f, .9f) : new Color(1, .5f, .25f, .7f), false);
                gateLines.Add(line);
            }
        }
        LineRenderer Line(string name, Vector3[] points, float width, Color color, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(visuals.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = loop; line.widthMultiplier = width;
            line.positionCount = points.Length; line.SetPositions(points); line.startColor = line.endColor = color;
            line.alignment = LineAlignment.TransformZ; go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            // TransformZ alignment lays the ribbon flat: rotate points back into the parent's plane
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3(points[i].x, points[i].z, -points[i].y);
            line.SetPositions(points);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        void UpdateVisuals()
        {
            if (visuals == null || !host.Context.TryGetFrame(out var frame)) return;
            visuals.transform.position = (float3)(trackOrigin - frame.OriginInStarSystem);
            var em = host.Context.EntityManager;
            ulong next = 1;
            byte me = LocalSlot;
            foreach (var r in em.GetBuffer<RacerProgress>(race))
                if (r.Slot == me) next = r.LastGate >= 0 ? em.GetBuffer<RaceGate>(race)[r.LastGate].NextMask : 1;
            for (int i = 0; i < gateLines.Count; i++) gateLines[i].widthMultiplier = (next & (1UL << i)) != 0 ? 9 : 3;
            UpdateMines();
        }

        // HAZ-008: a ring the size of each mine's trigger radius, under it (grey arming, pulsing red armed, flashing triggered).
        void UpdateMines()
        {
            if (material == null) return;
            var em = host.Context.EntityManager;
            if (mineVisuals == null) { mineVisuals = new GameObject("Race mines"); mineVisuals.transform.SetParent(host.transform, false); }
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Voxel.Model.VoxelGrid>(), ComponentType.ReadOnly<LocalTransform>());
            using var grids = q.ToEntityArray(Allocator.Temp);
            int used = 0; float t = UnityEngine.Time.time;
            foreach (var e in grids)
            {
                if (em.GetComponentData<Game.Voxel.Model.VoxelGrid>(e).Kind != Game.Voxel.Model.GridKind.Device) continue;
                if (used == mineRings.Count)
                {
                    var go = new GameObject("mine"); go.transform.SetParent(mineVisuals.transform, false);
                    var ring = go.AddComponent<LineRenderer>(); ring.sharedMaterial = material; ring.useWorldSpace = false; ring.loop = true;
                    ring.positionCount = 32; ring.widthMultiplier = 1.2f; ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    for (int i = 0; i < 32; i++) { float a = i * math.PI * 2 / 32; ring.SetPosition(i, new Vector3(math.cos(a) * 16, -1, math.sin(a) * 16)); }
                    mineRings.Add(ring);
                }
                var r = mineRings[used++]; r.gameObject.SetActive(true);
                r.transform.localPosition = em.GetComponentData<LocalTransform>(e).Position;
                Color c = new Color(1, .25f, .2f, .5f);
                if (em.HasComponent<MineState>(e))
                    switch (em.GetComponentData<MineState>(e).Phase)
                    {
                        case MinePhase.Arming: c = new Color(.6f, .6f, .6f, .4f); break;
                        case MinePhase.Armed: c = new Color(1, .25f, .2f, .35f + .3f * math.sin(t * 3)); break;
                        case MinePhase.Triggered: c = math.sin(t * 30) > 0 ? new Color(1, 1, 1, .95f) : new Color(1, .2f, .1f, .95f); break;
                    }
                r.startColor = r.endColor = c;
            }
            for (int i = used; i < mineRings.Count; i++) mineRings[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------------ HUD
        void BuildHud()
        {
            if (ui == null) return;
            hud = new VisualElement { name = "race-hud", pickingMode = PickingMode.Ignore };
            hud.style.position = Position.Absolute; hud.style.left = 0; hud.style.right = 0; hud.style.top = 24; hud.style.alignItems = Align.Center;
            banner = Label(hud, "", "text-xl"); status = Label(hud, "", "text-m"); warning = Label(hud, "", "text-l");
            warning.style.color = new Color(1, .35f, .25f); results = Label(hud, "", "text-s"); results.style.whiteSpace = WhiteSpace.Normal;
            results.AddToClassList("panel");
            ui.GetLayer(UILayer.Hud).Add(hud);
        }
        static Label Label(VisualElement parent, string text, string style)
        { var l = new Label(text) { pickingMode = PickingMode.Ignore }; l.AddToClassList(style); parent.Add(l); return l; }
        static string Time(uint ticks) => ticks == 0 ? "--:--.--" : $"{ticks / 3600}:{ticks / 60 % 60:00}.{ticks % 60 * 100 / 60:00}";

        void UpdateHud()
        {
            if (hud == null) return;
            var em = host.Context.EntityManager; var rs = State; uint tick = (uint)host.Context.Tick;
            var racers = em.GetBuffer<RacerProgress>(race);
            RacerProgress me = default; bool found = false;
            byte slot = LocalSlot;
            foreach (var r in racers) if (r.Slot == slot) { me = r; found = true; }
            switch (rs.Phase)
            {
                case RacePhase.Lobby: banner.text = "BATTLE GRAVEYARD"; status.text = $"{racers.Length} on the grid  ·  {rs.Laps} laps  ·  F TO START"; break;
                case RacePhase.Countdown:
                    uint left = RaceRules.CountdownTicks - math.min(RaceRules.CountdownTicks, tick - rs.PhaseTick);
                    banner.text = ((left + 59) / 60).ToString(); status.text = "GET READY"; break;
                case RacePhase.Racing:
                    banner.text = tick - rs.PhaseTick < 60 ? "GO" : "";
                    status.text = found ? $"LAP {math.min(me.Lap + 1, rs.Laps)}/{rs.Laps}   ·   P{me.Place}/{racers.Length}   ·   {Time(tick - me.LapStartTick)}   ·   BEST {Time(me.BestLapTicks)}" : "SPECTATING";
                    if (found && (me.Flags & RacerFlags.Finished) != 0) status.text = $"FINISHED P{me.Place}  ·  BEST {Time(me.BestLapTicks)}";
                    break;
                case RacePhase.Finished: banner.text = "RESULTS"; status.text = "F TO RACE AGAIN  ·  race end TO LEAVE"; break;
            }
            warning.text = found && (me.Flags & RacerFlags.WrongWay) != 0 ? "WRONG WAY" : found && (me.Flags & RacerFlags.Respawning) != 0 ? "RESPAWNING" : "";
            if (rs.Phase == RacePhase.Finished || rs.Phase == RacePhase.Racing && rs.FirstFinishTick != 0)
            {
                var rows = new List<RacerProgress>(); foreach (var r in racers) rows.Add(r);
                rows.Sort((a, b) => a.Place.CompareTo(b.Place));
                var text = new System.Text.StringBuilder();
                foreach (var r in rows)
                    text.Append($"P{r.Place}   {PilotLabel(r.Slot)}   {((r.Flags & RacerFlags.Finished) != 0 ? Time(r.FinishTick - rs.StartTick) : "DNF")}   BEST {Time(r.BestLapTicks)}\n");
                results.text = text.ToString(); results.style.display = DisplayStyle.Flex;
            }
            else results.style.display = DisplayStyle.None;
        }

        string PilotLabel(byte slot)
        {
            var em = host.Context.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<Game.Core.Net.PilotName>());
            using var ships = q.ToEntityArray(Allocator.Temp);
            foreach (var s in ships) if (em.GetComponentData<Pilot>(s).Slot == slot) return em.GetComponentData<Game.Core.Net.PilotName>(s).Value.ToString().ToUpperInvariant();
            return slot == LocalSlot ? "YOU" : "PILOT " + (slot + 1);
        }

        public void Dispose() => End();
    }
}
#endif
