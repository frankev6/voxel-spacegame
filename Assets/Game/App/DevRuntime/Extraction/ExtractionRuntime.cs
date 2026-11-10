#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Galaxy.Components;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Extraction;
using Game.Simulation.Hazards;
using Game.UI.Data;
using Game.UI.Services;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Extraction
{
    /// <summary>SPEC-93 EXT-009: the extraction loop in the playground's system (ADR-0124 decision 4). The Rock (the den) and Tollhouse
    /// Nine (the heist) sit 20 km apart, joined by a pair of one-way lane gates. At the den's Table the crew takes a job from the board;
    /// out through the gate to the Lantern; the job; back through the Lantern's gate (the cargo rule: the tow or the hold); the core into
    /// the socket. Keys while flying: U the magnet (attach the nearest loose body / let go), O the transponder (off, trader, work if you
    /// hold it), F2 cut the nearest tether, F3 stow or unstow, F4 dig, F6 build, F7 the next thing to build. F at a place (the Table, Cloth
    /// Row, the Long Wait, Winch Square, the pier, the hauler, the engineers' room) does what that place does.</summary>
    internal sealed class ExtractionRuntime : IDisposable
    {
        internal static readonly float2 LanternOffset = new float2(9000, -7000), DenOffset = new float2(-6500, 5500);   // from the playground's origin (m)
        /// <summary>Place-local: the den's outbound gate and where it lands by the Lantern; the Lantern's gate home and where it lands by the den.</summary>
        internal static readonly float2 DenGateLocal = new float2(420, -40), LanternArrivalLocal = new float2(0, -820), LanternGateLocal = new float2(720, 160), DenArrivalLocal = new float2(760, 40);
        internal const ushort GateGroup = 71;
        internal enum JobState : byte { None, Accepted, OnSite, CoreTaken, Escaped, Home, Installed }
        [StaticStateOk("constant job table")]
        internal static readonly (string Id, string Title, string Brief)[] Jobs =
        {
            ("dimming", "The Dimming", "Lantern Night: buy sailcloth at Cloth Row (F), and in the minute of dark cut the tethers and tow the core up the Well."),
            ("trojan", "The Trojan", "Book a freight berth at the pier (F): a hauler waits at the yard apron. Into the Powerhouse by the dock lift, cut, out the big door, the core into the hauler's hold (F)."),
            ("depot", "The Depot Fire", "Blow the fuel depot (north-east) to draw the cutters, blow the Cradle's floor hatch from below, cut, out down the trunk and the Wound."),
            ("pod", "Pod Seven", "Take a yard job in the engineers' room (F): the big door opens for the crane-tug. Cut, tow the core into the mass driver's breech, and it fires."),
            ("scrap", "The Scrap Run", "Winch Square (F): the fixer forges a work order and a scrap housing. Your work code opens the big door; the housing hides the core."),
            ("inside", "The Inside Man", "The Long Wait (F): clear the night engineer's debt. He opens the Cradle's floor hatch and holds the outfall flush."),
        };

        readonly DevContext ctx;
        readonly ExtractionKit kit;
        internal readonly LanternPlace Lantern;
        internal readonly DenPlace Den;
        readonly double3 playground;
        readonly UIRoot ui;
        Label hud;
        bool gates;
        readonly List<Entity> gateZones = new();
        internal JobState State;
        internal string Job;
        internal bool HoldsWork;
        internal Action<string> Report = _ => { };
        double installedAt = -1;
        uint refusedTick;

        internal ExtractionRuntime(DevContext ctx, double3 playgroundOrigin, UIRoot ui)
        {
            this.ctx = ctx; this.ui = ui; playground = playgroundOrigin; kit = new ExtractionKit(ctx);
            Lantern = new LanternPlace(kit, playground + new double3(LanternOffset.x, 0, LanternOffset.y)) { Toast = Say };
            Den = new DenPlace(kit, playground + new double3(DenOffset.x, 0, DenOffset.y)) { Toast = Say };
        }

        void Say(string text) { Report(text); }
        internal string Missing => kit.Missing.Count == 0 ? null : "Extraction content missing (" + string.Join(", ", kit.Missing) + "): run tools/build-extraction.sh (Game/Content/Build Lantern and Den).";

        internal void Build()
        {
            Den.Build(); Lantern.Build(); Gates(); Crew();
            if (hud == null && ui != null)
            {
                hud = new Label("") { name = "extraction-hud", pickingMode = PickingMode.Ignore };
                hud.AddToClassList("text-s");
                hud.style.position = Position.Absolute; hud.style.left = new Length(50, LengthUnit.Percent); hud.style.top = 64;
                hud.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
                hud.style.color = new Color(1f, .9f, .7f); hud.style.unityTextAlign = TextAnchor.UpperCenter;
                ui.GetLayer(UILayer.Hud).Add(hud);
            }
        }

        /// <summary>The gate pair: out from the den to the Lantern's toll approach, home from beyond the Lantern's Yards to the den's canyon.</summary>
        void Gates()
        {
            if (gates || !kit.Ready(out var session, out var catalog, out var frame)) return;
            if (!catalog.TryGet<ZoneDefinition>(new ContentId("zone_gate"), out var gate)) { kit.Missing.Add("zone_gate"); return; }
            var em = ctx.EntityManager;
            float2 Xz(DenPlace d, float2 p) => ExtractionKit.Sim(d.Anchor, new float3(p.x, 0, p.y), frame).xz;
            float2 Lz(float2 p) => ExtractionKit.Sim(Lantern.Anchor, new float3(p.x, 0, p.y), frame).xz;
            gateZones.Add(HazardSpawn.Portal(em, gate, Xz(Den, DenGateLocal), math.PI / 2, Lz(LanternArrivalLocal), 0, 360, 4, GateGroup, session.Allocate(), session.Allocate()));
            gateZones.Add(HazardSpawn.Portal(em, gate, Lz(LanternGateLocal), math.PI / 2, Xz(Den, DenArrivalLocal), -math.PI / 2, 520, 4, GateGroup, session.Allocate(), session.Allocate()));
            gates = true;
        }

        /// <summary>A test lever (`extraction hold t=`): the hold's rating in tonnes instead of the ship's cargo blocks; negative = none.</summary>
        internal float HoldOverride = -1;

        /// <summary>The crew's ship: a transponder (on, trader) and a hold rated from its cargo blocks (or the test override).</summary>
        internal void Crew()
        {
            var em = ctx.EntityManager; var ship = ctx.PlayerShipRoot;
            if (ship == Entity.Null || !em.Exists(ship)) return;
            if (!em.HasComponent<Transponder>(ship)) TransponderApi.Set(em, ship, true, AccessCode.Trader);
            float t = HoldOverride >= 0 ? HoldOverride : HoldTonnes();
            var hold = em.HasComponent<CargoHold>(ship) ? em.GetComponentData<CargoHold>(ship) : default;
            hold.CapacityT = t;
            if (em.HasComponent<CargoHold>(ship)) em.SetComponentData(ship, hold); else em.AddComponentData(ship, hold);
        }

        float HoldTonnes()
        {
            if (!ctx.TryGetService<FleetModel>(out var fleet) || !ctx.TryGetService<ContentService>(out var content)) return 0;
            var structure = fleet.Flagship?.Structure;
            if (structure == null || !structure.Data.IsCreated) return 0;
            double units = 0;
            foreach (var instance in structure.Data.Instances)
            {
                if (instance.Block == 0) continue;
                var def = content.Catalog.GetBlock(instance.Block);
                if (def != null && def.TryGetModule(out CargoModule cargo)) units += cargo.Params.Capacity;
            }
            return (float)(units * CargoRule.TonnesPerCargoUnit);
        }

        // ---------------------------------------------------------------------------------------------------------- per frame
        internal void Tick(bool located, double3 ship)
        {
            if (!ctx.HasGameplay) return;
            double now = Time.timeAsDouble;
            if (located)
            {
                if (!Den.Built && math.distance(ship.xz, Den.Anchor.xz) < 3000) { Den.Build(); Gates(); Crew(); }
                if (!Lantern.Built && math.distance(ship.xz, Lantern.Anchor.xz) < 3500) { Lantern.Build(); Gates(); Crew(); }
            }
            Lantern.Tick(now); Den.Tick(now);
            var em = ctx.EntityManager;
            var player = ctx.PlayerShipRoot;
            if (player != Entity.Null && em.Exists(player) && !em.HasComponent<Transponder>(player)) Crew();
            Keys(em, player);
            Loop(em, located, ship, now);
            Hud(em, player);
            if (player != Entity.Null && em.Exists(player) && em.HasComponent<CargoRefused>(player))
            {
                var r = em.GetComponentData<CargoRefused>(player);
                if (r.Tick != refusedTick) { refusedTick = r.Tick; Say($"The gate refuses you: {r.LoadT:0} t on the cable, the hold takes {r.FreeT:0} t. Stow it in a hold that can (F3), or find a hauler."); }
            }
        }

        void Keys(EntityManager em, Entity player)
        {
            var kb = Keyboard.current;
            if (kb == null || player == Entity.Null || !em.Exists(player)) return;
            if (ctx.TryGetService<Game.Input.InputService>(out var input) && input.CurrentContext != Game.Core.InputContext.Flight) return;
            if (kb.uKey.wasPressedThisFrame) Say(Magnet(em, player));
            if (kb.oKey.wasPressedThisFrame) Say(CycleTransponder(em, player));
            if (kb.f2Key.wasPressedThisFrame) Say(Cut(em, player));
            if (kb.f3Key.wasPressedThisFrame) Say(StowToggle(em, player));
            if (kb.f4Key.wasPressedThisFrame) Say(Den.Dig());
            if (kb.f6Key.wasPressedThisFrame) Say(Den.Place());
            if (kb.f7Key.wasPressedThisFrame) Den.Cycle();
        }

        internal string Magnet(EntityManager em, Entity ship)
        {
            em.CompleteAllTrackedJobs();
            if (TowApi.IsTowing(em, ship)) { TowApi.Release(em, ship); return "Magnet off: let go."; }
            var load = Game.App.DevRuntime.Commands.TowDevCommand.Nearest(em, ship, 90);
            if (load == Entity.Null) return "Magnet: nothing loose within 90 m.";
            float d = math.distance(em.GetComponentData<LocalTransform>(ship).Position, em.GetComponentData<LocalTransform>(load).Position);
            if (!TowApi.Attach(em, ship, load, math.max(30, d + 4), kit.Tick)) return "Magnet: it will not take that.";
            if (load == Lantern.Core && Lantern.Shroud && !em.HasComponent<Shrouded>(load)) { em.AddComponent<Shrouded>(load); return "Magnet on the core. The shroud is over it: its light is hidden."; }
            return load == Lantern.Core ? "Magnet on the core." : "Magnet on.";
        }

        internal string CycleTransponder(EntityManager em, Entity ship)
        {
            if (!em.HasComponent<Transponder>(ship)) TransponderApi.Set(em, ship, true, AccessCode.Trader);
            var t = em.GetComponentData<Transponder>(ship);
            if (t.On == 0) TransponderApi.Set(em, ship, true, AccessCode.Trader);
            else if (t.Code == AccessCode.Trader && HoldsWork) TransponderApi.Set(em, ship, true, AccessCode.Work);
            else TransponderApi.Set(em, ship, false, AccessCode.None);
            t = em.GetComponentData<Transponder>(ship);
            return t.On == 0 ? "Transponder OFF: you are dark. Anyone who sees you will remember." : "Transponder on: " + t.Code.ToString().ToUpperInvariant();
        }

        internal string Cut(EntityManager em, Entity ship)
        {
            if (!Lantern.Built) return "No tethers here.";
            var at = em.GetComponentData<LocalTransform>(ship).Position;
            var tether = Lantern.NearestTether(em, at, 45);
            if (tether == Entity.Null) return "Cut: fly within 45 m of a tether post in the Cradle.";
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<TetherCutRequests>());
            if (q.CalculateEntityCount() != 1) return "no tether system";
            em.CompleteDependencyBeforeRW<TetherCutRequests>();
            q.GetSingleton<TetherCutRequests>().Pending.Add(tether);
            var t = em.GetComponentData<CoreTether>(tether);
            string name = "a district"; foreach (var x in LanternLayout.Tethers) if (x.District == t.District) name = x.Name;
            return "Cut: " + name + " goes dark.";
        }

        internal string StowToggle(EntityManager em, Entity ship)
        {
            if (TowApi.IsTowing(em, ship))
            {
                // beside the Trojan's hauler, the core goes into its hold
                if (Lantern.Hauler.IsNone == false && kit.Root(Lantern.Hauler, out var hauler)
                    && math.distance(em.GetComponentData<LocalTransform>(hauler).Position, em.GetComponentData<LocalTransform>(ship).Position) < 90)
                {
                    if (!StowApi.StowInto(em, ship, hauler, out var why)) return "The hauler: " + why;
                    Lantern.HaulerLeaving = true;
                    return "The core is in the hauler's hold. It leaves with the crowd, transponder on, manifest clean.";
                }
                return StowApi.Stow(em, ship, out var reason) ? "Stowed in your hold." : "Stow: " + reason;
            }
            // unstow the last thing stowed in this ship
            using var q = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Stowed>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            using var all = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var e in all) if (em.GetComponentData<Stowed>(e).Ship == ship) return StowApi.Unstow(em, ship, e, 45) ? "Unstowed behind you." : "Unstow failed.";
            return "Nothing on the cable and nothing in the hold.";
        }

        // ---------------------------------------------------------------------------------------------------------- the loop
        void Loop(EntityManager em, bool located, double3 ship, double now)
        {
            var core = Lantern.Core != Entity.Null && em.Exists(Lantern.Core) && em.HasComponent<StationCore>(Lantern.Core) ? em.GetComponentData<StationCore>(Lantern.Core) : default;
            if (State == JobState.Accepted && located && math.distance(ship.xz, Lantern.Anchor.xz) < 1500) { State = JobState.OnSite; Say("Tollhouse Nine. The toll queue is ahead; the arch scans everyone. Job: " + Title(Job)); }
            if ((State == JobState.OnSite || State == JobState.Accepted) && core.Freed != 0) State = JobState.CoreTaken;
            if (State == JobState.CoreTaken && Lantern.Heist != Entity.Null && em.GetComponentData<HeistState>(Lantern.Heist).Outcome == HeistOutcome.Escaped) State = JobState.Escaped;
            if (State is JobState.CoreTaken or JobState.Escaped && Lantern.Core != Entity.Null && em.Exists(Lantern.Core))
            {
                double3 at = CoreUniverse(em);
                if (math.distance(at.xz, Den.Anchor.xz) < 1200) { State = JobState.Home; Say("Home. Bring the core into the socket at the back of the cave."); }
            }
            if (State != JobState.Installed && core.Socket != Entity.Null)
            {
                State = JobState.Installed; installedAt = now;
                Say("The core is in the socket. The Rock has power: guns, doors, lamps. Build round it (F6), and get ready: they will come for it.");
                Den.ScheduleAttack(now, 150);
            }
            // the Trojan's hauler: from the yard apron through the Lantern's gate home, then to the cave, where it gives up the core
            Hauler(em);
        }

        double3 CoreUniverse(EntityManager em)
        {
            var e = Lantern.Core;
            if (em.HasComponent<Stowed>(e)) e = em.GetComponentData<Stowed>(e).Ship;
            if (!em.Exists(e) || !kit.Ready(out _, out _, out var frame)) return default;
            return (double3)em.GetComponentData<LocalTransform>(e).Position + frame.OriginInStarSystem;
        }

        void Hauler(EntityManager em)
        {
            if (Lantern.Hauler.IsNone || !kit.Root(Lantern.Hauler, out var root) || !em.HasComponent<AiBrain>(root) || !kit.Ready(out _, out _, out var frame)) return;
            var brain = em.GetComponentData<AiBrain>(root);
            if (brain.Initialized == 0) return;
            double3 at = (double3)em.GetComponentData<LocalTransform>(root).Position + frame.OriginInStarSystem;
            bool home = math.distance(at.xz, Den.Anchor.xz) < 4000;
            float3 goal;
            if (!Lantern.HaulerLeaving) goal = Lantern.Sim(LanternLayout.Poi("yard_apron") + new float3(40, 0, 60));
            else if (!home) goal = Lantern.Sim(new float3(LanternGateLocal.x + 300, LanternLayout.Poi("yard_apron").y, LanternGateLocal.y));
            else goal = Den.Sim(DenPlace.CaveMouth + new float3(60, 0, 0));
            brain.Anchor = goal; brain.GoalPosition = goal; em.SetComponentData(root, brain);
            if (home && Lantern.HaulerLeaving && math.distance(em.GetComponentData<LocalTransform>(root).Position, Den.Sim(DenPlace.CaveMouth)) < 160)
            {
                using var q = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Stowed>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
                using var all = q.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in all)
                    if (em.GetComponentData<Stowed>(e).Ship == root && StowApi.Unstow(em, root, e, 50))
                    {
                        var p = em.GetComponentData<LocalTransform>(e); p.Position = Den.Sim(DenPlace.SocketStart + new float3(12, 0, 0)); em.SetComponentData(e, p);
                        Lantern.HaulerLeaving = false;
                        Say("The hauler is home and sets the core down in the cave, by the socket.");
                    }
            }
        }

        internal string Accept(string id)
        {
            foreach (var j in Jobs)
            {
                if (j.Id != id) continue;
                Job = id; State = JobState.Accepted;
                if (id == "dimming") Lantern.ScheduleDimming(Time.timeAsDouble + 120);
                return $"Job taken: {j.Title}. {j.Brief} Out through the den's gate, east of the cave.";
            }
            return "job accept=" + string.Join("|", Array.ConvertAll(Jobs, j => j.Id));
        }

        static string Title(string id) { foreach (var j in Jobs) if (j.Id == id) return j.Title; return "none"; }

        internal string Board()
        {
            var text = new System.Text.StringBuilder("THE BOARD (the Table at the Rock). job accept=<id>:\n");
            foreach (var j in Jobs) text.Append("  ").Append(j.Id).Append(": ").Append(j.Title).Append(". ").Append(j.Brief).Append('\n');
            return text.ToString();
        }

        /// <summary>F at a place. Null when the ship is at none of them.</summary>
        internal string Interact()
        {
            var em = ctx.EntityManager; var ship = ctx.PlayerShipRoot;
            if (ship == Entity.Null || !em.Exists(ship)) return null;
            float3 at = em.GetComponentData<LocalTransform>(ship).Position;
            bool Near(float3 local, float r) => Lantern.Built && math.distance(at, Lantern.Sim(local)) < r;
            if (Den.Built && math.distance(at, Den.Sim(DenPlace.Table)) < 60) return Board();
            if (!Lantern.Built) return null;
            if (Near(LanternLayout.Poi("cloth_row"), 70)) { Lantern.Shroud = true; return "Cloth Row: forty metres of black sailcloth, stitched into a shroud. Throw it over the core when you take it (it goes on with the magnet)."; }
            if (Near(LanternLayout.Poi("long_wait"), 80)) { Lantern.InsideMan = true; Lantern.OpenHatch(); return "The Long Wait: the night engineer's debt is paid. On his shift he logs tether maintenance, holds the outfall flush and opens the Cradle's floor hatch."; }
            if (Near(LanternLayout.Poi("winch_square"), 80)) { HoldsWork = true; Lantern.Shroud = true; return "Winch Square: the fixer's work order (\"replace cracked reactor housing, expect brownouts\") and a scrap housing. You hold a WORK code now (O). At the big door, F: the scheduled part goes in."; }
            if (Near(LanternLayout.Poi("big_door_out"), 90) && HoldsWork) { Lantern.OpenBigDoor(); return "The big door opens for a scheduled part."; }
            if (Near(LanternLayout.Poi("engineers"), 70)) { HoldsWork = true; Lantern.OpenBigDoor(); return "The engineers' room is empty at 03:00. You request a heavy lift: the big door opens for the yard's crane-tug, which tonight is you. WORK code held (O)."; }
            if (Near(LanternLayout.Poi("pier"), 110)) return Lantern.BookHauler();
            return null;
        }

        void Hud(EntityManager em, Entity player)
        {
            if (hud == null) return;
            bool show = Lantern.Built || Den.Built;
            hud.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show || player == Entity.Null || !em.Exists(player)) return;
            var parts = new List<string>();
            if (em.HasComponent<Transponder>(player))
            {
                var t = em.GetComponentData<Transponder>(player);
                parts.Add(t.On == 0 ? "DARK" : "XPDR " + t.Code.ToString().ToUpperInvariant());
            }
            if (em.HasComponent<Suspicion>(player))
            {
                var s = em.GetComponentData<Suspicion>(player);
                int n = (int)math.round(s.Value * 10);
                parts.Add((s.Seen != 0 ? "SEEN " : s.Heard != 0 ? "HEARD " : "") + new string('|', n) + new string('.', 10 - n));
            }
            if (Lantern.Built && TransponderApi.IsWantedBy(em, player, Lantern.AllianceIndex)) parts.Add("WANTED");
            if (TowApi.IsTowing(em, player))
            {
                var link = em.GetComponentData<TowLink>(player);
                var load = em.HasComponent<TowLoad>(link.Load) ? em.GetComponentData<TowLoad>(link.Load) : default;
                parts.Add($"CABLE {load.MassT:0} t {(link.Tension > 0 ? "TAUT" : "SLACK")}");
            }
            if (Lantern.Heist != Entity.Null && em.Exists(Lantern.Heist))
            {
                var h = em.GetComponentData<HeistState>(Lantern.Heist);
                if (h.Phase == HeistPhase.Alarm || h.Phase == HeistPhase.Lockdown)
                {
                    float sec = (kit.Tick - h.AlarmTick) * SimTime.FixedTimestep;
                    parts.Add($"ALARM {(int)sec / 60}:{(int)sec % 60:00}");
                }
                else if (h.Phase == HeistPhase.Over) parts.Add(h.Outcome.ToString().ToUpperInvariant());
            }
            if (Den.Built && math.distance(em.GetComponentData<LocalTransform>(player).Position, Den.Sim(DenPlace.CaveCentre)) < 900)
                parts.Add($"ORE {Den.Ore} · BUILD {Den.KindName.ToUpperInvariant()} (F6, F7)");
            if (State != JobState.None) parts.Add("JOB " + Title(Job).ToUpperInvariant() + " · " + State.ToString().ToUpperInvariant());
            hud.text = string.Join("   ", parts);
        }

        internal string Status(EntityManager em)
        {
            var ship = ctx.PlayerShipRoot;
            string crew = "";
            if (ship != Entity.Null && em.Exists(ship))
            {
                var t = em.HasComponent<Transponder>(ship) ? em.GetComponentData<Transponder>(ship) : default;
                var s = em.HasComponent<Suspicion>(ship) ? em.GetComponentData<Suspicion>(ship) : default;
                var h = em.HasComponent<CargoHold>(ship) ? em.GetComponentData<CargoHold>(ship) : default;
                crew = $" transponder={(t.On == 0 ? "off" : t.Code.ToString())} suspicion={s.Value:0.00} seen={s.Seen} heard={s.Heard} wanted={(em.HasComponent<Wanted>(ship) ? em.GetComponentData<Wanted>(ship).FactionMask : 0)} holdT={h.CapacityT:0}/{h.StowedT:0} towing={TowApi.IsTowing(em, ship)} work={HoldsWork}";
            }
            return $"job={Job ?? "none"} state={State}{crew} | {Lantern.Status(em)} | {Den.Status(em)}";
        }

        internal double3 UniverseOf(string place, float3 local) => (place == "den" ? Den.Anchor : Lantern.Anchor) + (double3)local;

        public void Dispose()
        {
            Lantern.Dispose(); Den.Dispose();
            if (ctx.HasGameplay) foreach (var z in gateZones) if (ctx.EntityManager.Exists(z)) HazardSpawn.DespawnGroup(ctx.EntityManager, GateGroup);
            gateZones.Clear(); gates = false;
            hud?.RemoveFromHierarchy(); hud = null;
        }
    }
}
#endif
