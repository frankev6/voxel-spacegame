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
        Game.Presentation.Racing.TrackDressing dressing;
        readonly List<(Vector3, float, Game.Presentation.Racing.TrackDressing.MineLook)> mineLooks = new();
        double3 trackOrigin;
        VisualElement hud; Label banner, status, warning, results;
        readonly Dictionary<byte, ContentHash> blueprints = new();   // each racer's ship, for respawns
        // RACE-017: AI racers take the grid slots no pilot fills. Their race slots start here, clear of co-op pilot slots.
        internal const byte AiSlotBase = 16;
        static readonly string[] AiShips = { "ship_racer_needle", "ship_racer_kite", "ship_racer_gimbal", "ship_racer_hammerhead", "ship_racer_battering_ram" };
        static readonly string[] AiNames = { "VEGA", "ORION", "LYRA", "CASSIA", "RIGEL", "NOVA", "ATLAS", "SIRIUS" };
        readonly Dictionary<byte, string> aiNames = new();
        bool autopilot;
        System.IO.StreamWriter trace; string tracePath; long tracedTick = -100;

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

        /// <summary>`ai` AI racers join (−1: one in every grid slot no pilot takes).</summary>
        internal DevCommandResult Open(double3 padsOrigin, int laps = 0, int ai = -1)
        {
            var ctx = host.Context; var em = ctx.EntityManager;
            if (!ctx.TryGetService<Game.Data.Services.ContentService>(out var content) || !content.Catalog.TryGet<TrackDefinition>(new ContentId("track_01"), out var track))
                return DevCommandResult.Fail("track_01 is not in the content database (Game/Content/Build Race Track)");
            if (!ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("frame origin unavailable");
            ctx.TryGetService<GameSession>(out var session);
            End();
            em.CompleteAllTrackedJobs();
            blueprints.Clear(); aiNames.Clear();
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
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<Hulk>(), ComponentType.Exclude<TrackPieceOf>()))
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
            // RACE-017: AI racers in the free slots, each in a different racer from the others and from the host's.
            int aiCount = math.min(ai < 0 ? slots.Length : ai, slots.Length - racers.Count);
            var taken = new HashSet<ContentHash>(blueprints.Values);
            for (int k = 0, ship = 0; k < aiCount; k++)
            {
                ContentHash blueprint = default;
                for (int tries = 0; tries < AiShips.Length && blueprint.Equals(default(ContentHash)); tries++, ship++)
                {
                    if (!content.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId(AiShips[ship % AiShips.Length]), out var definition)) continue;
                    if (taken.Add(definition.Id.Hash) || tries == AiShips.Length - 1) blueprint = definition.Id.Hash;
                }
                if (blueprint.Equals(default(ContentHash))) break;
                byte slot = (byte)(AiSlotBase + k);
                var at = slots[racers.Count];
                blueprints[slot] = blueprint; aiNames[slot] = AiNames[k % AiNames.Length];
                var root = SpawnAiRacer(slot, blueprint, local + at.Position, at.Yaw);
                racers.Add(new RacerProgress { Slot = slot, LastGate = -1, Ship = root });
            }
            slots.Dispose();
            var buffer = em.GetBuffer<RacerProgress>(race);
            foreach (var r in racers) buffer.Add(r);
            BuildVisuals(track); BuildHud();
            if (Role == Game.Core.Net.SimRoleKind.Host) { Coop?.SendAllGuests($"race view x={F(trackOrigin.x)} z={F(trackOrigin.z)} laps={State.Laps}"); sentPhase = (RacePhase)255; }
            return DevCommandResult.Ok($"{track.DisplayName}: {racers.Count - aiNames.Count} pilot(s) and {aiNames.Count} AI on the grid, {State.Laps} laps. Press F (or race start) for the countdown.");
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
                var mine = ctx.PlayerShipRoot;
                if (mine != Entity.Null && em.Exists(mine) && em.HasComponent<RacerPilot>(mine)) { em.RemoveComponent<RacerPilot>(mine); em.RemoveComponent<RacerLinePoint>(mine); }
                // AI racers leave with the track (TrackPieceOf); they stop being pilots now, so a reopened race never binds them.
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<TrackPieceOf>()))
                using (var ships = q.ToEntityArray(Allocator.Temp))
                    foreach (var s in ships) em.RemoveComponent<Pilot>(s);
                TrackSpawnService.Despawn(em, Group);
                using (var q = em.CreateEntityQuery(typeof(RaceRespawnRequest))) em.DestroyEntity(q);
            }
            race = Entity.Null; autopilot = false;
            trace?.Dispose(); trace = null;
            dressing?.Dispose(); dressing = null;
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

        /// <summary>Dev (RACE-016): an explosion on the emitter head nearest the player's ship, to test that its fence spans drop.</summary>
        internal DevCommandResult Blast(float radius, float damage)
        {
            if (!IsOpen) return DevCommandResult.Fail("no race open");
            var em = host.Context.EntityManager; var ship = host.Context.PlayerShipRoot;
            if (ship == Entity.Null || !em.HasComponent<LocalTransform>(ship)) return DevCommandResult.Fail("no player ship");
            float2 at = em.GetComponentData<LocalTransform>(ship).Position.xz, best = default; float bestD = float.MaxValue; ushort index = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<WallSpan>(), ComponentType.ReadOnly<LocalTransform>()))
            using (var spans = q.ToComponentDataArray<WallSpan>(Allocator.Temp))
            using (var poses = q.ToComponentDataArray<LocalTransform>(Allocator.Temp))
                for (int i = 0; i < spans.Length; i++)
                    foreach (var p in new[] { poses[i].Position.xz + spans[i].A, poses[i].Position.xz + spans[i].B })
                        if (math.distancesq(p, at) < bestD) { bestD = math.distancesq(p, at); best = p; index = spans[i].Index; }
            if (bestD == float.MaxValue) return DevCommandResult.Fail("no emitter posts");
            var streams = em.CreateEntityQuery(typeof(DamageStreams)).GetSingleton<DamageStreams>();
            em.CompleteAllTrackedJobs();
            streams.Explosions.PostFromMainThread(new ExplosionRequest { WorldPos = new float3(best.x, 9, best.y), Radius = radius, Amount = damage,
                Type = Game.Data.Combat.DamageType.Explosive, Producer = Game.Data.Combat.ProducerKind.Hazard, Sequence = 77 });
            return DevCommandResult.Ok($"blast at ({best.x:0.0}, {best.y:0.0}), {math.sqrt(bestD):0} m away (span {index})");
        }

        /// <summary>Dev (RACE-016): one of the track's mines `ahead` metres in front of the player's ship, arming at once.</summary>
        internal DevCommandResult MineAhead(float ahead)
        {
            if (!IsOpen) return DevCommandResult.Fail("no race open");
            var ctx = host.Context; var em = ctx.EntityManager; var ship = ctx.PlayerShipRoot;
            if (ship == Entity.Null || !em.HasComponent<LocalTransform>(ship)) return DevCommandResult.Fail("no player ship");
            if (!ctx.TryGetService<Game.Data.Services.ContentService>(out var content) || !content.Catalog.TryGet<MineDefinition>(new ContentId("mine_proximity"), out var def))
                return DevCommandResult.Fail("mine_proximity missing");
            var pose = em.GetComponentData<LocalTransform>(ship); float3 f = math.rotate(pose.Rotation, new float3(0, 0, 1));
            float2 at = pose.Position.xz + math.normalizesafe(f.xz) * ahead;
            ctx.TryGetService<GameSession>(out var session);
            em.CompleteAllTrackedJobs();
            HazardSpawn.Mine(em, def, at, 0, Group, session != null ? session.Allocate() : 0, (uint)ctx.Tick);
            return DevCommandResult.Ok($"mine at ({at.x:0.0}, {at.y:0.0}), {ahead:0} m ahead");
        }

        internal string Info()
        {
            if (!IsOpen) return "no race open";
            var em = host.Context.EntityManager; var rs = State; var text = new System.Text.StringBuilder();
            text.Append(rs.Phase).Append(" laps ").Append(rs.Laps).Append(" tick ").Append(host.Context.Tick);
            using (var all = em.CreateEntityQuery(ComponentType.ReadOnly<WallSpan>()))
            using (var down = em.CreateEntityQuery(ComponentType.ReadOnly<WallSpan>(), ComponentType.ReadOnly<ZoneOff>(), ComponentType.ReadOnly<LocalTransform>()))
            using (var spans = all.ToComponentDataArray<WallSpan>(Allocator.Temp))
            {
                int seen = 0; foreach (var w in spans) if (w.Seen == 3) seen++;
                text.Append(" spans ").Append(spans.Length).Append(" (").Append(seen).Append(" with both posts seen, ").Append(down.CalculateEntityCount()).Append(" down)");
                using var downSpans = down.ToComponentDataArray<WallSpan>(Allocator.Temp);
                using var downPoses = down.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                for (int i = 0; i < downSpans.Length && i < 8; i++)
                {
                    var w = downSpans[i]; float2 a = downPoses[i].Position.xz + w.A, b = downPoses[i].Position.xz + w.B;
                    text.Append(i == 0 ? " down:" : ",").Append(w.Index).Append('/').Append(w.Lost).Append($" ({a.x:0},{a.y:0} {w.CellsA}c)-({b.x:0},{b.y:0} {w.CellsB}c)");
                }
            }
            text.Append('\n');
            var me = host.Context.PlayerShipRoot;
            if (me != Entity.Null && em.HasComponent<Unity.Physics.PhysicsVelocity>(me))
            {
                var pose = em.GetComponentData<LocalTransform>(me); var v = em.GetComponentData<Unity.Physics.PhysicsVelocity>(me); var m = em.GetComponentData<Unity.Physics.PhysicsMass>(me);
                float3 f = math.rotate(pose.Rotation, new float3(0, 0, 1));
                float yawRate = math.rotate(math.mul(pose.Rotation, m.InertiaOrientation), v.Angular).y;
                text.Append($"ship x {pose.Position.x:0.0} z {pose.Position.z:0.0} heading {math.degrees(math.atan2(f.x, f.z)):0} speed {math.length(v.Linear.xz):0.0} yawRate {yawRate:0.00} angular {math.length(v.Angular):0.00}\n");
            }
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
            var mine = ctx.PlayerShipRoot;
            if (autopilot && mine != Entity.Null && em.Exists(mine) && !em.HasComponent<RacerPilot>(mine)) RacerPilotSystem.Attach(em, mine, 1, 1);
            if (Role == Game.Core.Net.SimRoleKind.Host && (++syncFrame % 12 == 0 || State.Phase != sentPhase)) { sentPhase = State.Phase; Coop?.SendAllGuests(SyncText()); }
            if (Role == Game.Core.Net.SimRoleKind.Guest) ApplyLocalHold();
            TraceRow();
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
                    if (em.HasComponent<RacerPilot>(h)) em.RemoveComponent<RacerPilot>(h);
                }
            if (request.Slot >= AiSlotBase) { SpawnAiRacer(request.Slot, blueprint, request.Position, request.Yaw); return; }
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

        /// <summary>RACE-017: an AI racer's ship: a crewless racer (no combat AI) flown by RacerPilotSystem, held until GO. It is a track
        /// piece of the race group, so ending the race removes it and its hulks.</summary>
        Entity SpawnAiRacer(byte slot, ContentHash blueprint, float2 position, float yaw)
        {
            var ctx = host.Context; var em = ctx.EntityManager;
            ctx.TryGetService<GameSession>(out var session);
            var root = Game.Voxel.Grid.GridSpawnApi.RequestSpawnBlueprint(em, blueprint, new Game.Voxel.Grid.GridSpawnParams
            {
                PersistentId = session != null ? session.Allocate() : 0, Pose = new RigidTransform(quaternion.RotateY(yaw), new float3(position.x, 0, position.y)),
                Kind = Game.Voxel.Model.GridKind.Ship, Motion = Game.Voxel.Model.GridMotion.Dynamic, Profile = Game.Voxel.Model.GridProfile.Dummy
            });
            em.AddComponentData(root, new Game.Simulation.Allegiance { Faction = 0, Team = slot });   // a player-side ship, nobody's wingmate
            em.AddComponentData(root, new Pilot { Slot = slot });
            em.AddComponentData(root, Game.Core.Net.PilotName.From(aiNames.TryGetValue(slot, out var name) ? name : "AI " + slot));
            em.AddComponentData(root, new TrackPieceOf { Group = Group });
            RacerPilotSystem.Attach(em, root, .55f + .4f * (((slot - AiSlotBase) * 37) % 10) / 9f, (uint)(slot * 7919 + 17));
            em.AddComponent<RaceHold>(root); em.SetComponentEnabled<RaceHold>(root, State.Phase == RacePhase.Lobby || State.Phase == RacePhase.Countdown);
            return root;
        }

        /// <summary>RACE-017 dev: the racer AI flies this machine's ship (host or solo), to test the track hands-off.</summary>
        internal DevCommandResult Autopilot(string mode)
        {
            if (Role == Game.Core.Net.SimRoleKind.Guest) return DevCommandResult.Fail("the autopilot runs on the host");
            autopilot = mode == "on" || mode != "off" && !autopilot;
            var em = host.Context.EntityManager; var ship = host.Context.PlayerShipRoot;
            if (ship != Entity.Null && em.Exists(ship))
            {
                if (autopilot && !em.HasComponent<RacerPilot>(ship)) RacerPilotSystem.Attach(em, ship, 1, 1);
                if (!autopilot && em.HasComponent<RacerPilot>(ship)) { em.RemoveComponent<RacerPilot>(ship); em.RemoveComponent<RacerLinePoint>(ship); }
            }
            return DevCommandResult.Ok(autopilot ? "autopilot on: the racer AI flies your ship (race autopilot off to fly)" : "autopilot off");
        }

        string LinePoint(Entity ship, int index)
        {
            var em = host.Context.EntityManager; var line = em.GetBuffer<RacerLinePoint>(ship);
            if (index < 0 || index >= line.Length) return "-";
            float2 p = em.GetComponentData<LocalTransform>(race).Position.xz + line[index].Position;
            return $"({p.x:0},{p.y:0})";
        }

        /// <summary>RACE-017 dev: records every racer 10 times a second (track space) to a CSV for tools/playtest/race-plot.py.</summary>
        internal DevCommandResult Trace(string mode, string file)
        {
            bool on = mode == "on" || mode != "off" && trace == null;
            trace?.Dispose(); trace = null;
            if (!on) return DevCommandResult.Ok("trace off: " + tracePath);
            tracePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", string.IsNullOrEmpty(file) ? "race-trace.csv" : file));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(tracePath));
            trace = new System.IO.StreamWriter(tracePath, false, new System.Text.UTF8Encoding(false));
            trace.WriteLine("tick,phase,slot,name,x,z,yaw,vx,vz,yawrate,mass,lap,lastgate,flags,ai,target,off,threat,steer,aimx,aimz,cmd,linefrom,lineto,index,replans");
            // the boost strips as spawned (track space), for the plot
            var inv = System.Globalization.CultureInfo.InvariantCulture; var em = host.Context.EntityManager;
            using (var boxes = new System.IO.StreamWriter(System.IO.Path.ChangeExtension(tracePath, ".boost.csv"), false, new System.Text.UTF8Encoding(false)))
            {
                boxes.WriteLine("x,z,yaw,hx,hz");
                if (IsOpen && em.HasBuffer<RaceBoost>(race))
                    foreach (var b in em.GetBuffer<RaceBoost>(race))
                        boxes.WriteLine(string.Join(",", b.Center.x.ToString("0.0", inv), b.Center.y.ToString("0.0", inv), b.Yaw.ToString("0.0000", inv), b.HalfExtents.x.ToString("0.0", inv), b.HalfExtents.y.ToString("0.0", inv)));
            }
            return DevCommandResult.Ok("tracing to " + tracePath);
        }

        void TraceRow()
        {
            var ctx = host.Context; var em = ctx.EntityManager;
            if (trace == null || ctx.Tick - tracedTick < 6) return;   // per frame; one row per racer every 6 sim ticks
            tracedTick = (long)ctx.Tick;
            float2 origin = em.GetComponentData<LocalTransform>(race).Position.xz;
            var inv = System.Globalization.CultureInfo.InvariantCulture; var rs = State;
            foreach (var r in em.GetBuffer<RacerProgress>(race))
            {
                var s = r.Ship;
                if (s == Entity.Null || !em.Exists(s) || !em.HasComponent<LocalTransform>(s)) continue;
                var pose = em.GetComponentData<LocalTransform>(s); float2 p = pose.Position.xz - origin;
                float3 f = math.rotate(pose.Rotation, new float3(0, 0, 1));
                float3 v = em.HasComponent<Unity.Physics.PhysicsVelocity>(s) ? em.GetComponentData<Unity.Physics.PhysicsVelocity>(s).Linear : default;
                float yawRate = em.HasComponent<Game.Simulation.Flight.FlightState>(s) ? em.GetComponentData<Game.Simulation.Flight.FlightState>(s).YawRate : 0;
                float mass = em.HasComponent<Unity.Physics.PhysicsMass>(s) && em.GetComponentData<Unity.Physics.PhysicsMass>(s).InverseMass > 0 ? 1 / em.GetComponentData<Unity.Physics.PhysicsMass>(s).InverseMass : 0;
                var row = new System.Text.StringBuilder();
                row.Append(ctx.Tick).Append(',').Append((int)rs.Phase).Append(',').Append(r.Slot).Append(',').Append(PilotLabel(r.Slot)).Append(',')
                    .Append(p.x.ToString("0.0", inv)).Append(',').Append(p.y.ToString("0.0", inv)).Append(',').Append(math.atan2(f.x, f.z).ToString("0.000", inv)).Append(',')
                    .Append(v.x.ToString("0.0", inv)).Append(',').Append(v.z.ToString("0.0", inv)).Append(',').Append(yawRate.ToString("0.00", inv)).Append(',')
                    .Append(mass.ToString("0", inv)).Append(',').Append(r.Lap).Append(',').Append(r.LastGate).Append(',').Append((int)r.Flags).Append(',');
                if (em.HasComponent<RacerPilot>(s))
                {
                    var a = em.GetComponentData<RacerPilot>(s);
                    var command = em.HasComponent<ShipCommand>(s) ? em.GetComponentData<ShipCommand>(s) : default;
                    int flags = (int)command.Flags | (command.FireGroups != 0 ? 4 : 0);   // 1 boost, 2 brake, 4 firing
                    row.Append(1).Append(',').Append(a.TargetSpeed.ToString("0", inv)).Append(',').Append(a.Off.ToString("0", inv)).Append(',').Append(a.Threat.ToString("0.0", inv)).Append(',')
                        .Append(a.Steer.ToString("0.00", inv)).Append(',').Append(a.Aim.x.ToString("0", inv)).Append(',').Append(a.Aim.y.ToString("0", inv)).Append(',')
                        .Append(flags).Append(',').Append(a.LineFrom).Append(',').Append(a.LineTo).Append(',').Append(a.Index).Append(',').Append(a.Replans);
                }
                else row.Append("0,,,,,,,,,,,");
                trace.WriteLine(row.ToString());
            }
        }

        /// <summary>RACE-017 dev: what each AI-flown racer is doing.</summary>
        internal string AiInfo()
        {
            if (!IsOpen) return "no race open";
            var em = host.Context.EntityManager; var text = new System.Text.StringBuilder();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<RacerPilot>(), ComponentType.ReadOnly<Pilot>(), ComponentType.ReadOnly<LocalTransform>());
            using var ships = q.ToEntityArray(Allocator.Temp);
            foreach (var s in ships)
            {
                var p = em.GetComponentData<RacerPilot>(s); var pose = em.GetComponentData<LocalTransform>(s);
                float speed = em.HasComponent<Unity.Physics.PhysicsVelocity>(s) ? math.length(em.GetComponentData<Unity.Physics.PhysicsVelocity>(s).Linear.xz) : 0;
                float mass = em.HasComponent<Unity.Physics.PhysicsMass>(s) && em.GetComponentData<Unity.Physics.PhysicsMass>(s).InverseMass > 0 ? 1 / em.GetComponentData<Unity.Physics.PhysicsMass>(s).InverseMass : 0;
                byte slot = em.GetComponentData<Pilot>(s).Slot;
                if (em.HasComponent<Game.Simulation.Flight.PropulsionStats>(s) && em.HasComponent<Game.Simulation.Flight.FlightState>(s))
                {
                    var ps = em.GetComponentData<Game.Simulation.Flight.PropulsionStats>(s); var fs = em.GetComponentData<Game.Simulation.Flight.FlightState>(s);
                    text.Append($"[accel fwd {ps.ForwardAccel:0.0} rev {ps.ReverseAccel:0.0} strafe {ps.StrafeAccel:0.0} turn {ps.TurnRateDeg:0}°/s caps fwd {fs.ForwardCap:0} strafe {fs.StrafeCap:0} yaw {math.degrees(fs.YawRateCap):0}°/s carry {fs.Carry.x:0},{fs.Carry.y:0}] ");
                }
                text.Append($"{PilotLabel(slot)} (slot {slot}): x {pose.Position.x:0} z {pose.Position.z:0} speed {speed:0} target {p.TargetSpeed:0} threat {p.Threat:0.0} off {p.Off:0} wide {p.Wide:0} cmd {(em.HasComponent<ShipCommand>(s) ? em.GetComponentData<ShipCommand>(s).Flags.ToString() : "")} lane {p.Lane:0.00} steer {math.degrees(p.Steer):0} line {p.Index}/{em.GetBuffer<RacerLinePoint>(s).Length} at {LinePoint(s, p.Index)} from gate {p.LineFrom} mass {mass:0} skill {p.Skill:0.00}\n");
            }
            return text.Length == 0 ? "no AI racers" : text.ToString();
        }

        // ------------------------------------------------------------------------------------------------ visuals
        // RACE-016: TrackDressing builds the look (lanes, fences, gates, pads, boards, grid, wreck lights, dust); this feeds it the
        // pilot's next gates, the mines and the guide arrow each frame.
        void BuildVisuals(TrackDefinition track)
        {
            dressing?.Dispose();
            dressing = new Game.Presentation.Racing.TrackDressing(track, host.transform);
            gatePositions = new Vector3[track.Gates.Length];
            for (int i = 0; i < track.Gates.Length; i++) gatePositions[i] = new Vector3(track.Gates[i].Position.x, 0, track.Gates[i].Position.y);
        }
        Vector3[] gatePositions = Array.Empty<Vector3>();
        readonly List<(int, byte)> spansDown = new();
        void UpdateVisuals()
        {
            if (dressing == null || !host.Context.TryGetFrame(out var frame)) return;
            Vector3 origin = (float3)(trackOrigin - frame.OriginInStarSystem);
            dressing.Root.transform.position = origin;
            var em = host.Context.EntityManager;
            ulong next = 1; Entity ship = Entity.Null;
            byte me = LocalSlot;
            foreach (var r in em.GetBuffer<RacerProgress>(race))
                if (r.Slot == me)
                {
                    next = r.LastGate >= 0 ? em.GetBuffer<RaceGate>(race)[r.LastGate].NextMask : 1;
                    if ((r.Flags & RacerFlags.Finished) != 0) next = 0;
                    ship = r.Ship;
                }
            dressing.SetNextGates(next);
            // guide: toward the nearest of the next gates, while racing
            bool racing = State.Phase == RacePhase.Racing && next != 0;
            if (ship == Entity.Null || !em.Exists(ship)) ship = host.Context.PlayerShipRoot;
            Vector3 at = ship != Entity.Null && em.Exists(ship) && em.HasComponent<LocalTransform>(ship) ? (Vector3)em.GetComponentData<LocalTransform>(ship).Position : Vector3.zero;
            Vector3 target = at; float best = float.MaxValue;
            for (int i = 0; i < gatePositions.Length && i < 64; i++)
            {
                if ((next & (1UL << i)) == 0) continue;
                Vector3 g = origin + gatePositions[i]; float d = (g - at).sqrMagnitude;
                if (d < best) { best = d; target = g; }
            }
            dressing.SetGuide(false, at, target);   // owner: the guide arrow is unnecessary
            // RACE-016: spans whose emitter post was shot away go dark
            spansDown.Clear();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<WallSpan>(), ComponentType.ReadOnly<ZoneOff>()))
            using (var spans = q.ToComponentDataArray<WallSpan>(Allocator.Temp))
                foreach (var w in spans) spansDown.Add((w.Index, w.Lost));
            dressing.SetSpans(spansDown);
            UpdateMines();
        }

        // HAZ-008: each mine's halo (its trigger radius) and light: grey while arming, pulsing red armed, flashing when triggered.
        void UpdateMines()
        {
            var em = host.Context.EntityManager;
            mineLooks.Clear();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Voxel.Model.VoxelGrid>(), ComponentType.ReadOnly<LocalTransform>());
            using var grids = q.ToEntityArray(Allocator.Temp);
            foreach (var e in grids)
            {
                if (em.GetComponentData<Game.Voxel.Model.VoxelGrid>(e).Kind != Game.Voxel.Model.GridKind.Device) continue;
                var state = Game.Presentation.Racing.TrackDressing.MineLook.Armed;
                if (em.HasComponent<MineState>(e))
                    switch (em.GetComponentData<MineState>(e).Phase)
                    {
                        case MinePhase.Arming: state = Game.Presentation.Racing.TrackDressing.MineLook.Arming; break;
                        case MinePhase.Triggered: state = Game.Presentation.Racing.TrackDressing.MineLook.Triggered; break;
                        case MinePhase.Spent: continue;
                    }
                float radius = em.HasComponent<MineFuse>(e) ? em.GetComponentData<MineFuse>(e).Params.TriggerRadius : 16;
                mineLooks.Add((em.GetComponentData<LocalTransform>(e).Position, radius, state));
            }
            dressing.SetMines(mineLooks, UnityEngine.Time.time);
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
            if (aiNames.TryGetValue(slot, out var ai)) return ai;
            return slot == LocalSlot ? "YOU" : "PILOT " + (slot + 1);
        }

        public void Dispose() => End();
    }
}
#endif
