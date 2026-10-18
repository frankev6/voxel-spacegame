#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Presentation.Racing;
using Unity.Mathematics;
using UnityEngine;
using static Game.App.DevRuntime.Playground.HollowLayout;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-023 the Hollow's holograms (owner: "lights can be different colors than just boring white, also add interesting 2d
    /// and 3d holograms within the place to give it more life and also guide players instead of saying 'THE MARKET' that is so
    /// boring"). Each district has its colours; over each station a 3D emblem turns above a projector ring; by each sign a holo board
    /// stands on the floor with the place's name and its trade; and the light strips at the walls' feet along the routes take the colour
    /// of the next station that way, a line of light flowing toward it. All in the race track's hologram shader (dot-matrix, additive).</summary>
    internal sealed class HollowHolograms
    {
        /// <summary>A district's look: its two colours, its emblem and the line on its board.</summary>
        internal readonly struct District
        {
            internal readonly Color A, B; internal readonly string Emblem, Name, Trade;
            internal District(Color a, Color b, string emblem, string name, string trade) { A = a; B = b; Emblem = emblem; Name = name; Trade = trade; }
        }
        static readonly Color Pink = new(1f, .22f, .62f), Amber = new(1f, .66f, .16f), Cyan = new(.18f, .86f, 1f), Lime = new(.45f, 1f, .45f),
            Ember = new(1f, .32f, .12f), Teal = new(.2f, 1f, .78f), Violet = new(.62f, .3f, 1f), Red = new(1f, .24f, .22f), Ice = new(.7f, .88f, 1f),
            Blue = new(.3f, .55f, 1f), Aqua = new(.35f, 1f, .92f);
        [Game.Core.StaticStateOk("constant table")] static readonly Dictionary<string, District> Districts = new()
        {
            { "market", new District(Pink, Amber, "crate", "MARKET", "BUY  SELL  NO QUESTIONS") },
            { "quartermaster", new District(Red, Ice, "shield", "QUARTERMASTER", "REFIT  RESET") },
            { "den_gunsmith", new District(Ember, Amber, "turret", "GUNSMITH", "GUNS  AMMO  MODS") },
            { "den_parts", new District(Teal, Ice, "gear", "PARTS", "ENGINES  REACTORS  HULL") },
            { "den_salvage", new District(Amber, Ember, "wreck", "SALVAGE", "SCRAP  WRECKS  CASH") },
            { "den_smuggler", new District(Violet, Pink, "gem", "SMUGGLER", "ASK NOTHING") },
            { "docks", new District(Cyan, Lime, "ship", "DOCKS", "REARM  BERTHS") },
        };
        static readonly District Tunnels = new(Blue, Aqua, null, null, null);
        static readonly District Racing = new(Amber, Ice, null, "TRACK 01", "RACE PORTAL");

        /// <summary>The district round `p` (Hollow metres): the nearest station within 170 m, else the tunnels'.</summary>
        internal static District At(float2 p)
        {
            float best = 170 * 170; var found = Tunnels;
            foreach (var st in Stations)
            {
                float d = math.distancesq(st.Position, p);
                if (d < best && Districts.TryGetValue(st.Id, out var district)) { best = d; found = district; }
            }
            return found;
        }
        /// <summary>A lamp's colour: mostly its district's two colours, now and then the station's LED white.</summary>
        internal static Color LampColour(float2 p, Color white)
        {
            var d = At(p); uint h = math.hash(new int2((int)math.floor(p.x), (int)math.floor(p.y)));
            int pick = (int)(h % 10);
            return pick < 5 ? d.A : pick < 8 ? d.B : white;
        }

        readonly List<(Transform t, float spin, float phase)> emblems = new();
        readonly List<(Transform pivot, GameObject front, GameObject back)> boards = new();
        readonly List<Object> owned = new();

        internal void Build(Transform parent, TrackLook look, float floor)
        {
            if (look.Solid == null || look.Halo == null) return;
            foreach (var st in Stations)
            {
                if (!Districts.TryGetValue(st.Id, out var d)) continue;
                // the projector: a turning dashed ring on the floor of the station's circle (no dot-matrix shaft over it: from the flight camera
                // its rings read as loose dots, owner: "wtf is all the dots")
                var ring = new HoloMesh(); Disc(ring, d.A);
                var projector = Add(parent, "holo projector " + st.Id, ring, look.Halo, new Vector3(st.Position.x, floor + .5f, st.Position.y), 1.6f);
                if (projector != null) projector.transform.localScale = Vector3.one * 17;   // the halo mode draws a unit disc at its scale
                // the emblem, turning slowly beyond the station along its approach (not over the ship parked in the circle)
                var emblem = new HoloMesh(); Emblem(emblem, d.Emblem, d.A, d.B);
                float2 beyond = st.Position + new float2(math.sin(st.Yaw), math.cos(st.Yaw)) * 32;
                var go = Add(parent, "holo emblem " + st.Id, emblem, look.Solid, new Vector3(beyond.x, 6, beyond.y), 1.2f);
                if (go != null) emblems.Add((go.transform, .45f + .1f * (st.Id.Length % 3), st.Position.x * .01f));
            }
            Guides(parent, look);
            // holo boards instead of flat names on the floor: standing on the floor where the signs are, facing their approach; a station
            // no sign names (the quartermaster) gets its own board beside it, not floating letters
            if (look.Curtain == null) return;
            foreach (var s in Signs)
            {
                var d = At(s.Position.xz);
                // in the tunnels: the Maw's board welcomes and points to the lanes (its floor letters name it); the blast doors need none
                if (d.Name == null && s.Text == "BLAST DOORS") continue;
                bool maw = d.Name == null && s.Text == "THE MAW";
                Board(parent, look, maw ? "THE HOLLOW" : d.Name ?? s.Text, maw ? "FOLLOW THE WALL LIGHTS" : d.Trade,
                    new Vector3(s.Position.x, floor, s.Position.z) + (maw ? new Vector3(math.sin(s.Yaw), 0, math.cos(s.Yaw)) * 40 : Vector3.zero), s.Yaw, d);
            }
            // the race portal's board, at the back of its alcove: a ship coming to the portal reads it through the gate
            Board(parent, look, Racing.Name, Racing.Trade, new Vector3(RacePortal.x + math.sin(RacePortalYaw) * 24, floor, RacePortal.y + math.cos(RacePortalYaw) * 24),
                RacePortalYaw, Racing);
            foreach (var st in Stations)
            {
                if (!Districts.TryGetValue(st.Id, out var d)) continue;
                bool signed = false;
                foreach (var sg in Signs) signed |= math.distancesq(sg.Position.xz, st.Position) < 90 * 90;
                if (signed) continue;
                // ahead of a ship arriving on the station's heading, to the left of the emblem
                var ahead = new float2(math.sin(st.Yaw), math.cos(st.Yaw)); var left = new float2(-ahead.y, ahead.x);
                float2 p = st.Position + ahead * 30 + left * 26;
                Board(parent, look, d.Name, d.Trade, new Vector3(p.x, floor, p.y), st.Yaw, d);
            }
        }

        /// <summary>A holo board standing on the floor at `at`, facing a ship heading `yaw` (its approach), leaning back 35 degrees so
        /// the high flight camera sees its face. It stays put (owner, of boards turning with the camera: "just does not look good"); it
        /// is lettered on both faces, and only the face toward the camera shows, so neither reads mirrored through the other.</summary>
        void Board(Transform parent, TrackLook look, string name, string trade, Vector3 at, float yaw, District d)
        {
            const float NamePixel = .8f, TradePixel = .42f, Lean = 35 * Mathf.Deg2Rad, Height = 16;
            // local frame: +x the reader's right, -z toward the reader
            var right = Vector3.right; var toward = Vector3.back;
            var up = Vector3.up * math.cos(Lean) - toward * math.sin(Lean);   // its top leans away from the reader
            var normal = Vector3.Cross(right, up);
            float width = math.max(PixelFont.LineWidth(name) * NamePixel, trade == null ? 0 : PixelFont.LineWidth(trade) * TradePixel) + 8;
            var pivot = new GameObject("holo board " + name); pivot.transform.SetParent(parent, false); owned.Add(pivot);
            pivot.transform.localPosition = at; pivot.transform.localRotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            var board = new HoloMesh(); Panel(board, Vector3.zero, right, up, width, Height, d.A);
            Add(pivot.transform, "board", board, look.Curtain, Vector3.zero, 1.3f);
            var faces = new GameObject[2];
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1 : -1; var r = right * sign; var off = normal * (-.25f * sign);
                var text = new HoloMesh();
                text.Text(name, up * (Height * .72f) + off, r, -up, NamePixel, d.B);
                if (trade != null) text.Text(trade, up * (Height * .3f) + off, r, -up, TradePixel, d.A);
                faces[side] = Add(pivot.transform, side == 0 ? "front" : "back", text, look.Solid, Vector3.zero, 1.8f);
            }
            boards.Add((pivot.transform, faces[0], faces[1]));
        }

        /// <summary>Wayfinding along the walls (owner: "guide players", then, of floor lanes side by side: "less confusing and less
        /// overlapping, and I would prefer if they ran along the walls"): each light strip at a wall's foot along a route to a station
        /// takes the colour of the next station that way, with a flowing line of light above it toward that station. Strips on the trunk
        /// from the Maw (it leads everywhere) and in the caverns stay white. Call before the neons are drawn: <see cref="Guided"/>
        /// says which of them are drawn here instead.</summary>
        internal void Plan()
        {
            guides = new (int path, float a, float b, float2 inward)[Neons.Length];
            var totals = new float[Paths.Length];
            for (int k = 0; k < Paths.Length; k++) totals[k] = Along(Paths[k].Points, Paths[k].Points[^1]).along;
            for (int i = 0; i < Neons.Length; i++)
            {
                guides[i] = (-1, 0, 0, 0);
                var n = Neons[i];
                if (n.Y > 20) continue;
                float2 mid = (n.A + n.B) * .5f;
                bool nearStation = false;
                foreach (var st in Stations) nearStation |= math.distancesq(st.Position, mid) < 60 * 60;
                if (nearStation || math.lengthsq(n.B - n.A) < 1) continue;
                // the routes run down the tunnels' middles (up to 90 m wide) and the walls step and zigzag: any strip within 70 m of a
                // route lines that tunnel
                int best = -1, passing = 0; float bestLeft = float.MaxValue; float2 inward = 0;
                for (int k = 0; k < Paths.Length; k++)
                {
                    var (d, along, nearest) = Along(Paths[k].Points, mid);
                    if (d > 70 || !Districts.ContainsKey(Paths[k].Station)) continue;
                    passing++;
                    if (totals[k] - along < bestLeft) { bestLeft = totals[k] - along; best = k; inward = math.normalizesafe(nearest - mid); }
                }
                if (best < 0 || passing >= 5) continue;
                var pts = Paths[best].Points;
                guides[i] = (best, Along(pts, n.A).along, Along(pts, n.B).along, inward);
            }
        }
        internal bool Guided(int neon) => guides != null && neon < guides.Length && guides[neon].path >= 0;
        (int path, float a, float b, float2 inward)[] guides;

        /// <summary>The nearest point of a polyline to `p`: its distance, its distance along the line, and the point.</summary>
        static (float distance, float along, float2 nearest) Along(float2[] pts, float2 p)
        {
            float best = float.MaxValue, at = 0, run = 0; float2 nearest = p;
            for (int i = 1; i < pts.Length; i++)
            {
                float2 a = pts[i - 1], ab = pts[i] - a; float len = math.length(ab);
                if (len < 1e-3f) continue;
                float t = math.saturate(math.dot(p - a, ab) / (len * len)), d = math.distance(p, a + ab * t);
                if (d < best) { best = d; at = run + t * len; nearest = a + ab * t; }
                run += len;
            }
            return (best, at, nearest);
        }

        void Guides(Transform parent, TrackLook look)
        {
            if (guides == null || look.Ribbon == null) return;
            const float Width = 1.8f, Gap = .9f;
            var strips = new HoloMesh(); var flow = new HoloMesh();
            for (int i = 0; i < Neons.Length; i++)
            {
                var (path, along0, along1, inward) = guides[i];
                if (path < 0 || !Districts.TryGetValue(Paths[path].Station, out var d)) continue;
                var n = Neons[i];
                strips.Beam(new Vector3(n.A.x, n.Y, n.A.y), new Vector3(n.B.x, n.Y, n.B.y), 1.2f, .6f, d.A);
                // the flowing line: on the floor just inside the strip, its dashes running toward the station
                float2 a0 = n.A + inward * Gap, b0 = n.B + inward * Gap, a1 = a0 + inward * Width, b1 = b0 + inward * Width;
                float y = n.Y + .3f; int k = flow.Count;
                flow.Vertex(new Vector3(a0.x, y, a0.y), d.A, new Vector2(along0, 0), new Vector2(0, Width));
                flow.Vertex(new Vector3(b0.x, y, b0.y), d.A, new Vector2(along1, 0), new Vector2(0, Width));
                flow.Vertex(new Vector3(b1.x, y, b1.y), d.A, new Vector2(along1, 1), new Vector2(0, Width));
                flow.Vertex(new Vector3(a1.x, y, a1.y), d.A, new Vector2(along0, 1), new Vector2(0, Width));
                flow.Quad(k, k + 1, k + 2, k + 3);
            }
            Add(parent, "holo wall guides", strips, look.Solid, Vector3.zero, 2f);
            Add(parent, "holo wall guide flow", flow, look.Ribbon, Vector3.zero, 2.4f);
        }

        /// <summary>Per frame: the emblems turn and hover; each board shows the face toward the camera (`forward`, its view direction).</summary>
        internal void Tick(float time, Vector3 forward)
        {
            foreach (var (t, spin, phase) in emblems)
            {
                if (t == null) continue;
                t.localRotation = Quaternion.Euler(0, (time * spin + phase) * Mathf.Rad2Deg, 0);
                var p = t.localPosition; p.y = 6 + 1.2f * math.sin(time * .9f + phase * 7); t.localPosition = p;
            }
            // a board's front faces -z: the camera sees it while it looks along the board's +z
            foreach (var (t, front, back) in boards)
            {
                if (t == null || front == null || back == null) continue;
                bool facing = Vector3.Dot(forward, t.forward) >= 0;
                if (front.activeSelf != facing) front.SetActive(facing);
                if (back.activeSelf == facing) back.SetActive(!facing);
            }
        }

        internal void Dispose()
        {
            foreach (var o in owned) if (o != null) Object.Destroy(o);
            owned.Clear(); emblems.Clear(); boards.Clear();
        }

        GameObject Add(Transform parent, string name, HoloMesh b, Material material, Vector3 at, float brightness)
        {
            if (b.Count == 0) return null;
            var mesh = b.ToMesh(name); owned.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at; owned.Add(go);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", Color.white * brightness); r.SetPropertyBlock(block);
            return go;
        }

        /// <summary>A unit disc on the floor plane for the halo mode (uv −1..1).</summary>
        static void Disc(HoloMesh m, Color c)
        {
            int k = m.Count;
            m.Vertex(new Vector3(-1, 0, -1), c, new Vector2(-1, -1), default); m.Vertex(new Vector3(1, 0, -1), c, new Vector2(1, -1), default);
            m.Vertex(new Vector3(1, 0, 1), c, new Vector2(1, 1), default); m.Vertex(new Vector3(-1, 0, 1), c, new Vector2(-1, 1), default);
            m.Quad(k, k + 1, k + 2, k + 3);
        }
        /// <summary>A standing panel for the curtain mode: uv 0..1 across and up, uv1 = (width, height).</summary>
        static void Panel(HoloMesh m, Vector3 at, Vector3 right, Vector3 up, float width, float height, Color c)
        {
            int k = m.Count; var size = new Vector2(width, height); var half = right * width * .5f;
            m.Vertex(at - half, c, new Vector2(0, 0), size); m.Vertex(at + half, c, new Vector2(1, 0), size);
            m.Vertex(at + half + up * height, c, new Vector2(1, 1), size); m.Vertex(at - half + up * height, c, new Vector2(0, 1), size);
            m.Quad(k, k + 1, k + 2, k + 3);
        }

        /// <summary>A district's emblem, about 14 m across, centred on the origin (it turns about y).</summary>
        static void Emblem(HoloMesh m, string kind, Color a, Color b)
        {
            switch (kind)
            {
                case "crate":    // a cargo crate with banded edges, a coin orbiting it
                    m.Prism(Vector3.zero, 4, 6, 6, -5, 5, a);
                    m.Ring(Vector3.zero, 4, 6.4f, -1, 1, b);
                    m.Ring(new Vector3(0, 0, 0), 24, 9.5f, 2.5f, 3.3f, b);
                    break;
                case "shield":   // a shield with a cross
                    m.Prism(Vector3.zero, 6, 6, 2.5f, -6, 4, a);
                    m.Beam(new Vector3(0, -3, 6.5f), new Vector3(0, 5, 6.5f), 1.6f, 1.6f, b);
                    m.Beam(new Vector3(-3.5f, 1.5f, 6.5f), new Vector3(3.5f, 1.5f, 6.5f), 1.6f, 1.6f, b);
                    break;
                case "turret":   // a turret: a domed base and twin barrels
                    m.Prism(Vector3.zero, 8, 6, 4.5f, -5, -1, a);
                    m.Prism(Vector3.zero, 8, 4.5f, 2, -1, 2, a);
                    m.Beam(new Vector3(-1.5f, 0, 0), new Vector3(-1.5f, 1.5f, 11), 1.2f, 1.2f, b);
                    m.Beam(new Vector3(1.5f, 0, 0), new Vector3(1.5f, 1.5f, 11), 1.2f, 1.2f, b);
                    break;
                case "gear":     // a gear: a ring with teeth and a hub
                    m.Ring(Vector3.zero, 16, 6, -1.5f, 1.5f, a);
                    for (int i = 0; i < 10; i++)
                    {
                        float t = i * math.PI * 2 / 10; var dir = new Vector3(math.sin(t), 0, math.cos(t));
                        m.Beam(dir * 6, dir * 8.5f, 1.6f, 3, a);
                    }
                    m.Prism(Vector3.zero, 6, 2, 2, -2, 2, b);
                    break;
                case "wreck":    // a broken hull: two pieces drifting apart, a tumbling plate
                    m.Beam(new Vector3(-8, -1, -1), new Vector3(-1, 1, 1), 4, 4, a);
                    m.Beam(new Vector3(1.5f, 0, 0), new Vector3(8, 2.5f, -2), 3, 3, a);
                    m.Beam(new Vector3(0, 4, -3), new Vector3(2, 6, 1), 2.5f, .6f, b);
                    break;
                case "gem":      // a cut gem: two pyramids, a girdle
                    m.Prism(new Vector3(0, 0, 0), 6, 6, .2f, 0, 6, a);
                    m.Prism(new Vector3(0, 0, 0), 6, .2f, 6, -8, 0, a);
                    m.Ring(Vector3.zero, 6, 6.2f, -.5f, .5f, b);
                    break;
                case "ship":     // a small ship: a hull, wings, two engine rings
                    m.Beam(new Vector3(0, 0, -7), new Vector3(0, 0, 8), 3, 2.5f, a);
                    m.Beam(new Vector3(-8, 0, -3), new Vector3(8, 0, -3), 4, .8f, a);
                    m.Ring(new Vector3(-3, 0, -7), 10, 1.4f, -1, 1, b);
                    m.Ring(new Vector3(3, 0, -7), 10, 1.4f, -1, 1, b);
                    break;
            }
        }
    }
}
#endif
