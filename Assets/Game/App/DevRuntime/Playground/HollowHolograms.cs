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
    /// stands on the floor with the place's name and its trade; and lit ribbons on the tunnel floors run from the Maw to every station
    /// in its colour, their dashes flowing toward it. All in the race track's hologram shader (dot-matrix, additive).</summary>
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
        readonly List<Transform> boards = new();
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
            Lanes(parent, look, floor);
            // holo boards instead of flat names on the floor: standing on the floor where the signs are, facing their approach; a station
            // no sign names (the quartermaster) gets its own board beside it, not floating letters
            if (look.Curtain == null) return;
            foreach (var s in Signs)
            {
                var d = At(s.Position.xz);
                // in the tunnels: the Maw's board welcomes and points to the lanes (its floor letters name it); the blast doors need none
                if (d.Name == null && s.Text == "BLAST DOORS") continue;
                bool maw = d.Name == null && s.Text == "THE MAW";
                Board(parent, look, maw ? "THE HOLLOW" : d.Name ?? s.Text, maw ? "FOLLOW YOUR LANE" : d.Trade,
                    new Vector3(s.Position.x, floor, s.Position.z) + (maw ? new Vector3(math.sin(s.Yaw), 0, math.cos(s.Yaw)) * 40 : Vector3.zero), s.Yaw, d);
            }
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

        /// <summary>A holo board standing on the floor at `at`: a panel leaning back 35 degrees so the high flight camera sees its face.
        /// It turns about its foot to face the camera, which turns with the ship (a board facing one approach
        /// showed its edge to a ship arriving from the side).</summary>
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
            // one face only: it always faces the reader, and a second face's letters would show through mirrored
            var text = new HoloMesh(); var off = normal * -.25f;
            text.Text(name, up * (Height * .72f) + off, right, -up, NamePixel, d.B);
            if (trade != null) text.Text(trade, up * (Height * .3f) + off, right, -up, TradePixel, d.A);
            Add(pivot.transform, "text", text, look.Solid, Vector3.zero, 1.8f);
            boards.Add(pivot.transform);
        }

        /// <summary>The wayfinding lanes (owner: "guide players"): from the Maw to every station a lit lane on the tunnel floor in the
        /// station's colour, its dashes flowing toward it. Lanes that share a tunnel run side by side like a transit map and peel off
        /// at the junctions, where chevrons on the floor point each branch's way.</summary>
        void Lanes(Transform parent, TrackLook look, float floor)
        {
            const float Width = 1.7f, Spacing = 2.6f;
            if (look.Ribbon != null && Paths.Length > 0)
            {
                var lanes = new HoloMesh();
                for (int k = 0; k < Paths.Length; k++)
                {
                    var (station, pts) = Paths[k];
                    if (pts == null || pts.Length < 2 || !Districts.TryGetValue(station, out var d)) continue;
                    float offset = (k - (Paths.Length - 1) * .5f) * Spacing, along = 0; int k0 = lanes.Count;
                    for (int i = 0; i < pts.Length; i++)
                    {
                        if (i > 0) along += math.distance(pts[i - 1], pts[i]);
                        float2 dir = math.normalizesafe(pts[math.min(pts.Length - 1, i + 1)] - pts[math.max(0, i - 1)]);
                        float2 rightward = new float2(dir.y, -dir.x), c = pts[i] + rightward * offset, half = rightward * (Width * .5f);
                        lanes.Vertex(new Vector3(c.x - half.x, floor + .3f, c.y - half.y), d.A, new Vector2(along, 0), new Vector2(0, Width));
                        lanes.Vertex(new Vector3(c.x + half.x, floor + .3f, c.y + half.y), d.A, new Vector2(along, 1), new Vector2(0, Width));
                        if (i > 0) lanes.Quad(k0 + (i - 1) * 2, k0 + (i - 1) * 2 + 1, k0 + i * 2 + 1, k0 + i * 2);
                    }
                }
                Add(parent, "holo lanes", lanes, look.Ribbon, Vector3.zero, 2.2f);
            }
            if (look.Pad == null) return;
            var arrows = new HoloMesh();
            foreach (var w in Waypoints)
            {
                // one chevron per way out of the junction, in the colour of the first station that way
                var seen = new List<float>();
                for (int i = 0; i < w.To.Length && i < w.Yaw.Length; i++)
                {
                    float yaw = w.Yaw[i]; bool dup = false;
                    foreach (var y in seen) dup |= math.abs(math.sin((y - yaw) * .5f)) < .1f;
                    if (dup || !Districts.TryGetValue(w.To[i], out var d)) continue;
                    seen.Add(yaw);
                    // the pad pattern fades in over 25 m at each end, so a chevron run needs about 50 m
                    const float From = 6, Length = 50;
                    float2 dir = new float2(math.sin(yaw), math.cos(yaw)), side = new float2(dir.y, -dir.x) * 4f, a = w.Position + dir * From, b = w.Position + dir * (From + Length);
                    int k = arrows.Count; var size = new Vector2(Length, 8);
                    arrows.Vertex(new Vector3(a.x - side.x, floor + .35f, a.y - side.y), d.A, new Vector2(0, 0), size);
                    arrows.Vertex(new Vector3(a.x + side.x, floor + .35f, a.y + side.y), d.A, new Vector2(1, 0), size);
                    arrows.Vertex(new Vector3(b.x + side.x, floor + .35f, b.y + side.y), d.A, new Vector2(1, Length), size);
                    arrows.Vertex(new Vector3(b.x - side.x, floor + .35f, b.y - side.y), d.A, new Vector2(0, Length), size);
                    arrows.Quad(k, k + 3, k + 2, k + 1);
                }
            }
            Add(parent, "holo junction arrows", arrows, look.Pad, Vector3.zero, 1.6f);
        }

        /// <summary>Per frame: the emblems turn and hover; the boards face the camera (`forward`, its view direction in the world).</summary>
        internal void Tick(float time, Vector3 forward)
        {
            foreach (var (t, spin, phase) in emblems)
            {
                if (t == null) continue;
                t.localRotation = Quaternion.Euler(0, (time * spin + phase) * Mathf.Rad2Deg, 0);
                var p = t.localPosition; p.y = 6 + 1.2f * math.sin(time * .9f + phase * 7); t.localPosition = p;
            }
            forward.y = 0;
            if (forward.sqrMagnitude < 1e-4f) return;
            var face = Quaternion.LookRotation(forward.normalized, Vector3.up);
            foreach (var b in boards) if (b != null) b.rotation = face;
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
