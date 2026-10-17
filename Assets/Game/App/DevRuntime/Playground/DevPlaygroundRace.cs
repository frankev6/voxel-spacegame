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
        double3 trackOrigin;
        VisualElement hud; Label banner, status, warning, results;

        internal DevPlaygroundRace(DevRuntimeHost host) { this.host = host; }
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
            trackOrigin = padsOrigin + TrackOffset;
            float2 local = (float2)(trackOrigin - frame.OriginInStarSystem).xz;
            race = TrackSpawnService.Spawn(em, track, local, Group, session);
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
            }
            slots.Dispose();
            var buffer = em.GetBuffer<RacerProgress>(race);
            foreach (var r in racers) buffer.Add(r);
            BuildVisuals(track); BuildHud();
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
                    for (int i = 0; i < racers.Length; i++)
                        if (racers[i].Slot == request.Slot && racers[i].Ship != Entity.Null && em.Exists(racers[i].Ship) && !em.HasComponent<Hulk>(racers[i].Ship))
                            DevEcs.TeleportRoot(em, racers[i].Ship, new float3(request.Position.x, 0, request.Position.y), quaternion.RotateY(request.Yaw));
                }
            UpdateVisuals(); UpdateHud();
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
            foreach (var r in em.GetBuffer<RacerProgress>(race))
                if (r.Ship == host.Context.PlayerShipRoot) next = r.LastGate >= 0 ? em.GetBuffer<RaceGate>(race)[r.LastGate].NextMask : 1;
            for (int i = 0; i < gateLines.Count; i++) gateLines[i].widthMultiplier = (next & (1UL << i)) != 0 ? 9 : 3;
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
            foreach (var r in racers) if (r.Ship == host.Context.PlayerShipRoot) { me = r; found = true; }
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
                    text.Append($"P{r.Place}   PILOT {r.Slot + 1}   {((r.Flags & RacerFlags.Finished) != 0 ? Time(r.FinishTick - rs.StartTick) : "DNF")}   BEST {Time(r.BestLapTicks)}\n");
                results.text = text.ToString(); results.style.display = DisplayStyle.Flex;
            }
            else results.style.display = DisplayStyle.None;
        }

        public void Dispose() => End();
    }
}
#endif
