#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-022: the layout of the ARK Proving Grounds, the playground's space hub. A central plaza with the hub station, a
    /// ring of lane-gate portals (one per district) and the spawn; nine districts round it, each themed on one group of mechanics, with
    /// its own entrance, title, signs, stations (F pads) and a reset pad; a return portal in each district leads back to the plaza.
    /// Everything here is data relative to the playground origin (metres on the flight plane; +z north). A district's own layout is
    /// written in its local frame: x to the right and z forward for a pilot arriving from the plaza.</summary>
    internal static class PlaygroundHub
    {
        internal enum StationKind : byte { Activate, ResetDistrict, ResetAll, FlyShip }

        internal sealed class District
        {
            internal readonly string Id, Title, Tagline;
            internal readonly Color Colour;
            internal readonly float2 Centre, Forward, Right;
            internal readonly float Radius, Yaw, PortalBearing;
            internal District(string id, string title, string tagline, Color colour, float2 centre, float radius, float portalBearingDeg)
            {
                Id = id; Title = title; Tagline = tagline; Colour = colour; Centre = centre; Radius = radius;
                Forward = math.normalize(centre - Plaza); Right = new float2(Forward.y, -Forward.x);
                Yaw = math.atan2(Forward.x, Forward.y); PortalBearing = math.radians(portalBearingDeg);
            }
            /// <summary>A point given in the district's frame (x right, z forward), relative to the playground origin.</summary>
            internal float2 At(float x, float z) => Centre + Right * x + Forward * z;
            internal float YawOf(float localYaw) => Yaw + localYaw;
            /// <summary>Where the plaza portal lands a pilot, just inside the district's entrance, facing in.</summary>
            internal float2 Arrival => At(0, -(Radius - 40));
            internal float2 ReturnPortal => At(-120, -(Radius - 70));
            internal float2 PlazaPortal => Plaza + new float2(math.sin(PortalBearing), math.cos(PortalBearing)) * PortalRing;
            /// <summary>The return portal's exit: outside the portal ring, between two entry portals, heading in to the plaza.</summary>
            internal float2 PlazaReturn => Plaza + new float2(math.sin(PortalBearing + ReturnOffset), math.cos(PortalBearing + ReturnOffset)) * ReturnRing;
        }

        internal sealed class Station
        {
            internal readonly string Id, Title, Detail;
            internal readonly District District;
            internal readonly float2 Position;
            internal readonly float Yaw;          // the way a pilot faces to read its label
            internal readonly StationKind Kind;
            internal readonly string Ship;        // FlyShip: the blueprint ("starter" for the start ship)
            internal Station(string id, District district, float2 position, float yaw, string title, string detail, StationKind kind = StationKind.Activate, string ship = null)
            { Id = id; District = district; Position = position; Yaw = yaw; Title = title; Detail = detail; Kind = kind; Ship = ship; }
        }

        /// <summary>A floor sign (dot-matrix text on the flight plane), read by a pilot heading `Yaw`.</summary>
        internal readonly struct Sign
        {
            internal readonly string Text; internal readonly float2 Position; internal readonly float Yaw, Pixel; internal readonly Color Colour;
            internal readonly HoloAlign Align;
            internal Sign(string text, float2 position, float yaw, float pixel, Color colour, HoloAlign align = HoloAlign.Centre)
            { Text = text; Position = position; Yaw = yaw; Pixel = pixel; Colour = colour; Align = align; }
        }
        internal enum HoloAlign : byte { Left, Centre, Right }

        /// <summary>A static structure placed by the hub (a voxel blueprint).</summary>
        internal readonly struct Piece
        {
            internal readonly string Blueprint; internal readonly float2 Position; internal readonly float Yaw; internal readonly District District;
            internal Piece(string blueprint, float2 position, float yaw, District district = null) { Blueprint = blueprint; Position = position; Yaw = yaw; District = district; }
        }

        // ---------------------------------------------------------------------------------------------------- the plaza
        internal static readonly float2 Plaza = new float2(0, 860);
        internal const float PortalRing = 300, ReturnRing = 400, PortalWidth = 56, StationRadius = 18, CoreRadius = 94;
        internal static readonly float ReturnOffset = math.radians(13);
        internal static float2 Spawn => Plaza + new float2(0, -175);
        internal const float SpawnYaw = 0;
        internal const float InnerLoop = 460, OuterLoop = 1260;

        static readonly Color Cyan = new Color(.35f, .85f, 1f), Red = new Color(1f, .32f, .26f), Orange = new Color(1f, .52f, .18f),
            Amber = new Color(1f, .76f, .22f), Violet = new Color(.72f, .48f, 1f), White = new Color(.82f, .9f, 1f),
            Gold = new Color(1f, .84f, .4f), Green = new Color(.45f, 1f, .55f), Checker = new Color(1f, .92f, .7f);
        internal static readonly Color PlazaColour = new Color(.55f, .8f, 1f), ResetColour = new Color(1f, .25f, .2f);

        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Flight = new("flight", "FLIGHT SCHOOL", "THRUST · STRAFE · BRAKE · BOOST", Cyan, Ring(135, 900), 280, 135);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Weapons = new("weapons", "WEAPONS RANGE", "BREAK IT APART", Red, new float2(0, 80), 250, 165);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Combat = new("combat", "COMBAT ARENA", "LIVE AI THAT SHOOTS BACK", Orange, Ring(225, 900), 300, 225);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Hazards = new("hazards", "HAZARD YARD", "PULSES · CANISTERS · MINES", Amber, Ring(270, 900), 300, 270);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Mechanisms = new("mechanisms", "MECHANISM WORKS", "ARMS · DOORS · SWITCHES · PORTALS", Violet, Ring(315, 900), 300, 315);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Shipyard = new("shipyard", "SHIPYARD", "PICK A SHIP AND FLY IT", White, Ring(0, 900), 310, 0);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Market = new("market", "MARKET DOCKS", "TRADE WITH A MERCHANT", Gold, Ring(45, 900), 260, 45);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Salvage = new("salvage", "SALVAGE YARD", "WRECKS · SCRAP · CARGO", Green, Ring(90, 900), 280, 90);
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District Race = new("race", "RACE CONTROL", "TRACK 01 · BATTLE GRAVEYARD", Checker, new float2(0, -640), 180, 195);

        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly District[] Districts = { Flight, Weapons, Combat, Hazards, Mechanisms, Shipyard, Market, Salvage, Race };

        static float2 Ring(float bearingDeg, float distance)
        {
            float a = math.radians(bearingDeg);
            return Plaza + new float2(math.sin(a), math.cos(a)) * distance;
        }

        /// <summary>The district a point lies in (within its radius plus a margin), or null on the plaza and the lanes.</summary>
        internal static District DistrictAt(float2 p, float margin = 40)
        {
            foreach (var d in Districts) if (math.distancesq(p, d.Centre) <= (d.Radius + margin) * (d.Radius + margin)) return d;
            return null;
        }

        // ---------------------------------------------------------------------------------------------------- the ships
        /// <summary>The shipyard's display bays: blueprint, the name on its floor. Only the new ship lines: the four Nocturne
        /// warships, the five racers and the two xeno ships (owner, 2026-10-18: "lets please use the new ones only going forward").</summary>
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly (string blueprint, string name)[] Bays =
        {
            (StarterBlueprint, "STARTER WRAITH"), ("ship_nocturne_vesper", "NOCTURNE VESPER"), ("ship_nocturne_dirge", "NOCTURNE DIRGE"),
            ("ship_nocturne_requiem", "NOCTURNE REQUIEM"), ("ship_xeno_husk", "XENO HUSK"), ("ship_xeno_broodmother", "XENO BROODMOTHER"),
            ("ship_racer_needle", "NEEDLE RACER"), ("ship_racer_kite", "KITE RACER"), ("ship_racer_hammerhead", "HAMMERHEAD RAM"),
            ("ship_racer_gimbal", "GIMBAL RACER"), ("ship_racer_battering_ram", "BATTERING RAM"),
        };
        /// <summary>The start ship (config_game's start blueprint); its bay flies "starter", which clears the playground override.</summary>
        internal const string StarterBlueprint = "ship_nocturne_wraith";
        /// <summary>Two rows, 100 m apart across: six bays in the front row, the rest behind it.</summary>
        internal static float2 BayAt(int i)
        {
            const int Front = 6;
            int row = i < Front ? 0 : 1, column = row == 0 ? i : i - Front, count = row == 0 ? math.min(Bays.Length, Front) : Bays.Length - Front;
            return Shipyard.At((column - (count - 1) * .5f) * 100, row == 0 ? -40 : 140);
        }

        // ---------------------------------------------------------------------------------------------------- stations
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly Station[] Stations = BuildStations();

        static Station[] BuildStations()
        {
            var s = new List<Station>();
            void Add(string id, District d, float x, float z, string title, string detail, StationKind kind = StationKind.Activate, string ship = null)
                => s.Add(new Station(id, d, d.At(x, z), d.Yaw, title, detail, kind, ship));
            void Reset(District d, float x, float z)
                => s.Add(new Station("reset_" + d.Id, d, d.At(x, z), d.Yaw, "RESET " + d.Title, "F puts every exhibit in " + d.Title +
                    " back as it started: respawns what was destroyed and clears what you woke up. The rest of the hub is untouched.", StationKind.ResetDistrict));

            // the plaza
            s.Add(new Station("reset", null, Plaza + new float2(-80, -215), SpawnYaw, "RESET EVERYTHING",
                "F rebuilds the whole playground from scratch (a new sandbox session): ship, cargo, credits and every district.", StationKind.ResetAll));
            s.Add(new Station("campaign", null, Plaza + new float2(80, -215), SpawnYaw, "PRACTICE CAMPAIGN",
                "F starts the real opening campaign in this playground's own profile: dialogue, combat, docking, trade and travel. RESET EVERYTHING brings you back."));

            Add("flight", Flight, 0, -140, "SLALOM COURSE", "F lights the first of six gates. Fly through each highlighted ring in turn: reverse, strafe and brake through the turns.");
            Add("boost_strip", Flight, 175, -140, "BOOST STRIP", "Fly up the green strip for a speed kick (the race tracks' boost pads). Ctrl fires your own boost ability.");
            Add("brake", Flight, -175, 120, "BRAKE BOX", "Come in fast and stop inside the box: X brakes, S reverses. F times your stop.");
            Reset(Flight, 120, -185);

            Add("range", Weapons, 0, RangePadZ, "TARGET STATION", "Space or left mouse fires. Aim at the narrow bridges to cut its pods off; destroy the core for a drifting hulk. " +
                "Tests tracers, impacts, sparks, explosions, smoke, debris, sound and collider updates.");
            Reset(Weapons, 120, -150);

            Add("combat", Combat, -80, -140, "DUEL · RAIDER WRAITH", "F launches one hostile raider WRAITH across the arena: real AI, weapons, damage, power and the player-death flow.");
            Add("xeno", Combat, 80, -140, "XENO WAVE", "F wakes two HUSK skimmers and a BROODMOTHER: grown ships of obsidian tissue that hunt you with acid. Wounds open into glowing flesh; kill the heart for a hulk.");
            Reset(Combat, 180, -190);

            Add("hazards", Hazards, 0, -140, "HAZARD TOUR", "Ahead: a reactor that pulses every 4 s over 140 m (hide in the slab's shadow), energy canisters on the left (fly through to top up power, shoot one and it blows) and one mine of each payload on the right.");
            Reset(Hazards, 120, -190);

            Add("movers", Mechanisms, 0, -140, "MOVING STRUCTURES", "A sweeper arm, a sliding blast door and a switch door (shoot the gold post to slam it for 5 s), and a portal that jams for 6 s behind each ship. " +
                "All are voxel structures: they bat you aside, ram you, and can be shot apart.");
            Reset(Mechanisms, 120, -190);

            for (int i = 0; i < Bays.Length; i++)
            {
                var at = BayAt(i);
                var local = math.mul(math.transpose(new float2x2(Shipyard.Right, Shipyard.Forward)), at - Shipyard.Centre);
                Add("ship_" + Bays[i].blueprint, Shipyard, local.x, local.y - 52, "FLY " + Bays[i].name, "F swaps you into the " + Bays[i].name +
                    " and rebuilds the playground around it (you come back here). The choice holds until you pick another.", StationKind.FlyShip,
                    Bays[i].blueprint == StarterBlueprint ? "starter" : Bays[i].blueprint);
            }
            Add("editor", Shipyard, 190, -200, "BLUEPRINT LAB", "F opens the creative Blueprint Lab: place and remove blocks, rotate, mirror, undo, inspect overlays and save a blueprint. RESET EVERYTHING returns here.");

            Add("merchant", Market, -40, -110, "TRADE · ARK MARKET", "F hails the moored merchant: real stock, prices, Buy and Sell. Your first visit brings 2000 test credits; trades change your actual cargo and wallet.");
            Reset(Market, 120, -150);

            Add("salvage", Salvage, 0, -130, "SALVAGE A WRECK", DevPlaygroundSalvage.PadDetail);
            Reset(Salvage, 120, -170);

            Add("race", Race, 0, -40, "OPEN TRACK 01", "F opens Track 01 to the south and puts every pilot on the start grid; F again starts the countdown. Three laps through a fleet graveyard.");
            return s.ToArray();
        }

        // ---------------------------------------------------------------------------------------------------- structures
        internal const string Core = "ship_hub_core", Pylon = "ship_hub_pylon", Kiosk = "ship_hub_kiosk", Pier = "ship_hub_pier",
            Plinth = "ship_hub_plinth", Wall = "ship_hub_wall";

        /// <summary>The hub's permanent structures (the districts' exhibits spawn separately, when you first arrive).</summary>
        internal static IEnumerable<Piece> Pieces()
        {
            yield return new Piece(Core, Plaza, 0);
            foreach (var d in Districts)
            {
                yield return new Piece(Pylon, d.At(-70, -(d.Radius - 40)), d.Yaw, d);
                yield return new Piece(Pylon, d.At(70, -(d.Radius - 40)), d.Yaw, d);
            }
            foreach (var st in Stations)
                if (st.Kind == StationKind.Activate && st.District != null && st.Id != "boost_strip" && st.Id != "brake")
                {
                    // a kiosk behind the pad, screen toward the arriving pilot
                    float2 at = st.Position + st.District.Forward * 30;
                    yield return new Piece(Kiosk, at, st.District.Yaw, st.District);
                }
            for (int i = 0; i < Bays.Length; i++) yield return new Piece(Plinth, BayAt(i), Shipyard.Yaw, Shipyard);
            yield return new Piece(Pier, Market.At(70, -90), Market.Yaw, Market);
            // cover walls in the hazard yard and the combat arena
            yield return new Piece(Wall, Hazards.At(-60, 150), Hazards.Yaw, Hazards);
            yield return new Piece(Wall, Combat.At(-120, 60), Combat.Yaw + math.PI / 4, Combat);
            yield return new Piece(Wall, Combat.At(120, 60), Combat.Yaw - math.PI / 4, Combat);
            yield return new Piece(Wall, Combat.At(0, 170), Combat.Yaw, Combat);
        }

        // ---------------------------------------------------------------------------------------------------- signs
        internal static List<Sign> Signs()
        {
            var s = new List<Sign>();
            var plaza = PlazaColour;
            s.Add(new Sign("ARK PROVING GROUNDS", Plaza + new float2(0, -122), SpawnYaw, 1.1f, plaza));
            s.Add(new Sign("MECHANICS PLAYGROUND · FLY THROUGH A PORTAL TO VISIT A DISTRICT", Plaza + new float2(0, -136), SpawnYaw, .42f, plaza * .8f));
            s.Add(new Sign("W/S THRUST · A/D TURN · Q/E STRAFE · CTRL BOOST · X BRAKE · SPACE FIRE · F USE", Spawn + new float2(0, -26), SpawnYaw, .36f, plaza * .7f));
            var directory = new System.Text.StringBuilder("PORTALS\n");
            foreach (var d in Districts) directory.Append(Compass(d.PortalBearing)).Append(' ').Append(d.Title).Append('\n');
            s.Add(new Sign(directory.ToString().TrimEnd(), Spawn + new float2(-62, 4), SpawnYaw, .36f, plaza * .85f, HoloAlign.Left));
            foreach (var d in Districts)
            {
                // at the plaza portal, for a pilot heading out through it
                float2 portal = d.PlazaPortal, outward = math.normalize(portal - Plaza);
                s.Add(new Sign(d.Title, portal - outward * 20, d.PortalBearing, .75f, d.Colour));
                s.Add(new Sign(d.Tagline, portal - outward * 30, d.PortalBearing, .4f, d.Colour * .75f));
                // the district's entrance, read on arrival
                s.Add(new Sign(d.Title, d.At(0, -(d.Radius - 40) + 46), d.Yaw, 1.3f, d.Colour));
                s.Add(new Sign(d.Tagline, d.At(0, -(d.Radius - 40) + 32), d.Yaw, .55f, d.Colour * .8f));
                // the way home
                s.Add(new Sign("← HUB PORTAL", d.ReturnPortal - d.Forward * 16, d.Yaw, .6f, PlazaColour));
            }
            foreach (var st in Stations)
            {
                var colour = st.Kind == StationKind.ResetDistrict || st.Kind == StationKind.ResetAll ? ResetColour : st.District?.Colour ?? plaza;
                float2 forward = new float2(math.sin(st.Yaw), math.cos(st.Yaw));
                s.Add(new Sign(st.Title, st.Position + forward * (StationRadius + 5), st.Yaw, st.Kind == StationKind.ResetDistrict ? .45f : .6f, colour));
                s.Add(new Sign("F", st.Position, st.Yaw, .9f, colour));
            }
            // exhibit labels
            var amber = Hazards.Colour;
            s.Add(new Sign("PULSE REACTOR · EVERY 4 S · 140 M", Hazards.At(0, 60) - Hazards.Forward * 0 + Hazards.Right * 0 + new float2(0, 0) + Hazards.Forward * -52, Hazards.Yaw, .55f, amber));
            s.Add(new Sign("HIDE IN THE SLAB'S SHADOW", Hazards.At(0, -40), Hazards.Yaw, .45f, amber * .8f));
            s.Add(new Sign("ENERGY CANISTERS\nFLY THROUGH: +POWER\nSHOOT: IT BLOWS", Hazards.At(-180, -150), Hazards.Yaw, .45f, amber));
            string[] mines = { "REPULSOR\nSHOVES YOU AWAY", "EMP\nKILLS YOUR POWER", "SEEKER\nHOMES IN", "GRAVITY\nPULLS YOU IN" };
            for (int i = 0; i < mines.Length; i++) s.Add(new Sign(mines[i], Hazards.At(205, MineZ(i)), Hazards.Yaw, .45f, amber, HoloAlign.Left));
            var violet = Mechanisms.Colour;
            s.Add(new Sign("SWEEPER ARM", Mechanisms.At(-150, -30), Mechanisms.Yaw, .55f, violet));
            s.Add(new Sign("SLIDING DOOR", Mechanisms.At(110, -135), Mechanisms.Yaw, .55f, violet));
            s.Add(new Sign("SWITCH DOOR\nSHOOT THE GOLD POST", Mechanisms.At(110, -15), Mechanisms.Yaw, .45f, violet));
            s.Add(new Sign("PORTAL · JAMS 6 S", Mechanisms.At(40, 125), Mechanisms.Yaw, .55f, violet));
            var red = Weapons.Colour;
            foreach (float range in GalleryRanges)
                foreach (float side in new[] { -1f, 1f })
                    s.Add(new Sign(range + " M", Weapons.At(side * 170, RangePadZ + range - 20), Weapons.Yaw, .6f, red));
            s.Add(new Sign("SPACE / LEFT MOUSE: FIRE · AIM WITH THE MOUSE", Weapons.At(0, -75), Weapons.Yaw, .4f, red * .8f));
            s.Add(new Sign("ARENA", Combat.At(0, 0), Combat.Yaw, 2f, Combat.Colour * .45f));
            s.Add(new Sign("STOP INSIDE", Flight.At(-175, 120), Flight.Yaw, .55f, Flight.Colour));
            s.Add(new Sign("TRAFFIC LANE", Plaza + new float2(0, -InnerLoop - 14), SpawnYaw, .6f, new Color(1f, .7f, .3f) * .7f));
            s.Add(new Sign("3 LAPS · 6 PILOTS · F OPENS, F AGAIN STARTS", Race.At(0, 10), Race.Yaw, .45f, Race.Colour));
            return s;
        }

        static string Compass(float bearing)
        {
            string[] names = { "N ", "NE", "E ", "SE", "S ", "SW", "W ", "NW" };
            int i = (int)math.round(math.degrees(bearing) / 45) & 7;
            return names[i];
        }

        internal const float RangePadZ = -110;
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly float[] GalleryRanges = { 100, 180, 260 };

        internal static float MineZ(int i) => -110 + 75 * i;

        // ---------------------------------------------------------------------------------------------------- flight school course
        [Game.Core.StaticStateOk("constant hub layout")]
        internal static readonly float2[] Course =
        {
            Flight.At(-90, -90), Flight.At(110, -20), Flight.At(-110, 50), Flight.At(110, 120), Flight.At(0, 200), Flight.At(-40, -10),
        };

        // ---------------------------------------------------------------------------------------------------- lanes and traffic
        /// <summary>The lanes on the flight plane: plaza to each district, and the inner traffic loop.</summary>
        internal static IEnumerable<(float2 a, float2 b, float width, Color colour)> Lanes()
        {
            foreach (var d in Districts)
            {
                float2 from = d.PlazaPortal + math.normalize(d.PlazaPortal - Plaza) * 40, to = d.At(0, -d.Radius - 10);
                if (math.dot(to - from, d.PlazaPortal - Plaza) > 0) yield return (from, to, 60, d.Colour);
            }
        }

        internal static float2[] Loop(float radius, int points)
        {
            var p = new float2[points];
            for (int i = 0; i < points; i++) { float a = i * math.PI * 2 / points; p[i] = Plaza + new float2(math.sin(a), math.cos(a)) * radius; }
            return p;
        }
    }
}
#endif
